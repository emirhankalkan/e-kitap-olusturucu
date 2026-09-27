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

    public Stream OpenDocument(Guid bookId, Guid paperId) => new FileStream(
        Path.Combine(root, "books", bookId.ToString("N"), $"{paperId:N}.docx"),
        FileMode.Open, FileAccess.Read, FileShare.Read);

    public async Task<string> SavePdfAsync(Guid bookId, byte[] bytes, CancellationToken cancellationToken)
    {
        var relativePath = $"books/{bookId:N}/book.pdf";
        var path = Path.Combine(root, relativePath);
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // Yarım PDF hiçbir zaman nihai dosya adıyla görünmez.
            File.Move(temporaryPath, path, overwrite: true);
            return relativePath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public Stream OpenPdf(Guid bookId) => new FileStream(
        Path.Combine(root, "books", bookId.ToString("N"), "book.pdf"),
        FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);

    public void DeletePdf(Guid bookId)
    {
        var path = Path.Combine(root, "books", bookId.ToString("N"), "book.pdf");
        if (File.Exists(path))
            File.Delete(path);
    }
}
