namespace EKitap.Api.Services;

public sealed class LocalBookFileStorage(IWebHostEnvironment environment) : IBookFileStorage
{
    private readonly string root = Path.Combine(environment.ContentRootPath, "Storage");

    public async Task<string> SaveAsync(
        Guid bookId, Guid paperId, IFormFile file, CancellationToken cancellationToken)
    {
        // Kullanıcı dosya adı fiziksel dosya yoluna katılmaz.
        var relativePath = $"books/{bookId:N}/{paperId:N}.docx";
        var absolutePath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var stream = new FileStream(absolutePath, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await file.CopyToAsync(stream, cancellationToken);
        return relativePath;
    }

    public void DeleteBookFiles(Guid bookId)
    {
        var directory = Path.Combine(root, "books", bookId.ToString("N"));
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
