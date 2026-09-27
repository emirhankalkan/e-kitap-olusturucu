using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using EKitap.Api.Documents;
using EKitap.Api.Services;
using UglyToad.PdfPig;
using Xunit;

namespace EKitap.Tests;

public sealed class RealSampleBookTests
{
    [LocalSamplesFact]
    public async Task SuppliedDocuments_ProduceCleanPdf_WithAllOtherTextAndCorrectPages()
    {
        var directory = Environment.GetEnvironmentVariable("EKITAP_SAMPLE_DIRECTORY")!;
        var files = Directory.GetFiles(directory, "*.docx").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
        Assert.Equal(10, files.Length);
        var cleaner = new ContactInfoCleaner();
        var reader = new DocxReaderService(cleaner);
        var papers = new List<PaperContent>();
        var expectedTexts = new Dictionary<Guid, List<string>>();
        var emails = new List<string>();
        var orcids = new List<string>();

        for (var index = 0; index < files.Length; index++)
        {
            using var source = new FileStream(files[index], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bytes = new MemoryStream();
            await source.CopyToAsync(bytes);
            var originalHash = SHA256.HashData(bytes.ToArray());
            bytes.Position = 0;
            var content = reader.Read(bytes, Path.GetFileName(files[index]));
            Assert.Equal(originalHash, SHA256.HashData(bytes.ToArray()));
            var paper = new PaperContent(Guid.NewGuid(), index + 1, content);
            papers.Add(paper);
            bytes.Position = 0;
            using var rawDocument = WordprocessingDocument.Open(bytes, false);
            var rawParagraphs = rawDocument.MainDocumentPart!.Document!.Body!.Descendants<Paragraph>()
                .Select(paragraph => paragraph.InnerText).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
            expectedTexts[paper.Id] = rawParagraphs.Where(text => !text.Contains('@')).ToList();
            foreach (var raw in rawParagraphs)
            {
                emails.AddRange(Regex.Matches(raw, @"[\w.]+@example\.org").Select(match => match.Value));
                orcids.AddRange(Regex.Matches(raw, @"\d{4}-\d{4}-\d{4}-\d{3}[\dX]").Select(match => match.Value));
            }
        }

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var result = new PdfGeneratorService(cleaner).Generate("Akademik Bildiriler Kitabı", papers);
        using var pdf = PdfDocument.Open(result.Bytes);
        Assert.Equal(result.PageCount, pdf.NumberOfPages);
        var allText = string.Concat(pdf.GetPages().Select(page => page.Text));
        Assert.Equal(13, emails.Count);
        Assert.All(emails, email => Assert.DoesNotContain(email, allText));
        Assert.DoesNotContain("example.org", allText);
        Assert.DoesNotMatch(@"(?:\+90|0500|0\s*\(500\)|\(0500\)|0 500)", allText);
        Assert.Equal(3, orcids.Count);
        Assert.All(orcids, orcid => Assert.Contains(orcid, allText));

        for (var index = 0; index < papers.Count; index++)
        {
            var paper = papers[index];
            var start = result.StartPages[paper.Id];
            var end = index + 1 < papers.Count ? result.StartPages[papers[index + 1].Id] - 1 : result.PageCount;
            var text = PdfGeneratorTests.Normalize(string.Concat(Enumerable.Range(start, end - start + 1).Select(number => pdf.GetPage(number).Text)));
            Assert.Contains(PdfGeneratorTests.Normalize(paper.Document.Title), PdfGeneratorTests.Normalize(pdf.GetPage(start).Text));
            foreach (var expected in expectedTexts[paper.Id])
                Assert.Contains(PdfGeneratorTests.Normalize(expected), text);
        }

        var output = Environment.GetEnvironmentVariable("EKITAP_SAMPLE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            await File.WriteAllBytesAsync(Path.Combine(output, "sample-book.pdf"), result.Bytes);
            await File.WriteAllTextAsync(Path.Combine(output, "sample-book.json"), JsonSerializer.Serialize(new
            {
                result.PageCount,
                Papers = papers.Select(paper => new { paper.SortOrder, paper.Document.Title, StartPage = result.StartPages[paper.Id] })
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}

public sealed class LocalSamplesFactAttribute : FactAttribute
{
    public LocalSamplesFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EKITAP_SAMPLE_DIRECTORY")))
            Skip = "Gerçek örnekler için EKITAP_SAMPLE_DIRECTORY ayarlanmalıdır.";
    }
}
