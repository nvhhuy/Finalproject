using Domains.UserAggregate;

namespace Application.Contracts.Services;

public interface IJwtGenerator
{
    (string AccessToken, DateTime ExpiresUtc) CreateAccessToken(User user, Guid? impersonatorUserId = null);
}