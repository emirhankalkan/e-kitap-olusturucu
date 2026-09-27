using EKitap.Api.Entities;

namespace EKitap.Api.Contracts;

public sealed record BookResponse(
    Guid Id,
    string Name,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PaperResponse> Papers,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    string? PdfUrl,
    string? DownloadUrl)
{
    public static BookResponse From(Book book) => new(
        book.Id, book.Name, book.Status.ToString(), book.CreatedAt,
        book.Papers.OrderBy(paper => paper.SortOrder)
            .Select(paper => new PaperResponse(paper.Id, paper.OriginalFileName,
                paper.SortOrder, paper.Title, paper.StartPage)).ToArray(),
        book.CompletedAt, book.ErrorMessage,
        book.Status == BookStatus.Completed ? $"/api/books/{book.Id}/pdf" : null,
        book.Status == BookStatus.Completed ? $"/api/books/{book.Id}/download" : null);
}

public sealed record PaperResponse(Guid Id, string FileName, int SortOrder, string? Title, int? StartPage);
