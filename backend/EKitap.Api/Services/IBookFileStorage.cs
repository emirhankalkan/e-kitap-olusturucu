namespace EKitap.Api.Services;

public interface IBookFileStorage
{
    Task<string> SaveAsync(Guid bookId, Guid paperId, IFormFile file, CancellationToken cancellationToken);
    Stream OpenDocument(Guid bookId, Guid paperId);
    Task<string> SavePdfAsync(Guid bookId, byte[] bytes, CancellationToken cancellationToken);
    Stream OpenPdf(Guid bookId);
    void DeletePdf(Guid bookId);
    void DeleteBookFiles(Guid bookId);
}
