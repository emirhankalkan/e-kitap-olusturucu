namespace EKitap.Api.Documents;

public enum ParagraphAlignment { Left, Center, Right, Justify }

public sealed record DocumentParagraph(
    string Text,
    bool IsBold = false,
    bool IsItalic = false,
    ParagraphAlignment Alignment = ParagraphAlignment.Left,
    bool PageBreakBefore = false);

public sealed record DocumentContent(string Title, IReadOnlyList<DocumentParagraph> Paragraphs);

public sealed record PaperContent(Guid Id, int SortOrder, DocumentContent Document);

public sealed record GeneratedBookPdf(
    byte[] Bytes,
    int PageCount,
    IReadOnlyDictionary<Guid, int> StartPages);
