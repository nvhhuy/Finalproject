using Application.Contracts.Transactions;
using Api.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Api.Controllers.Base;

namespace Api.Controllers.Samples;

[ApiController]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/samples")]
public sealed partial class SamplesController : ApiControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public SamplesController(ISender sender, IUnitOfWork unitOfWork) : base(sender) =>
        _unitOfWork = unitOfWork;
}
