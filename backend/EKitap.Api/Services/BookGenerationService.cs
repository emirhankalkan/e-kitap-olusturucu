using EKitap.Api.Contracts;
using EKitap.Api.Data;
using EKitap.Api.Documents;
using EKitap.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EKitap.Api.Services;

public sealed class BookGenerationService(
    AppDbContext dbContext,
    IBookFileStorage storage,
    DocxReaderService reader,
    PdfGeneratorService generator,
    ILogger<BookGenerationService> logger)
{
    public async Task<BookResponse> GenerateAsync(Guid bookId, CancellationToken cancellationToken)
    {
        var book = await dbContext.Books.Include(book => book.Papers)
            .SingleOrDefaultAsync(book => book.Id == bookId, cancellationToken)
            ?? throw new BookOperationException(StatusCodes.Status404NotFound, "Kitap bulunamadı.");

        if (book.Status == BookStatus.Completed)
            return BookResponse.From(book);

        cancellationToken.ThrowIfCancellationRequested();
        using var claimTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        // Koşullu UPDATE, farklı isteklerin aynı kitabı birlikte üretmesini engeller.
        var claimed = await dbContext.Books
            .Where(book => book.Id == bookId && (book.Status == BookStatus.Pending || book.Status == BookStatus.Failed))
            .ExecuteUpdateAsync(update => update
                .SetProperty(book => book.Status, BookStatus.Processing)
                .SetProperty(book => book.ErrorMessage, (string?)null)
                .SetProperty(book => book.CompletedAt, (DateTimeOffset?)null)
                .SetProperty(book => book.PdfFilePath, (string?)null), claimTimeout.Token);

        if (claimed == 0)
            throw new BookOperationException(StatusCodes.Status409Conflict, "Kitap zaten işleniyor veya durumu değişti. Sayfayı yenileyin.");

        try
        {
            book.Status = BookStatus.Processing;
            book.ErrorMessage = null;
            book.CompletedAt = null;
            book.PdfFilePath = null;
            var contents = new List<PaperContent>();
            foreach (var paper in book.Papers.OrderBy(paper => paper.SortOrder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = storage.OpenDocument(book.Id, paper.Id);
                contents.Add(new PaperContent(paper.Id, paper.SortOrder,
                    reader.Read(stream, paper.OriginalFileName, cancellationToken)));
            }

            var result = generator.Generate(book.Name, contents, cancellationToken);
            book.PdfFilePath = await storage.SavePdfAsync(book.Id, result.Bytes, cancellationToken);
            foreach (var paper in book.Papers)
            {
                paper.Title = contents.Single(content => content.Id == paper.Id).Document.Title;
                paper.StartPage = result.StartPages[paper.Id];
            }
            book.Status = BookStatus.Completed;
            book.CompletedAt = DateTimeOffset.UtcNow;
            // Dosya hazır olmadan Completed kaydedilmez; tüm metadata birlikte kaydedilir.
            await dbContext.SaveChangesAsync(cancellationToken);
            return BookResponse.From(book);
        }
        catch (Exception exception)
        {
            var message = exception is OperationCanceledException
                ? "Kitap oluşturma işlemi iptal edildi. Yeniden deneyebilirsiniz."
                : "Kitap oluşturulamadı. Belgeleri kontrol edip yeniden deneyin.";
            logger.LogError(exception, "Kitap üretimi başarısız. BookId: {BookId}", bookId);
            await MarkFailedAsync(bookId, message);
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
                throw;
            throw new BookOperationException(StatusCodes.Status500InternalServerError, message);
        }
    }

    private async Task MarkFailedAsync(Guid bookId, string message)
    {
        try
        {
            storage.DeletePdf(bookId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Başarısız üretimin PDF'i temizlenemedi. BookId: {BookId}", bookId);
        }

        try
        {
            // İstemci bağlantısı kesilse de hata durumu kaydedilir.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            dbContext.ChangeTracker.Clear();
            await dbContext.Books.Where(book => book.Id == bookId && book.Status == BookStatus.Processing)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(book => book.Status, BookStatus.Failed)
                    .SetProperty(book => book.ErrorMessage, message)
                    .SetProperty(book => book.PdfFilePath, (string?)null)
                    .SetProperty(book => book.CompletedAt, (DateTimeOffset?)null), timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Kitabın hata durumu kaydedilemedi. BookId: {BookId}", bookId);
        }
    }
}
