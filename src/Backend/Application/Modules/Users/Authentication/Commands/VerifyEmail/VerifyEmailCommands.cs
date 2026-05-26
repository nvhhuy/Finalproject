using Application.Contracts.Caching;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Transactions;
using Application.Modules.Users.Authentication;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Security;

namespace Application.Modules.Users.Authentication.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(string TokenId, string Token) : IRequest<Result<bool, Error>>;

public sealed record EmailVerificationTokenPayload(Guid UserId, string TokenHash);

public sealed class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, Result<bool, Error>>
{
    private readonly IUserWriteRepository _users;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _uow;

    public VerifyEmailCommandHandler(IUserWriteRepository users, ICacheService cache, IUnitOfWork uow)
    {
        _users = users;
        _cache = cache;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TokenId) || string.IsNullOrWhiteSpace(request.Token))
            return new Error("BadRequest", 400, "Token id and token are required.");

        string cacheKey = AuthCacheKeys.EmailVerificationPrefix + request.TokenId;
        EmailVerificationTokenPayload? payload = await _cache.GetAsync<EmailVerificationTokenPayload>(cacheKey, cancellationToken);
        if (payload is null)
            return new Error("InvalidOrExpiredVerificationToken", 400, "Verification link is invalid or expired.");

        string hash = TokenHasher.Sha256(request.Token);
        if (!string.Equals(payload.TokenHash, hash, StringComparison.Ordinal))
            return new Error("InvalidOrExpiredVerificationToken", 400, "Verification link is invalid or expired.");

        User? user = await _users.GetByIdForUpdateAsync(payload.UserId, cancellationToken);
        if (user is null)
            return new Error("InvalidVerificationTarget", 400, "User is no longer available.");

        user.MarkEmailVerified(DateTime.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(user.Id.Value, user.Email, user.UserName, cancellationToken);
        await _cache.RemoveAsync(cacheKey, cancellationToken);
        return true;
    }
}
