using Api.Authorization;
using Api.Controllers.Base;
using Api.Models;
using Api.Models.Tenancy;
using Application.Modules.Tenancy;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;

namespace Api.Controllers.Tenancy;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/tenants")]
public sealed class PublicTenantsController : ApiControllerBase
{
    public PublicTenantsController(ISender sender) : base(sender) { }

    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ApiResponse<TenantPublicResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken = default)
    {
        Result<TenantPublicDto, Error> result = await Sender.Send(new GetTenantBySlugQuery(slug), cancellationToken);
        return MatchApi(result, dto => OkApi(TenantApiMapping.ToResponse(dto)));
    }

    [HttpGet("resolve-host")]
    [ProducesResponseType(typeof(ApiResponse<TenantHostResolutionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveHost([FromQuery] string host, CancellationToken cancellationToken = default)
    {
        Result<TenantHostResolutionDto, Error> result =
            await Sender.Send(new ResolveTenantByHostQuery(host), cancellationToken);
        return MatchApi(result, dto => OkApi(TenantApiMapping.ToResponse(dto)));
    }

    [HttpGet("{slug}/sso")]
    [ProducesResponseType(typeof(ApiResponse<TenantSsoPublicResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSsoBySlug(string slug, CancellationToken cancellationToken = default)
    {
        Result<TenantSsoPublicDto, Error> result =
            await Sender.Send(new GetPublicTenantSsoBySlugQuery(slug), cancellationToken);
        return MatchApi(result, dto => OkApi(TenantApiMapping.ToResponse(dto)));
    }
}

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Route("api/v{version:apiVersion}/tenants")]
public sealed class TenantsController : ApiControllerBase
{
    public TenantsController(ISender sender) : base(sender) { }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TenantSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<TenantSummaryDto>, Error> result =
            await Sender.Send(new GetMyTenantsQuery(CurrentUserId), cancellationToken);
        return MatchApi(result, rows => OkApi(rows.Select(TenantApiMapping.ToResponse).ToList()));
    }

    [HttpPost("{tenantId:guid}/members")]
    [ProducesResponseType(typeof(ApiResponse<TenantMemberResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> InviteMember(
        Guid tenantId,
        [FromBody] InviteTenantMemberRequest request,
        CancellationToken cancellationToken)
    {
        Result<TenantMemberSummaryDto, Error> result = await Sender.Send(
            new InviteTenantMemberCommand(tenantId, CurrentUserId, request.Email, request.Role),
            cancellationToken);
        return MatchApi(
            result,
            dto => OkApi(new TenantMemberResponse(dto.Id, dto.TenantId, dto.UserId, dto.Role, dto.JoinedAtUtc)));
    }

    [HttpGet("{tenantId:guid}/domains")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TenantDomainResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDomains(Guid tenantId, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<TenantDomainDto>, Error> result =
            await Sender.Send(new ListTenantDomainsQuery(tenantId, CurrentUserId), cancellationToken);
        return MatchApi(result, rows => OkApi(rows.Select(TenantApiMapping.ToResponse).ToList()));
    }

    [HttpPost("{tenantId:guid}/domains")]
    [ProducesResponseType(typeof(ApiResponse<TenantDomainResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddDomain(
        Guid tenantId,
        [FromBody] AddTenantDomainRequest request,
        CancellationToken cancellationToken)
    {
        Result<TenantDomainDto, Error> result = await Sender.Send(
            new AddTenantDomainCommand(tenantId, CurrentUserId, request.Hostname),
            cancellationToken);
        return MatchApi(result, dto => OkApi(TenantApiMapping.ToResponse(dto)));
    }

    [HttpPost("{tenantId:guid}/domains/{domainId:guid}/verify")]
    [ProducesResponseType(typeof(ApiResponse<TenantDomainResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyDomain(Guid tenantId, Guid domainId, CancellationToken cancellationToken)
    {
        Result<TenantDomainDto, Error> result = await Sender.Send(
            new VerifyTenantDomainCommand(tenantId, CurrentUserId, domainId),
            cancellationToken);
        return MatchApi(result, dto => OkApi(TenantApiMapping.ToResponse(dto)));
    }

    [HttpDelete("{tenantId:guid}/domains/{domainId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveDomain(Guid tenantId, Guid domainId, CancellationToken cancellationToken)
    {
        Result<Unit, Error> result = await Sender.Send(
            new RemoveTenantDomainCommand(tenantId, CurrentUserId, domainId),
            cancellationToken);
        return MatchApi(result, _ => OkApi(true));
    }

    [HttpGet("{tenantId:guid}/sso")]
    [ProducesResponseType(typeof(ApiResponse<TenantSsoSettingsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSso(Guid tenantId, CancellationToken cancellationToken)
    {
        Result<TenantSsoSettingsDto, Error> result =
            await Sender.Send(new GetTenantSsoSettingsQuery(tenantId, CurrentUserId), cancellationToken);
        return MatchApi(result, dto => OkApi(TenantApiMapping.ToResponse(dto)));
    }

    [HttpPut("{tenantId:guid}/sso")]
    [ProducesResponseType(typeof(ApiResponse<TenantSsoSettingsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertSso(
        Guid tenantId,
        [FromBody] UpsertTenantSsoSettingsRequest request,
        CancellationToken cancellationToken)
    {
        Result<TenantSsoSettingsDto, Error> result = await Sender.Send(
            new UpsertTenantSsoSettingsCommand(
                tenantId,
                CurrentUserId,
                request.IsEnabled,
                request.OidcAuthority,
                request.OidcClientId,
                request.OidcClientSecret),
            cancellationToken);
        return MatchApi(result, dto => OkApi(TenantApiMapping.ToResponse(dto)));
    }
}
