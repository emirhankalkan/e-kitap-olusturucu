namespace EKitap.Api.Entities;

public sealed class Book
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public BookStatus Status { get; set; } = BookStatus.Pending;
    public string? PdfFilePath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<Paper> Papers { get; set; } = new List<Paper>();
}
