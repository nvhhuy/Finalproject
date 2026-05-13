using Api.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Api.Controllers.Base;

namespace Api.Controllers.Users;

[ApiController]
[JwtAuthorize]
[Authorize]
[Route("api/users")]
public sealed partial class UsersController : ApiControllerBase
{
    public UsersController(ISender sender) : base(sender)
    {
    }
}
