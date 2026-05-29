using Api.Tenancy;
using Application.Contracts.Tenancy;
using Application.Contracts.Services;
using Domains.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Api.Middleware;

/// <summary>
/// Resolves tenant context from <c>X-Tenant-Id</c>, JWT <c>tenant_id</c> claim, or request host (subdomain / custom domain).
/// Validates membership for authenticated users unless platform admin bypass applies.
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantMembershipService membership,
        ITenantHostResolver hostResolver,
        ILoggedInUserService loggedInUser)
    {
        if (TryResolveTenantId(context, out Guid tenantId))
        {
            if (!await AuthorizeTenantAccessAsync(context, tenantContext, membership, loggedInUser, tenantId))
                return;

            tenantContext.SetTenant(tenantId);
        }
        else
        {
            string host = context.Request.Host.Host ?? string.Empty;
            TenantHostResolution? hostResolution = await hostResolver.ResolveAsync(host, context.RequestAborted);
            if (hostResolution is not null)
            {
                tenantContext.SetTenant(hostResolution.TenantId);
                context.Items[TenantHttpItems.ResolvedSlug] = hostResolution.Slug;
                context.Items[TenantHttpItems.ResolutionSource] = hostResolution.Source.ToString();
            }
            else if (loggedInUser.UserId is { } autoUserKey)
            {
                Guid userId = autoUserKey.Value;
                if (await membership.IsPlatformAdminAsync(userId, context.RequestAborted))
                {
                    tenantContext.EnableBypass();
                }
                else
                {
                    IReadOnlyList<Guid> memberships =
                        await membership.GetTenantIdsForUserAsync(userId, context.RequestAborted);
                    if (memberships.Count > 0)
                    {
                        Guid activeTenant = memberships.Contains(SystemTenants.DefaultTenantId)
                            ? SystemTenants.DefaultTenantId
                            : memberships[0];
                        tenantContext.SetTenant(activeTenant);
                    }
                }
            }
        }

        await _next(context);
    }

    private static async Task<bool> AuthorizeTenantAccessAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantMembershipService membership,
        ILoggedInUserService loggedInUser,
        Guid tenantId)
    {
        if (loggedInUser.UserId is not { } userKey)
            return true;

        Guid userId = userKey.Value;
        bool isPlatformAdmin = await membership.IsPlatformAdminAsync(userId, context.RequestAborted);
        if (isPlatformAdmin)
            tenantContext.EnableBypass();

        bool isMember = isPlatformAdmin || await membership.IsMemberAsync(tenantId, userId, context.RequestAborted);
        if (isMember)
            return true;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "You are not a member of the requested tenant.",
        });
        return false;
    }

    private static bool TryResolveTenantId(HttpContext context, out Guid tenantId)
    {
        tenantId = Guid.Empty;

        if (context.Request.Headers.TryGetValue(TenantHttpHeaders.TenantId, out var headerValues)
            && Guid.TryParse(headerValues.FirstOrDefault(), out Guid fromHeader)
            && fromHeader != Guid.Empty)
        {
            tenantId = fromHeader;
            return true;
        }

        string? claim = context.User.FindFirst(TenantClaimTypes.TenantId)?.Value;
        if (!string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out Guid fromClaim) && fromClaim != Guid.Empty)
        {
            tenantId = fromClaim;
            return true;
        }

        return false;
    }
}
