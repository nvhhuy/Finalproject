using Api.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Api.Controllers.Base;

namespace Api.Controllers.Admin;

[ApiController]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/admin/users")]
public sealed partial class AdminUsersController : ApiControllerBase
{
    public AdminUsersController(ISender sender) : base(sender)
    {
    }
}
