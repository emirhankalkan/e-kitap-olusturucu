using System.Globalization;
using EKitap.Api.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EKitap.Api.Services;

public sealed class PdfGeneratorService(ContactInfoCleaner cleaner)
{
    public GeneratedBookPdf Generate(
        string bookName, IReadOnlyList<PaperContent> papers, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookName);
        if (bookName.Length > 200)
            throw new ArgumentException("Kitap adı en fazla 200 karakter olabilir.", nameof(bookName));
        ArgumentNullException.ThrowIfNull(papers);
        if (papers.Count != UploadLimits.RequiredFileCount || papers.Select(paper => paper.Id).Distinct().Count() != papers.Count ||
            !papers.Select(paper => paper.SortOrder).Order().SequenceEqual(Enumerable.Range(1, UploadLimits.RequiredFileCount)))
            throw new ArgumentException("Bildiriler benzersiz kimliklerle 1–10 sırasında olmalıdır.", nameof(papers));

        var title = cleaner.Clean(bookName);
        if (string.IsNullOrWhiteSpace(title))
            title = "Bildiri Kitabı";
        var orderedPapers = papers.OrderBy(paper => paper.SortOrder).Select(CleanPaper).ToArray();
        var startPages = new Dictionary<Guid, int>();
        var pageCount = 0;
        cancellationToken.ThrowIfCancellationRequested();

        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(48);
            page.MarginVertical(42);
            page.DefaultTextStyle(style => style.FontFamily("Lato").FontSize(11).LineHeight(1.35f));
            page.Footer().PaddingTop(16).AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Darken1));
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages().Format(number =>
                {
                    if (number.HasValue) pageCount = number.Value;
                    return (number ?? 9999).ToString(CultureInfo.InvariantCulture);
                });
            });

            page.Content().Column(column =>
            {
                column.Item().Text(title).FontSize(23).Bold();
                column.Item().PaddingTop(20).Text("İçindekiler").FontSize(17).Bold();
                column.Item().PaddingTop(8).PaddingBottom(18).Text("10 bildiri").FontColor(Colors.Grey.Darken1);

                foreach (var paper in orderedPapers)
                {
                    column.Item().PreventPageBreak().PaddingBottom(14).SectionLink(SectionName(paper)).Row(row =>
                    {
                        row.ConstantItem(24).Text($"{paper.SortOrder:D2}").FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().PaddingRight(12).Text(paper.Document.Title).FontSize(10);
                        row.ConstantItem(36).AlignRight().Text(text =>
                        {
                            // QuestPDF ikinci geçişte gerçek bölüm sayfasını çözer.
                            text.BeginPageNumberOfSection(SectionName(paper)).Format(number =>
                            {
                                if (number.HasValue) startPages[paper.Id] = number.Value;
                                return (number ?? 9999).ToString(CultureInfo.InvariantCulture);
                            });
                        });
                    });
                }

                foreach (var paper in orderedPapers)
                {
                    column.Item().PageBreak();
                    column.Item().Section(SectionName(paper)).Column(content =>
                    {
                        var titleInBody = paper.Document.Paragraphs[0].Text == paper.Document.Title;
                        if (!titleInBody)
                            content.Item().PaddingBottom(14).Text(paper.Document.Title).FontSize(16).Bold();

                        for (var index = 0; index < paper.Document.Paragraphs.Count; index++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var paragraph = paper.Document.Paragraphs[index];
                            if (index > 0 && paragraph.PageBreakBefore)
                                content.Item().PageBreak();
                            var isTitle = index == 0 && titleInBody;
                            content.Item().EnsureSpace(isTitle ? 90 : 35).PaddingBottom(isTitle ? 14 : 8).Text(text =>
                            {
                                switch (paragraph.Alignment)
                                {
                                    case ParagraphAlignment.Center: text.AlignCenter(); break;
                                    case ParagraphAlignment.Right: text.AlignRight(); break;
                                    case ParagraphAlignment.Justify: text.Justify(); break;
                                }
                                var span = text.Span(paragraph.Text);
                                if (isTitle) span.FontSize(16).Bold();
                                else if (paragraph.IsBold) span.Bold();
                                if (paragraph.IsItalic) span.Italic();
                            });
                        }
                    });
                }
            });
        })).WithMetadata(new DocumentMetadata { Title = title, Creator = "E-Kitap Oluşturucu" });

        var bytes = document.GeneratePdf();
        cancellationToken.ThrowIfCancellationRequested();
        if (startPages.Count != papers.Count || pageCount < 1)
            throw new InvalidOperationException("PDF bölüm sayfaları hesaplanamadı.");
        return new GeneratedBookPdf(bytes, pageCount, startPages.AsReadOnly());
    }

    private PaperContent CleanPaper(PaperContent paper)
    {
        var paragraphs = paper.Document.Paragraphs
            .Select(paragraph => paragraph with { Text = cleaner.Clean(paragraph.Text) })
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph.Text)).ToArray();
        var title = cleaner.Clean(paper.Document.Title);
        if (paragraphs.Length == 0 || string.IsNullOrWhiteSpace(title) || title.Length > 1000)
            throw new ArgumentException("Bildiri başlığı ve içeriği geçerli olmalıdır.", nameof(paper));
        return paper with { Document = new DocumentContent(title, paragraphs) };
    }

    private static string SectionName(PaperContent paper) => $"paper-{paper.Id:N}";
}
