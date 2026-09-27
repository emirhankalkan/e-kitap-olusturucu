namespace EKitap.Api.Services;

public interface IBookFileStorage
{
    Task<string> SaveAsync(Guid bookId, Guid paperId, IFormFile file, CancellationToken cancellationToken);
    void DeleteBookFiles(Guid bookId);
}
