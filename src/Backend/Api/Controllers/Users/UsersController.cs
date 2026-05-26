using Api.Authorization;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Api.Controllers.Base;

namespace Api.Controllers.Users;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize]
[Route("api/v{version:apiVersion}/users")]
public sealed partial class UsersController : ApiControllerBase
{
    public UsersController(ISender sender) : base(sender)
    {
    }
}
