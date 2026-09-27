namespace EKitap.Api.Services;

public sealed class UploadValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
