using Api.Authorization;
using Api.Controllers.Base;
using Application.Contracts.Transactions;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Samples;

[ApiController]
[ApiVersion("1.0")]
[JwtAuthorize]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v{version:apiVersion}/samples")]
public sealed partial class SamplesController : ApiControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public SamplesController(ISender sender, IUnitOfWork unitOfWork) : base(sender)
    {
        _unitOfWork = unitOfWork;
    }
}