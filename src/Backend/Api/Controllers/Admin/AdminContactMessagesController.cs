using Api.Authorization;
using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Application.Modules.Contact;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/admin/contact-messages")]
public sealed class AdminContactMessagesController : ApiControllerBase
{
    public AdminContactMessagesController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ContactMessageListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new AdminListContactMessagesQuery(page, pageSize, search, isRead),
            cancellationToken);
        return MatchApi(result, OkApi);
    }

    [HttpPost("{messageId:guid}/mark-read")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid messageId, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new AdminMarkContactMessageReadCommand(messageId),
            cancellationToken);
        return MatchApiEmpty(result);
    }
}
