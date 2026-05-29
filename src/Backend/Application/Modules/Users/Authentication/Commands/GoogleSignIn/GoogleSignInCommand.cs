using Application.Modules.Users.Authentication.Common;

namespace Application.Modules.Users.Authentication.Commands.GoogleSignIn;

public sealed record GoogleSignInCommand(string IdToken, string? RemoteIpAddress = null)
    : IRequest<Result<AuthenticationResponse, Error>>;