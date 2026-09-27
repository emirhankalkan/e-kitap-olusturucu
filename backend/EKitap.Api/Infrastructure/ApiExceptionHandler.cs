using EKitap.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EKitap.Api.Infrastructure;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        if (exception is UploadValidationException validationException)
        {
            problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [validationException.Field] = [validationException.Message]
            })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Yükleme bilgilerini kontrol edin."
            };
        }
        else if (exception is BadHttpRequestException badRequest)
        {
            problem = new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Title = badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "Yükleme boyutu izin verilen sınırı aşıyor."
                    : "Geçersiz yükleme isteği."
            };
        }
        else
        {
            logger.LogError(exception, "İstek tamamlanamadı. TraceId: {TraceId}", httpContext.TraceIdentifier);
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "İşlem tamamlanamadı. Lütfen tekrar deneyin."
            };
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        });
        return true;
    }
}
