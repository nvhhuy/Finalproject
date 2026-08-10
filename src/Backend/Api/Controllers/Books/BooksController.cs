using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Api.Models.Books;
using Application.Modules.Books.Commands;
using Application.Modules.Books.Dtos;
using Application.Modules.Books.Queries;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Books;

/// <summary>
///     CRUD surface for a user's personal book catalog. Every route requires an authenticated
///     account and every read/write is scoped to <c>CurrentUserId</c> — books are private per owner.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class BooksController : ApiControllerBase
{
    public BooksController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<BookListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListMyBooksQuery(CurrentUserId, page, pageSize, search),
            cancellationToken);
        return MatchApi(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BookDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetBookQuery(id, CurrentUserId), cancellationToken);
        return MatchApi(result);
    }

    [HttpPost]
    [Transactional]
    [RequiresIdempotencyKey(Scope = "book.create")]
    [ProducesResponseType(typeof(ApiResponse<BookDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBookRequest body, CancellationToken cancellationToken)
    {
        var command = new CreateBookCommand(CurrentUserId, body.Title, body.Author, body.Isbn, body.Description,
            body.PublishedYear);
        var result = await Sender.Send(command, cancellationToken);
        return MatchApi(result);
    }

    [HttpPut("{id:guid}")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<BookDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateBookRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateBookCommand(id, CurrentUserId, body.Title, body.Author, body.Isbn, body.Description,
            body.PublishedYear, body.RowVersion);
        var result = await Sender.Send(command, cancellationToken);
        return MatchApi(result);
    }

    [HttpDelete("{id:guid}")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        [FromQuery] uint rowVersion,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteBookCommand(id, CurrentUserId, rowVersion), cancellationToken);
        return MatchApiEmpty(result);
    }
}
