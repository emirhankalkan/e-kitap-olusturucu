namespace EKitap.Api.Entities;

public sealed class Paper
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
    public required string OriginalFileName { get; set; }
    public required string StoredFilePath { get; set; }
    public string? Title { get; set; }
    public int SortOrder { get; set; }
    public int? StartPage { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
