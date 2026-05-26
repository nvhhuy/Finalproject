using Api.Authorization;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Api.Controllers.Base;

namespace Api.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/admin/users")]
public sealed partial class AdminUsersController : ApiControllerBase
{
    public AdminUsersController(ISender sender) : base(sender)
    {
    }
}
