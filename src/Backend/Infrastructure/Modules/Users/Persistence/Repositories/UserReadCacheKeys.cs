namespace Infrastructure.Modules.Users.Persistence.Repositories;

internal static class UserReadCacheKeys
{
    public static string ById(Guid id)
    {
        return $"read:User:id:{id}";
    }

    public static string ByEmail(string email)
    {
        return $"read:User:email:{email.ToLowerInvariant()}";
    }

    public static string ByUserName(string userName)
    {
        return $"read:User:username:{userName.ToLowerInvariant()}";
    }
}