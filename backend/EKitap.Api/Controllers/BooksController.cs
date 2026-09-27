using EKitap.Api.Contracts;
using EKitap.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EKitap.Api.Controllers;

[ApiController]
[Route("api/books")]
public sealed class BooksController(BookService bookService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimits.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.MaxFileBytes)]
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
}
