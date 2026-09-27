namespace EKitap.Api.Services;

public sealed class BookOperationException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
