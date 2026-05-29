using Api.Filters;
using Api.Models;
using Api.Models.Auth;
using Application.Modules.Users.Authentication.Commands.CompleteTwoFactorLogin;
using Application.Modules.Users.Authentication.Commands.ForgotPassword;
using Application.Modules.Users.Authentication.Commands.GoogleSignIn;
using Application.Modules.Tenancy;
using Application.Modules.Users.Authentication.Commands.RefreshSession;
using Application.Modules.Users.Authentication.Commands.VerifyEmail;
using Application.Modules.Users.Authentication.Commands.Register;
using Application.Modules.Users.Authentication.Commands.ResendVerification;
using Application.Modules.Users.Authentication.Common;
using Application.Modules.Users.Authentication.Queries;
using Application.Modules.Users.Profile.Commands.TwoFactor;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;
using Shared.Errors.General;

using Api.Controllers.Base;

namespace Api.Controllers.Authentication;

[ApiController]
[ApiVersion("1.0")]
[EnableRateLimiting("auth")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class AuthController : ApiControllerBase
{
    public AuthController(ISender sender) : base(sender)
    {
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost("register")]
    [AllowAnonymous]
    [RequiresIdempotencyKey(Scope = "auth.register")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<RegistrationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
    {
        RegisterCommand withIp = command with { RemoteIpAddress = ClientIp };
        Result<RegistrationResult, Error> result = await Sender.Send(withIp, cancellationToken);
        return MatchApi(result, r => OkApi(RegistrationResponseDto.From(r)));
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendEmailVerificationRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new ResendEmailVerificationCommand(body.Email), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TwoFactorChallengeDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginQuery query, CancellationToken cancellationToken)
    {
        LoginQuery withIp = query with { RemoteIpAddress = ClientIp };
        Result<AuthenticationResponse, Error> result = await Sender.Send(withIp, cancellationToken);
        return MapAuthResult(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh([FromBody] RefreshSessionRequest body, CancellationToken cancellationToken)
    {
        var command = new RefreshSessionCommand(body.RefreshToken, ClientIp);
        Result<AuthenticationResponse, Error> result = await Sender.Send(command, cancellationToken);
        return MapAuthResult(result);
    }

    [HttpPost("google")]
    [AllowAnonymous]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Google([FromBody] GoogleSignInRequest body, CancellationToken cancellationToken)
    {
        var command = new GoogleSignInCommand(body.IdToken, ClientIp);
        Result<AuthenticationResponse, Error> result = await Sender.Send(command, cancellationToken);
        return MapAuthResult(result);
    }

    [HttpPost("tenant-sso/{tenantSlug}")]
    [AllowAnonymous]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TenantSso(string tenantSlug, [FromBody] TenantOidcSignInRequest body, CancellationToken cancellationToken)
    {
        Result<AuthenticationResponse, Error> result = await Sender.Send(
            new TenantOidcSignInCommand(tenantSlug, body.IdToken, ClientIp),
            cancellationToken);
        return MapAuthResult(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new ForgotPasswordCommand(body.Email), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result =
            await Sender.Send(new ResetPasswordCommand(body.TokenId, body.Token, body.NewPassword), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("verify-email")]
    [Transactional]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new VerifyEmailCommand(body.TokenId, body.Token), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("login/2fa")]
    [AllowAnonymous]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteTwoFactorLogin([FromBody] CompleteTwoFactorLoginRequest body, CancellationToken cancellationToken)
    {
        var command = new CompleteTwoFactorLoginCommand(body.PendingLoginId, body.Code, ClientIp);
        Result<AuthenticationResponse, Error> result = await Sender.Send(command, cancellationToken);
        return MapAuthResult(result);
    }

    [HttpPost("2fa/begin")]
    [Authorize]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<TwoFactorSetupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BeginTwoFactor(CancellationToken cancellationToken)
    {
        // The shared key + otpauth URI in the response are credential material — refuse to let
        // any proxy / CDN cache them. `no-store` is the strongest directive (no disk, no memory)
        // and `Pragma: no-cache` covers stubborn HTTP/1.0 intermediaries.
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Pragma = "no-cache";

        Result<TwoFactorSetupResponse, Error> result = await Sender.Send(new BeginTwoFactorSetupCommand(CurrentUserId), cancellationToken);
        return MatchApi(result);
    }

    [HttpPost("2fa/confirm")]
    [Authorize]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmTwoFactor([FromBody] ConfirmTwoFactorRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new ConfirmTwoFactorSetupCommand(CurrentUserId, body.Code), cancellationToken);
        return MatchApiEmpty(result);
    }

    private IActionResult MapAuthResult(Result<AuthenticationResponse, Error> result)
    {
        if (result.IsFailure)
        {
            if (result.Error is ValidationError validationError)
                return BadRequestValidationApi(validationError);

            if (result.Error is TwoFactorRequiredError twoFa)
            {
                var challenge = new TwoFactorChallengeDto(twoFa.PendingLoginId, twoFa.ErrorMessage);
                return Accepted(string.Empty, ApiResponse<TwoFactorChallengeDto>.Ok(challenge, HttpContext));
            }

            return FailApi(result.Error);
        }

        return OkApi(AuthResponseDto.From(result.Value));
    }
}
