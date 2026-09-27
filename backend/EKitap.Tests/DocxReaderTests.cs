using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using EKitap.Api.Services;
using Xunit;

namespace EKitap.Tests;

public sealed class DocxReaderTests
{
    [Fact]
    public void SplitRuns_AreCleaned_WithoutModifyingOriginalDocument()
    {
        using var stream = CreateDocument(
            new Paragraph(new Run(new Text("Türkçe Bildiri Başlığı"))),
            new Paragraph(new Run(new Text("elif.")), new Run(new Text("kaya@")), new Run(new Text("example.org"))),
            new Paragraph(new Run(new Text("Tel: +90 (500) ")), new Run(new Text("000 00 01"))),
            new Paragraph(new Run(new Text("ORCID: 0000-0001-1000-0001"))),
            new Paragraph(new Run(new Text("Özgün metin: %24, 2026-09-27, İı Şş Ğğ Çç Öö Üü."))));
        var original = stream.ToArray();
        var result = Reader().Read(stream, "test.docx");
        Assert.Equal("Türkçe Bildiri Başlığı", result.Title);
        Assert.Equal(3, result.Paragraphs.Count);
        Assert.Equal("ORCID: 0000-0001-1000-0001", result.Paragraphs[1].Text);
        Assert.Equal("Özgün metin: %24, 2026-09-27, İı Şş Ğğ Çç Öö Üü.", result.Paragraphs[2].Text);
        Assert.Equal(original, stream.ToArray());
    }

    [Fact]
    public void Paragraphs_LineBreaks_TablesAndHyperlinkText_AreReadInOrder()
    {
        using var stream = CreateDocument(
            new Paragraph(new Run(new Text("Başlık"))),
            new Paragraph(new Run(new Text("Birinci"), new Break(), new Text("İkinci"), new TabChar(), new Text("Son"))),
            new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("Tablo hücresi")))))),
            new Paragraph(new Hyperlink(new Run(new Text("Görünen bağlantı")))));
        var result = Reader().Read(stream, "test.docx");
        Assert.Equal(new[] { "Başlık", "Birinci\nİkinci\tSon", "Tablo hücresi", "Görünen bağlantı" }, result.Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public void ExplicitPageBreak_IsAppliedToNextVisibleParagraph()
    {
        using var stream = CreateDocument(
            new Paragraph(new Run(new Text("Başlık"))),
            new Paragraph(new Run(new Break { Type = BreakValues.Page })),
            new Paragraph(new Run(new Text("a@example.org"))),
            new Paragraph(new Run(new Text("ABSTRACT"))));
        var result = Reader().Read(stream, "test.docx");
        Assert.True(result.Paragraphs[1].PageBreakBefore);
    }

    [Fact]
    public void EmptyDocument_IsRejected()
    {
        using var stream = CreateDocument(new Paragraph());
        Assert.Throws<InvalidDataException>(() => Reader().Read(stream, "empty.docx"));
    }

    [Fact]
    public void Cancellation_IsObserved()
    {
        using var stream = CreateDocument(new Paragraph(new Run(new Text("Başlık"))));
        Assert.Throws<OperationCanceledException>(() => Reader().Read(stream, "test.docx", new CancellationToken(true)));
    }

    private static DocxReaderService Reader() => new(new ContactInfoCleaner());

    private static MemoryStream CreateDocument(params OpenXmlElement[] elements)
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var part = document.AddMainDocumentPart();
            part.Document = new Document(new Body(elements));
            part.Document.Save();
        }
        stream.Position = 0;
        return stream;
    }
}
