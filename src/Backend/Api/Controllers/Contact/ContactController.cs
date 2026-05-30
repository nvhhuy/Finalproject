using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Application.Modules.Contact;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers.Contact;

[ApiController]
[ApiVersion("1.0")]
[EnableRateLimiting("contact")]
[Route("api/v{version:apiVersion}/contact")]
public sealed class ContactController : ApiControllerBase
{
    public ContactController(ISender sender) : base(sender)
    {
    }

    /// <summary>Submit a public contact form message. No authentication required.</summary>
    [HttpPost]
    [RequiresTurnstile]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new SubmitContactMessageCommand(request.Name, request.Email, request.Subject, request.Body),
            cancellationToken);
        return MatchApi(result, OkApi);
    }

    public sealed record SubmitContactRequest(string Name, string Email, string Subject, string Body);
}
