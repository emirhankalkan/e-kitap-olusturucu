using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using EKitap.Api.Contracts;
using EKitap.Api.Data;
using EKitap.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EKitap.Tests;

public sealed class BookUploadTests
{
    [Fact]
    public async Task TenDocuments_AreSavedUnchangedInRequestOrder_AndCanBeRetrieved()
    {
        await using var factory = new UploadApiFactory();
        using var client = factory.CreateClient();
        var bytes = CreateDocx();
        using var form = CreateForm(bytes: bytes, fileName: "../../duplicate.docx");
        using var response = await client.PostAsync("/api/books", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = (await response.Content.ReadFromJsonAsync<BookResponse>())!;
        Assert.Equal("Örnek kitap", book.Name);
        Assert.Equal("Pending", book.Status);
        Assert.Equal(Enumerable.Range(1, 10), book.Papers.Select(paper => paper.SortOrder));
        Assert.All(book.Papers, paper => Assert.Equal("duplicate.docx", paper.FileName));
        Assert.Equal(10, book.Papers.Select(paper => paper.Id).Distinct().Count());

        using var getResponse = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var retrieved = (await getResponse.Content.ReadFromJsonAsync<BookResponse>())!;
        Assert.Equal(book.Id, retrieved.Id);
        Assert.Equal(book.Papers.ToArray(), retrieved.Papers.ToArray());
        Assert.DoesNotContain("storedFilePath", await getResponse.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await database.Books.CountAsync());
        var papers = await database.Papers.OrderBy(paper => paper.SortOrder).ToListAsync();
        Assert.Equal(10, papers.Count);
        foreach (var paper in papers)
        {
            Assert.StartsWith($"books/{book.Id:N}/", paper.StoredFilePath);
            var file = Path.Combine(factory.StorageDirectory, "Storage", paper.StoredFilePath);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(file));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(11)]
    public async Task WrongFileCount_Returns400WithoutSaving(int count)
    {
        await using var factory = new UploadApiFactory();
        using var client = factory.CreateClient();
        using var form = CreateForm(count: count);
        using var response = await client.PostAsync("/api/books", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNothingSaved(factory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task MissingName_Returns400(string? name)
    {
        await using var factory = new UploadApiFactory();
        using var client = factory.CreateClient();
        using var form = CreateForm(name: name);
        using var response = await client.PostAsync("/api/books", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNothingSaved(factory);
    }

    [Theory]
    [InlineData("report.pdf", false)]
    [InlineData("fake.docx", true)]
    public async Task InvalidDocument_Returns400WithoutSaving(string fileName, bool corrupt)
    {
        await using var factory = new UploadApiFactory();
        using var client = factory.CreateClient();
        using var form = CreateForm(fileName: fileName, bytes: corrupt ? [1, 2, 3] : CreateDocx());
        using var response = await client.PostAsync("/api/books", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors", await response.Content.ReadAsStringAsync());
        await AssertNothingSaved(factory);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PersistenceFailure_CleansFiles_AndDoesNotExposeDetails(bool failDatabase, bool failStorage)
    {
        await using var factory = new UploadApiFactory(failDatabase, failStorage);
        using var client = factory.CreateClient();
        using var form = CreateForm();
        using var response = await client.PostAsync("/api/books", form);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("confidential", await response.Content.ReadAsStringAsync());
        await AssertNothingSaved(factory);
    }

    [Fact]
    public async Task UnknownBook_Returns404()
    {
        await using var factory = new UploadApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/books/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EmptyOrOversizedFile_IsRejected()
    {
        var validator = new DocxUploadValidator();
        foreach (var length in new[] { 0L, UploadLimits.MaxFileBytes + 1 })
        {
            using var stream = new MemoryStream();
            var file = new FormFile(stream, 0, length, "Files", "test.docx");
            await Assert.ThrowsAsync<UploadValidationException>(() => validator.ValidateFileAsync(file, default));
        }
    }

    [Fact]
    public async Task InvalidLastFile_DoesNotLeaveEarlierFilesBehind()
    {
        await using var factory = new UploadApiFactory();
        using var client = factory.CreateClient();
        using var form = CreateForm(count: 9);
        form.Add(new ByteArrayContent([1, 2, 3]), "Files", "last.docx");
        using var response = await client.PostAsync("/api/books", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNothingSaved(factory);
    }

    [Fact]
    public async Task OversizedMultipartFile_Returns400WithoutSaving()
    {
        await using var factory = new UploadApiFactory();
        using var client = factory.CreateClient();
        using var form = CreateForm(count: 9);
        form.Add(new ByteArrayContent(new byte[UploadLimits.MaxFileBytes + 1]), "Files", "large.docx");
        using var response = await client.PostAsync("/api/books", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNothingSaved(factory);
    }

    [Fact]
    public async Task OverlongBookName_Returns400WithoutSaving()
    {
        await using var factory = new UploadApiFactory();
        using var client = factory.CreateClient();
        using var form = CreateForm(name: new string('a', 201));
        using var response = await client.PostAsync("/api/books", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNothingSaved(factory);
    }

    [Fact]
    public async Task XmlWithDtd_IsRejected()
    {
        var bytes = CreateDocx("<!DOCTYPE document [<!ENTITY x 'test'>]><document xmlns='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><body>&x;</body></document>");
        using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, stream.Length, "Files", "test.docx");
        await Assert.ThrowsAsync<UploadValidationException>(() => new DocxUploadValidator().ValidateFileAsync(file, default));
    }

    private static async Task AssertNothingSaved(UploadApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await database.Books.CountAsync());
        Assert.Equal(0, await database.Papers.CountAsync());
        Assert.Empty(Directory.GetFiles(factory.StorageDirectory, "*.docx", SearchOption.AllDirectories));
    }

    private static MultipartFormDataContent CreateForm(int count = 10, string? name = " Örnek kitap ",
        string? fileName = null, byte[]? bytes = null)
    {
        var form = new MultipartFormDataContent();
        if (name is not null)
            form.Add(new StringContent(name), "Name");
        for (var index = 1; index <= count; index++)
            form.Add(new ByteArrayContent(bytes ?? CreateDocx()), "Files", fileName ?? $"{index:D2}.docx");
        return form;
    }

    private static byte[] CreateDocx(string? document = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("_rels/.rels").Open()))
                writer.Write("<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument' Target='word/document.xml'/></Relationships>");
            using (var writer = new StreamWriter(archive.CreateEntry("[Content_Types].xml").Open()))
                writer.Write("<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Override PartName='/word/document.xml' ContentType='application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml'/></Types>");
            using (var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open()))
                writer.Write(document ?? "<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>Örnek bildiri elif@example.org 0500 000 00 01</w:t></w:r></w:p></w:body></w:document>");
        }
        return stream.ToArray();
    }
}
