using Api.Mapping;
using Application.Modules.Users.Authentication.Common;

namespace Api.Models.Auth;

public sealed record RefreshSessionRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string TokenId, string Token, string NewPassword);

public sealed record VerifyEmailRequest(string TokenId, string Token);

public sealed record ResendEmailVerificationRequest(string Email);

public sealed record RegistrationResponseDto(string Email, string Message)
{
    public static RegistrationResponseDto From(Application.Modules.Users.Authentication.Common.RegistrationResult result) =>
        new(result.Email, result.Message);
}

public sealed record GoogleSignInRequest(string IdToken);

public sealed record CompleteTwoFactorLoginRequest(string PendingLoginId, string Code);

public sealed record ConfirmTwoFactorRequest(string Code);

/// <summary>Returned when login succeeds with tokens.</summary>
public sealed record AuthResponseDto(
    Guid UserId,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string AccessToken,
    DateTime AccessTokenExpiresUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresUtc)
{
    public static AuthResponseDto From(AuthenticationResponse response) => response.ToAuthResponseDto();
}

/// <summary>Returned with HTTP 202 when the user must complete two-factor authentication.</summary>
public sealed record TwoFactorChallengeDto(string PendingLoginId, string? Message);
