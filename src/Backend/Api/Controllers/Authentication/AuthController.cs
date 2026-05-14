using Api.Filters;
using Api.Models;
using Api.Models.Auth;
using Application.Modules.Users.Authentication.Commands.CompleteTwoFactorLogin;
using Application.Modules.Users.Authentication.Commands.ForgotPassword;
using Application.Modules.Users.Authentication.Commands.GoogleSignIn;
using Application.Modules.Users.Authentication.Commands.RefreshSession;
using Application.Modules.Users.Authentication.Commands.VerifyEmail;
using Application.Modules.Users.Authentication.Commands.Register;
using Application.Modules.Users.Authentication.Common;
using Application.Modules.Users.Authentication.Queries;
using Application.Modules.Users.Profile.Commands.TwoFactor;
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
[EnableRateLimiting("auth")]
[Route("api/[controller]")]
public sealed class AuthController : ApiControllerBase
{
    public AuthController(ISender sender) : base(sender)
    {
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost("register")]
    [Transactional]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
    {
        RegisterCommand withIp = command with { RemoteIpAddress = ClientIp };
        Result<AuthenticationResponse, Error> result = await Sender.Send(withIp, cancellationToken);
        return MapAuthResult(result);
    }

    [HttpPost("login")]
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
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh([FromBody] RefreshSessionRequest body, CancellationToken cancellationToken)
    {
        var command = new RefreshSessionCommand(body.RefreshToken, ClientIp);
        Result<AuthenticationResponse, Error> result = await Sender.Send(command, cancellationToken);
        return MapAuthResult(result);
    }

    [HttpPost("google")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Google([FromBody] GoogleSignInRequest body, CancellationToken cancellationToken)
    {
        var command = new GoogleSignInCommand(body.IdToken, ClientIp);
        Result<AuthenticationResponse, Error> result = await Sender.Send(command, cancellationToken);
        return MapAuthResult(result);
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new ForgotPasswordCommand(body.Email), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result =
            await Sender.Send(new ResetPasswordCommand(body.TokenId, body.Token, body.NewPassword), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest body, CancellationToken cancellationToken)
    {
        Result<bool, Error> result = await Sender.Send(new VerifyEmailCommand(body.TokenId, body.Token), cancellationToken);
        return MatchApiEmpty(result);
    }

    [HttpPost("login/2fa")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteTwoFactorLogin([FromBody] CompleteTwoFactorLoginRequest body, CancellationToken cancellationToken)
    {
        var command = new CompleteTwoFactorLoginCommand(body.PendingLoginId, body.Code, ClientIp);
        Result<AuthenticationResponse, Error> result = await Sender.Send(command, cancellationToken);
        return MapAuthResult(result);
    }

    [HttpPost("2fa/begin")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TwoFactorSetupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BeginTwoFactor(CancellationToken cancellationToken)
    {
        Result<TwoFactorSetupResponse, Error> result = await Sender.Send(new BeginTwoFactorSetupCommand(CurrentUserId), cancellationToken);
        return MatchApi(result);
    }

    [HttpPost("2fa/confirm")]
    [Authorize]
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
