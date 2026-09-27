using EKitap.Api.Contracts;
using EKitap.Api.Data;
using EKitap.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EKitap.Api.Services;

public sealed class BookService(
    AppDbContext dbContext,
    DocxUploadValidator validator,
    IBookFileStorage storage,
    ILogger<BookService> logger)
{
    public async Task<BookResponse> CreateAsync(CreateBookRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        validator.ValidateRequest(name, request.Files);
        foreach (var file in request.Files)
            await validator.ValidateFileAsync(file, cancellationToken);

        var book = new Book { Name = name };
        try
        {
            for (var index = 0; index < request.Files.Count; index++)
            {
                var file = request.Files[index];
                var paperId = Guid.NewGuid();
                var path = await storage.SaveAsync(book.Id, paperId, file, cancellationToken);
                book.Papers.Add(new Paper
                {
                    Id = paperId,
                    BookId = book.Id,
                    OriginalFileName = DocxUploadValidator.GetFileName(file.FileName),
                    StoredFilePath = path,
                    SortOrder = index + 1
                });
            }

            dbContext.Books.Add(book);
            // Tek SaveChanges, kitap ve bildirileri aynı transaction içinde kaydeder.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                storage.DeleteBookFiles(book.Id);
            }
            catch (Exception cleanupException)
            {
                logger.LogError(cleanupException, "Başarısız yüklemenin dosyaları temizlenemedi. BookId: {BookId}", book.Id);
            }
            throw;
        }

        return ToResponse(book);
    }

    public async Task<BookResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await dbContext.Books.AsNoTracking()
            .Include(book => book.Papers)
            .SingleOrDefaultAsync(book => book.Id == id, cancellationToken);
        return book is null ? null : ToResponse(book);
    }

    private static BookResponse ToResponse(Book book) => new(
        book.Id, book.Name, book.Status.ToString(), book.CreatedAt,
        book.Papers.OrderBy(paper => paper.SortOrder)
            .Select(paper => new PaperResponse(paper.Id, paper.OriginalFileName,
                paper.SortOrder, paper.Title, paper.StartPage)).ToArray());
}
