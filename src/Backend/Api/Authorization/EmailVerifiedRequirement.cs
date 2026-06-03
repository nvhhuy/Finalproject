using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

/// <summary>Requires a signed-in user with a verified email (admins and impersonation sessions are exempt).</summary>
public sealed class EmailVerifiedRequirement : IAuthorizationRequirement;
