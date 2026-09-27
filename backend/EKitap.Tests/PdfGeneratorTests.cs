using System.Text;
using EKitap.Api.Documents;
using EKitap.Api.Services;
using UglyToad.PdfPig;
using Xunit;

namespace EKitap.Tests;

public sealed class PdfGeneratorTests
{
    public PdfGeneratorTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Toc_StartPagesAndFooters_MatchActualPdf(bool longContent)
    {
        var papers = CreatePapers(longContent);
        var result = Generator().Generate("Türkçe Bildiri Kitabı", papers.Reverse().ToArray());
        using var pdf = PdfDocument.Open(result.Bytes);
        Assert.Equal(result.PageCount, pdf.NumberOfPages);
        Assert.Equal(10, result.StartPages.Count);
        var toc = pdf.GetPage(1);
        for (var index = 0; index < papers.Length; index++)
        {
            var paper = papers[index];
            var start = result.StartPages[paper.Id];
            Assert.Contains(paper.Document.Title, pdf.GetPage(start).Text);
            Assert.True(start > (index == 0 ? 1 : result.StartPages[papers[index - 1].Id]));
            var titleWord = Assert.Single(toc.GetWords(), word => word.Text == paper.Document.Title);
            var pageWord = Assert.Single(toc.GetWords(), word => word.BoundingBox.Left > 500 &&
                Math.Abs(word.BoundingBox.Bottom - titleWord.BoundingBox.Bottom) < 5);
            Assert.Equal(start.ToString(), pageWord.Text);
        }
        foreach (var page in pdf.GetPages())
        {
            var footer = string.Concat(page.GetWords().Where(word => word.BoundingBox.Top < 60).OrderBy(word => word.BoundingBox.Left).Select(word => word.Text));
            Assert.Equal($"{page.Number}/{pdf.NumberOfPages}", footer);
        }
        if (longContent)
            Assert.True(pdf.NumberOfPages > 20);
    }

    [Fact]
    public void LongTitles_CanMoveTocToMultiplePages()
    {
        var papers = CreatePapers(false).Select(paper => paper with
        {
            Document = paper.Document with
            {
                Title = paper.Document.Title + " " + string.Join(" ", Enumerable.Repeat("Uzun Türkçe akademik araştırma başlığı", 15))
            }
        }).ToArray();
        var result = Generator().Generate("Çok sayfalı içindekiler", papers);
        using var pdf = PdfDocument.Open(result.Bytes);
        Assert.True(result.StartPages[papers[0].Id] > 2);
        foreach (var paper in papers)
            Assert.Contains(Normalize(paper.Document.Title), Normalize(pdf.GetPage(result.StartPages[paper.Id]).Text));
    }

    [Fact]
    public void PdfContainsTurkishTextAndOrcid_ButNoContactInformation()
    {
        var papers = CreatePapers(false);
        var result = Generator().Generate("İı Şş Ğğ Çç Öö Üü a@example.org", papers);
        using var pdf = PdfDocument.Open(result.Bytes);
        var text = string.Concat(pdf.GetPages().Select(page => page.Text));
        Assert.DoesNotContain("example.org", text);
        Assert.DoesNotContain("0500", text);
        Assert.Contains("0000-0002-1825-0097", text);
        Assert.Contains(Normalize("İı Şş Ğğ Çç Öö Üü"), Normalize(text));
        Assert.DoesNotContain("example.org", pdf.Information.Title);
    }

    [Fact]
    public void InvalidOrder_IsRejected()
    {
        var papers = CreatePapers(false);
        papers[1] = papers[1] with { SortOrder = 1 };
        Assert.Throws<ArgumentException>(() => Generator().Generate("Kitap", papers));
    }

    [Fact]
    public void Cancellation_IsObservedBeforeRendering()
    {
        Assert.Throws<OperationCanceledException>(() => Generator().Generate("Kitap", CreatePapers(false), new CancellationToken(true)));
    }

    internal static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormKC).Where(character => !char.IsWhiteSpace(character)));

    private static PdfGeneratorService Generator() => new(new ContactInfoCleaner());

    private static PaperContent[] CreatePapers(bool longContent) => Enumerable.Range(1, 10).Select(index =>
    {
        var title = $"Bildiri{index:D2}";
        var body = string.Join(" ", Enumerable.Repeat("Araştırma bulguları Türkçe içerikle eksiksiz korunur.", longContent ? 200 : 1));
        return new PaperContent(Guid.NewGuid(), index, new DocumentContent(title,
        [
            new DocumentParagraph(title, IsBold: true),
            new DocumentParagraph("İı Şş Ğğ Çç Öö Üü"),
            new DocumentParagraph("E-posta: elif@example.org | Tel: 0500 000 00 01 | ORCID: 0000-0002-1825-0097"),
            new DocumentParagraph(body)
        ]));
    }).ToArray();
}
