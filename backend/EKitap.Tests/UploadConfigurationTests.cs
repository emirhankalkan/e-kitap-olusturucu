using EKitap.Api.Controllers;
using EKitap.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EKitap.Tests;

public sealed class UploadConfigurationTests
{
    [Fact]
    public async Task MultipartAndServerLimits_AllowTenMaximumSizedDocuments()
    {
        await using var factory = new UploadApiFactory();
        var formOptions = factory.Services.GetRequiredService<IOptions<FormOptions>>().Value;
        var kestrelOptions = factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        var createMethod = typeof(BooksController).GetMethod(nameof(BooksController.Create))!;
        var requestFormLimit = createMethod.GetCustomAttributes(typeof(RequestFormLimitsAttribute), true)
            .Cast<RequestFormLimitsAttribute>().Single();

        Assert.Equal(UploadLimits.MaxRequestBytes, formOptions.MultipartBodyLengthLimit);
        Assert.Equal(UploadLimits.MaxRequestBytes, kestrelOptions.Limits.MaxRequestBodySize);
        Assert.Equal(UploadLimits.MaxRequestBytes, requestFormLimit.MultipartBodyLengthLimit);
        Assert.True(UploadLimits.MaxRequestBytes > UploadLimits.RequiredFileCount * UploadLimits.MaxFileBytes);
    }
}
