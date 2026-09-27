using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace EKitap.Api.Services;

public sealed class DocxUploadValidator
{
    private const string MainContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string StrictWordNamespace = "http://purl.oclc.org/ooxml/wordprocessingml/main";

    public void ValidateRequest(string name, IReadOnlyCollection<IFormFile> files)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            throw new UploadValidationException("Name", "Kitap adı 1–200 karakter arasında olmalıdır.");

        if (files.Count != UploadLimits.RequiredFileCount)
            throw new UploadValidationException("Files", "Tam olarak 10 adet .docx dosyası yüklemelisiniz.");
    }

    public async Task ValidateFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var name = GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 ||
            !string.Equals(Path.GetExtension(name), ".docx", StringComparison.OrdinalIgnoreCase))
            throw new UploadValidationException("Files", "Dosyaların uzantısı .docx, adı en fazla 255 karakter olmalıdır.");

        if (file.Length <= 0 || file.Length > UploadLimits.MaxFileBytes)
            throw new UploadValidationException("Files", $"'{name}' boş olamaz ve 10 MB sınırını aşamaz.");

        try
        {
            await using var stream = file.OpenReadStream();
            if (stream.Length < 22)
                throw new InvalidDataException();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            // DOCX bir ZIP paketidir; açılmış içerik boyutunu da sınırla.
            if (archive.Entries.Count > 2048 ||
                archive.Entries.Any(entry => entry.Length > UploadLimits.MaxExpandedDocxBytes) ||
                archive.Entries.Sum(entry => entry.Length) > UploadLimits.MaxExpandedDocxBytes)
                throw new InvalidDataException();

            var types = await ReadXmlAsync(archive, "[Content_Types].xml", cancellationToken);
            XNamespace contentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
            if (types.Root?.Name != contentTypes + "Types" ||
                !types.Root.Elements(contentTypes + "Override").Any(element =>
                    (string?)element.Attribute("PartName") == "/word/document.xml" &&
                    (string?)element.Attribute("ContentType") == MainContentType))
                throw new InvalidDataException();

            var relationships = await ReadXmlAsync(archive, "_rels/.rels", cancellationToken);
            XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
            if (relationships.Root?.Name != packageRelationships + "Relationships" ||
                !relationships.Root.Elements(packageRelationships + "Relationship").Any(element =>
                    (string?)element.Attribute("Type") is
                        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" or
                        "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument" &&
                    (string?)element.Attribute("Target") is "word/document.xml" or "/word/document.xml" &&
                    (string?)element.Attribute("TargetMode") is null or "Internal"))
                throw new InvalidDataException();

            var document = await ReadXmlAsync(archive, "word/document.xml", cancellationToken);
            var root = document.Root;
            if (root is null || root.Name.LocalName != "document" ||
                root.Name.NamespaceName is not (WordNamespace or StrictWordNamespace) ||
                root.Element(root.Name.Namespace + "body") is null)
                throw new InvalidDataException();
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or NotSupportedException or ArgumentOutOfRangeException)
        {
            throw new UploadValidationException("Files", $"'{name}' geçerli ve desteklenen bir Word belgesi değil.");
        }
    }

    public static string GetFileName(string fileName) => Path.GetFileName(fileName.Replace('\\', '/'));

    private static async Task<XDocument> ReadXmlAsync(
        ZipArchive archive, string entryName, CancellationToken cancellationToken)
    {
        var entries = archive.Entries.Where(entry => entry.FullName == entryName).ToArray();
        if (entries.Length != 1)
            throw new InvalidDataException();

        await using var stream = entries[0].Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = UploadLimits.MaxExpandedDocxBytes
        });
        return await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
    }
}
