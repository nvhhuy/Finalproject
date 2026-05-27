using Api.Controllers.Base;
using Api.Filters;
using Api.Models;
using Api.Models.Media;
using Application.Modules.Media.Commands;
using Application.Modules.Media.Dtos;
using Application.Modules.Media.Queries;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Media;

/// <summary>
/// HTTP surface for the upload-then-process flow:
/// <list type="number">
///   <item><description>POST <c>uploads</c> reserves an asset row and returns a presigned PUT URL.</description></item>
///   <item><description>Client PUTs bytes directly to the bucket using that URL.</description></item>
///   <item><description>POST <c>{id}/complete</c> confirms the upload; backend raises the integration event consumed by the media worker.</description></item>
///   <item><description>GET <c>{id}</c> returns the asset + variants (CDN-resolved URLs) so the client can render once <c>Status = Ready</c>.</description></item>
/// </list>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class MediaController : ApiControllerBase
{
    public MediaController(ISender sender) : base(sender) { }

    [HttpPost("uploads")]
    [Transactional]
    [EnableRateLimiting("media-upload")]
    [ProducesResponseType(typeof(ApiResponse<InitiateMediaUploadResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Initiate([FromBody] InitiateMediaUploadRequest body, CancellationToken cancellationToken)
    {
        var command = new InitiateMediaUploadCommand(CurrentUserId, body.ContentType, body.ByteSize, body.FileExtension);
        Result<InitiateMediaUploadResult, Error> result = await Sender.Send(command, cancellationToken);
        return MatchApi(result);
    }

    [HttpPost("{id:guid}/complete")]
    [RequiresIdempotencyKey(Scope = "media.complete")]
    [Transactional]
    [EnableRateLimiting("media-upload")]
    [ProducesResponseType(typeof(ApiResponse<MediaAssetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Complete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        Result<MediaAssetDto, Error> result = await Sender.Send(new CompleteMediaUploadCommand(id, CurrentUserId), cancellationToken);
        return MatchApi(result);
    }

    [HttpGet("{id:guid}")]
    [EnableRateLimiting("media-upload")]
    [ProducesResponseType(typeof(ApiResponse<MediaAssetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        Result<MediaAssetDto, Error> result = await Sender.Send(new GetMediaAssetQuery(id, CurrentUserId), cancellationToken);
        return MatchApi(result);
    }

    [HttpDelete("{id:guid}")]
    [Transactional]
    [EnableRateLimiting("media-upload")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        [FromQuery] uint rowVersion,
        CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(
            new DeleteMediaAssetCommand(id, CurrentUserId, rowVersion),
            cancellationToken);
        return MatchApiEmpty(result);
    }
}
