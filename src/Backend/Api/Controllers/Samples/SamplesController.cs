using Application.Contracts.Transactions;
using Api.Authorization;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Api.Controllers.Base;

namespace Api.Controllers.Samples;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/samples")]
public sealed partial class SamplesController : ApiControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public SamplesController(ISender sender, IUnitOfWork unitOfWork) : base(sender) =>
        _unitOfWork = unitOfWork;
}
