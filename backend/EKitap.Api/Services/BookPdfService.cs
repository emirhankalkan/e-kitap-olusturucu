using EKitap.Api.Data;
using EKitap.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EKitap.Api.Services;

public sealed class BookPdfService(AppDbContext dbContext, IBookFileStorage storage)
{
    public async Task<Stream> OpenAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await dbContext.Books.AsNoTracking()
            .SingleOrDefaultAsync(book => book.Id == id, cancellationToken)
            ?? throw new BookOperationException(StatusCodes.Status404NotFound, "Kitap bulunamadı.");
        if (book.Status != BookStatus.Completed || string.IsNullOrWhiteSpace(book.PdfFilePath))
            throw new BookOperationException(StatusCodes.Status409Conflict, "PDF henüz hazır değil.");

        try
        {
            return storage.OpenPdf(book.Id);
        }
        catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new BookOperationException(StatusCodes.Status404NotFound, "Kitabın PDF dosyası bulunamadı.");
        }
    }
}
