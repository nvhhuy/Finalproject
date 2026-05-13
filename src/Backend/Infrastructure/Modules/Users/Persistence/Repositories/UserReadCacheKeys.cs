namespace Infrastructure.Modules.Users.Persistence.Repositories;

internal static class UserReadCacheKeys
{
    public static string ById(Guid id) => $"read:User:id:{id}";
    public static string ByEmail(string email) => $"read:User:email:{email.ToLowerInvariant()}";
    public static string ByUserName(string userName) => $"read:User:username:{userName.ToLowerInvariant()}";
}
