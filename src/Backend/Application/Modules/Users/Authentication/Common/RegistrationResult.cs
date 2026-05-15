namespace Application.Modules.Users.Authentication.Common;

/// <summary>Returned when public registration succeeds; tokens are issued only after email verification.</summary>
public sealed record RegistrationResult(string Email, string Message);
