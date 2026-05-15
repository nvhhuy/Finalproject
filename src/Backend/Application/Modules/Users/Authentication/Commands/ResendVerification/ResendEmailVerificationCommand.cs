using Application.Contracts.Email;
using Application.Contracts.Modules.Users.Persistence.Read;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;

namespace Application.Modules.Users.Authentication.Commands.ResendVerification;

public sealed record ResendEmailVerificationCommand(string Email) : IRequest<Result<bool, Error>>;

/// <summary>Resends verification email if the account exists and is not yet verified (always returns success to limit enumeration).</summary>
public sealed class ResendEmailVerificationCommandHandler : IRequestHandler<ResendEmailVerificationCommand, Result<bool, Error>>
{
    private readonly IUserReadRepository _users;
    private readonly IEmailVerificationSender _emailVerification;

    public ResendEmailVerificationCommandHandler(IUserReadRepository users, IEmailVerificationSender emailVerification)
    {
        _users = users;
        _emailVerification = emailVerification;
    }

    public async Task<Result<bool, Error>> Handle(ResendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return true;

        User? user = await _users.GetByEmailAsync(request.Email.Trim(), cancellationToken: cancellationToken);
        if (user is null || user.EmailVerifiedAtUtc is not null)
            return true;

        await _emailVerification.SendVerificationEmailAsync(user.Id.Value, user.Email, cancellationToken);
        return true;
    }
}
