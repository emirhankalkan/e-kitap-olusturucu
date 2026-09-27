namespace EKitap.Api.Services;

public static class UploadLimits
{
    public const int RequiredFileCount = 10;
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const long MaxRequestBytes = RequiredFileCount * MaxFileBytes + 1024 * 1024;
    public const long MaxExpandedDocxBytes = 50 * 1024 * 1024;
}
