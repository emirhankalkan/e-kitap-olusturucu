using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using EKitap.Api.Documents;

namespace EKitap.Api.Services;

public sealed class DocxReaderService(ContactInfoCleaner cleaner)
{
    public DocumentContent Read(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var document = WordprocessingDocument.Open(stream, false, new OpenSettings
        {
            AutoSave = false,
            MaxCharactersInPart = UploadLimits.MaxExpandedDocxBytes
        });
        var body = document.MainDocumentPart?.Document?.Body
            ?? throw new InvalidDataException("Word belgesinin gövdesi bulunamadı.");
        var paragraphs = new List<DocumentParagraph>();
        var pendingPageBreak = false;

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (paragraph.Ancestors().Any(element => element is DeletedRun or MoveFromRun))
                continue;

            var text = new StringBuilder();
            AppendText(paragraph, text);
            var runs = paragraph.Descendants<Run>().Where(run => run.Descendants<Text>().Any()).ToArray();
            var bold = runs.Length > 0 && runs.All(run => IsEnabled(run.RunProperties?.Bold));
            var italic = runs.Length > 0 && runs.All(run => IsEnabled(run.RunProperties?.Italic));
            var alignment = GetAlignment(paragraph.ParagraphProperties?.Justification?.Val?.Value);
            pendingPageBreak |= IsEnabled(paragraph.ParagraphProperties?.PageBreakBefore);

            // Run parçaları birleştirilir; bölünmüş e-posta/telefonlar da yakalanır.
            var segments = text.ToString().Split('\f');
            for (var index = 0; index < segments.Length; index++)
            {
                if (index > 0)
                    pendingPageBreak = true;
                var cleaned = cleaner.Clean(segments[index]);
                if (string.IsNullOrWhiteSpace(cleaned))
                    continue;
                paragraphs.Add(new DocumentParagraph(cleaned, bold, italic, alignment, pendingPageBreak));
                pendingPageBreak = false;
            }
        }

        if (paragraphs.Count == 0)
            throw new InvalidDataException("Word belgesinde kullanılabilir metin bulunamadı.");

        // İçindekiler başlığı ilk uygun paragraftan alınır; uzun metinde dosya adına dönülür.
        var title = paragraphs[0].Text.Length <= 1000
            ? paragraphs[0].Text
            : cleaner.Clean(Path.GetFileNameWithoutExtension(DocxUploadValidator.GetFileName(fileName)).Replace('_', ' '));
        if (string.IsNullOrWhiteSpace(title))
            title = "Başlıksız bildiri";
        return new DocumentContent(title, paragraphs.AsReadOnly());
    }

    private static bool IsEnabled(OnOffType? value) => value is not null && (value.Val?.Value ?? true);

    private static ParagraphAlignment GetAlignment(JustificationValues? value) => value switch
    {
        var alignment when alignment == JustificationValues.Center => ParagraphAlignment.Center,
        var alignment when alignment == JustificationValues.Right => ParagraphAlignment.Right,
        var alignment when alignment == JustificationValues.Both => ParagraphAlignment.Justify,
        _ => ParagraphAlignment.Left
    };

    private static void AppendText(OpenXmlElement element, StringBuilder text)
    {
        foreach (var child in element.ChildElements)
        {
            switch (child)
            {
                case DeletedRun or MoveFromRun or Paragraph:
                    break;
                case Text value:
                    text.Append(value.Text);
                    break;
                case TabChar:
                    text.Append('\t');
                    break;
                case Break value:
                    text.Append(value.Type?.Value == BreakValues.Page ? '\f' : '\n');
                    break;
                case CarriageReturn:
                    text.Append('\n');
                    break;
                case NoBreakHyphen:
                    text.Append('-');
                    break;
                default:
                    AppendText(child, text);
                    break;
            }
        }
    }
}
