using EKitap.Api.Data;
using EKitap.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EKitap.Tests;

public sealed class UploadApiFactory(bool failDatabase = false, bool failStorage = false)
    : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public string StorageDirectory { get; } = Path.Combine(Path.GetTempPath(), "ekitap-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();
        connection.CreateFunction("LEN", (string value) => value.TrimEnd().Length);
        Directory.CreateDirectory(StorageDirectory);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(connection);
                if (failDatabase)
                    options.AddInterceptors(new FailingSaveInterceptor());
            });
            services.RemoveAll<IBookFileStorage>();
            services.AddSingleton<IBookFileStorage>(provider =>
            {
                var environment = new StorageEnvironment { ContentRootPath = StorageDirectory };
                var storage = new LocalBookFileStorage(environment);
                return failStorage ? new FailingStorage(storage) : storage;
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await connection.DisposeAsync();
        if (Directory.Exists(StorageDirectory))
            Directory.Delete(StorageDirectory, recursive: true);
    }

    private sealed class StorageEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "EKitap.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = string.Empty;
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => throw new DbUpdateException("Simulated database failure; confidential details.");
    }

    private sealed class FailingStorage(IBookFileStorage inner) : IBookFileStorage
    {
        private int savedCount;

        public async Task<string> SaveAsync(Guid bookId, Guid paperId, IFormFile file, CancellationToken cancellationToken)
        {
            var path = await inner.SaveAsync(bookId, paperId, file, cancellationToken);
            if (++savedCount == 2)
                throw new IOException("Simulated disk failure; confidential details.");
            return path;
        }

        public void DeleteBookFiles(Guid bookId) => inner.DeleteBookFiles(bookId);
    }
}
