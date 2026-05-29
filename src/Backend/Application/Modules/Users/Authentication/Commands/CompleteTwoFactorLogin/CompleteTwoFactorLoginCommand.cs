using Application.Modules.Users.Authentication.Common;

namespace Application.Modules.Users.Authentication.Commands.CompleteTwoFactorLogin;

public sealed record CompleteTwoFactorLoginCommand(string PendingLoginId, string Code, string? RemoteIpAddress = null)
    : IRequest<Result<AuthenticationResponse, Error>>;