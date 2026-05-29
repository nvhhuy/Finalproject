namespace Domains.Tenancy;

public static class TenantMemberRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";

    public static bool IsValid(string? role)
    {
        return role is Owner or Admin or Member;
    }
}