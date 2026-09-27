namespace EKitap.Api.Contracts;

public sealed record BookResponse(
    Guid Id,
    string Name,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PaperResponse> Papers);

public sealed record PaperResponse(Guid Id, string FileName, int SortOrder, string? Title, int? StartPage);
