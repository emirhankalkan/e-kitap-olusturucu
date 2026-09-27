using EKitap.Api.Contracts;
using EKitap.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EKitap.Api.Controllers;

[ApiController]
[Route("api/books")]
public sealed class BooksController(
    BookService bookService,
    BookGenerationService generationService,
    BookPdfService pdfService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimits.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.MaxRequestBytes)]
    [ProducesResponseType<BookResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<BookResponse>> Create(
        [FromForm] CreateBookRequest request, CancellationToken cancellationToken)
    {
        var book = await bookService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = book.Id }, book);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var book = await bookService.GetAsync(id, cancellationToken);
        return book is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Kitap bulunamadı.")
            : Ok(book);
    }

    [HttpPost("{id:guid}/generate")]
    [ProducesResponseType<BookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BookResponse>> Generate(Guid id, CancellationToken cancellationToken)
        => Ok(await generationService.GenerateAsync(id, cancellationToken));

    [HttpGet("{id:guid}/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> ViewPdf(Guid id, CancellationToken cancellationToken)
        => ServePdf(id, download: false, cancellationToken);

    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
        => ServePdf(id, download: true, cancellationToken);

    private async Task<IActionResult> ServePdf(Guid id, bool download, CancellationToken cancellationToken)
    {
        var stream = await pdfService.OpenAsync(id, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        if (!download)
            Response.Headers.ContentDisposition = "inline";
        return File(stream, "application/pdf", download ? $"kitap-{id:N}.pdf" : null, enableRangeProcessing: true);
    }
}
