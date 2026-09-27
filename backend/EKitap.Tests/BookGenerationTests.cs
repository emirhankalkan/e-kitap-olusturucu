using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EKitap.Api.Contracts;
using EKitap.Api.Services;
using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig;
using Xunit;

namespace EKitap.Tests;

public sealed class BookGenerationTests
{
    [Fact]
    public async Task Generate_PersistsMetadata_AndServesInlineDownloadAndRanges()
    {
        await using var factory = new UploadApiFactory();
        using var client = factory.CreateClient();
        var pending = await Upload(client);
        Assert.Null(pending.PdfUrl);
        using var response = await client.PostAsync($"/api/books/{pending.Id}/generate", null);
        response.EnsureSuccessStatusCode();
        var completed = (await response.Content.ReadFromJsonAsync<BookResponse>())!;
        Assert.Equal("Completed", completed.Status);
        Assert.NotNull(completed.CompletedAt);
        Assert.Null(completed.ErrorMessage);
        Assert.Equal(Enumerable.Range(2, 10), completed.Papers.Select(p => p.StartPage!.Value));
        Assert.All(completed.Papers, p => Assert.Equal("Örnek bildiri", p.Title));
        var persisted = (await client.GetFromJsonAsync<BookResponse>($"/api/books/{pending.Id}"))!;
        Assert.Equal(completed.Papers.ToArray(), persisted.Papers.ToArray());
        using var inline = await client.GetAsync(completed.PdfUrl);
        Assert.Equal("application/pdf", inline.Content.Headers.ContentType!.MediaType);
        Assert.Equal("inline", inline.Content.Headers.ContentDisposition!.DispositionType);
        var bytes = await inline.Content.ReadAsByteArrayAsync();
        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(11, pdf.NumberOfPages);
        Assert.DoesNotContain("example.org", string.Join(" ", pdf.GetPages().Select(p => p.Text)));
        using var download = await client.GetAsync(completed.DownloadUrl);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, completed.PdfUrl);
        rangeRequest.Headers.Range = new RangeHeaderValue(0, 15);
        using var range = await client.SendAsync(rangeRequest);
        Assert.Equal(HttpStatusCode.PartialContent, range.StatusCode);
        Assert.Equal(bytes[..16], await range.Content.ReadAsByteArrayAsync());
        using var repeated = await client.PostAsync($"/api/books/{pending.Id}/generate", null);
        Assert.Equal(completed.CompletedAt, (await repeated.Content.ReadFromJsonAsync<BookResponse>())!.CompletedAt);
        Assert.Equal(10, Directory.GetFiles(factory.StorageDirectory, "*.docx", SearchOption.AllDirectories).Length);
        File.Delete(Directory.GetFiles(factory.StorageDirectory, "book.pdf", SearchOption.AllDirectories).Single());
        using var missingPdf = await client.GetAsync(completed.PdfUrl);
        Assert.Equal(HttpStatusCode.NotFound, missingPdf.StatusCode);
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("download")]
    public async Task PendingPdf_ReturnsConflict(string endpoint)
    {
        await using var factory = new UploadApiFactory();
        using var client = factory.CreateClient();
        var book = await Upload(client);
        using var response = await client.GetAsync($"/api/books/{book.Id}/{endpoint}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("generate")]
    [InlineData("pdf")]
    [InlineData("download")]
    public async Task UnknownBook_ReturnsNotFound(string endpoint)
    {
        await using var factory = new UploadApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(endpoint == "generate" ? HttpMethod.Post : HttpMethod.Get,
            $"/api/books/{Guid.NewGuid()}/{endpoint}");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FailedGeneration_CanBeRetried_WithoutLosingOriginalDocuments()
    {
        ControlledStorage? storage = null;
        await using var factory = new UploadApiFactory(decorateStorage: inner => storage = new ControlledStorage(inner) { Fail = true });
        using var client = factory.CreateClient();
        var book = await Upload(client);
        using var failure = await client.PostAsync($"/api/books/{book.Id}/generate", null);
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        Assert.DoesNotContain("confidential", await failure.Content.ReadAsStringAsync());
        var failed = (await client.GetFromJsonAsync<BookResponse>($"/api/books/{book.Id}"))!;
        Assert.Equal("Failed", failed.Status);
        Assert.NotNull(failed.ErrorMessage);
        Assert.Null(failed.CompletedAt);
        Assert.All(failed.Papers, paper => Assert.Null(paper.StartPage));
        Assert.Empty(Directory.GetFiles(factory.StorageDirectory, "*.pdf", SearchOption.AllDirectories));
        Assert.Equal(10, Directory.GetFiles(factory.StorageDirectory, "*.docx", SearchOption.AllDirectories).Length);
        storage!.Fail = false;
        using var retry = await client.PostAsync($"/api/books/{book.Id}/generate", null);
        retry.EnsureSuccessStatusCode();
        var completed = (await retry.Content.ReadFromJsonAsync<BookResponse>())!;
        Assert.Equal("Completed", completed.Status);
        Assert.Null(completed.ErrorMessage);
    }

    [Fact]
    public async Task ConcurrentGeneration_IsRejected_WhileStatusRemainsVisible()
    {
        ControlledStorage? storage = null;
        await using var factory = new UploadApiFactory(decorateStorage: inner => storage = new ControlledStorage(inner) { Block = true });
        using var client = factory.CreateClient();
        var book = await Upload(client);
        var first = client.PostAsync($"/api/books/{book.Id}/generate", null);
        try
        {
            await storage!.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var processing = (await client.GetFromJsonAsync<BookResponse>($"/api/books/{book.Id}"))!;
            Assert.Equal("Processing", processing.Status);
            using var second = await client.PostAsync($"/api/books/{book.Id}/generate", null);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            using var pdf = await client.GetAsync($"/api/books/{book.Id}/pdf");
            Assert.Equal(HttpStatusCode.Conflict, pdf.StatusCode);
        }
        finally { storage!.Release.TrySetResult(); }
        using var completed = await first;
        completed.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CancelledGeneration_PersistsFailedState_WithoutPublishingPdf()
    {
        ControlledStorage? storage = null;
        await using var factory = new UploadApiFactory(decorateStorage: inner => storage = new ControlledStorage(inner) { Block = true });
        using var client = factory.CreateClient();
        var book = await Upload(client);
        using var cancellation = new CancellationTokenSource();
        var request = client.PostAsync($"/api/books/{book.Id}/generate", null, cancellation.Token);
        await storage!.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        BookResponse state;
        do
        {
            await Task.Delay(25, deadline.Token);
            state = (await client.GetFromJsonAsync<BookResponse>($"/api/books/{book.Id}", deadline.Token))!;
        } while (state.Status == "Processing");
        Assert.Equal("Failed", state.Status);
        Assert.Null(state.PdfUrl);
        Assert.Empty(Directory.GetFiles(factory.StorageDirectory, "*.pdf", SearchOption.AllDirectories));
    }

    private static async Task<BookResponse> Upload(HttpClient client)
    {
        using var form = BookUploadTests.CreateForm();
        using var response = await client.PostAsync("/api/books", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookResponse>())!;
    }

    private sealed class ControlledStorage(IBookFileStorage inner) : IBookFileStorage
    {
        public bool Fail { get; set; }
        public bool Block { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> SaveAsync(Guid bookId, Guid paperId, IFormFile file, CancellationToken cancellationToken)
            => inner.SaveAsync(bookId, paperId, file, cancellationToken);
        public Stream OpenDocument(Guid bookId, Guid paperId) => inner.OpenDocument(bookId, paperId);
        public Stream OpenPdf(Guid bookId) => inner.OpenPdf(bookId);
        public void DeletePdf(Guid bookId) => inner.DeletePdf(bookId);
        public void DeleteBookFiles(Guid bookId) => inner.DeleteBookFiles(bookId);
        public async Task<string> SavePdfAsync(Guid bookId, byte[] bytes, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            if (Block) await Release.Task.WaitAsync(cancellationToken);
            var path = await inner.SavePdfAsync(bookId, bytes, cancellationToken);
            if (Fail) throw new IOException("Simulated failure; confidential details.");
            return path;
        }
    }
}
