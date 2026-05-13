using Application.Modules.Users.Authentication.Common;
using Shared.Errors;

namespace Application.Modules.Users.Authentication.Commands.RefreshSession;

public sealed record RefreshSessionCommand(string RefreshToken, string? RemoteIpAddress = null)
    : IRequest<Result<AuthenticationResponse, Error>>;
