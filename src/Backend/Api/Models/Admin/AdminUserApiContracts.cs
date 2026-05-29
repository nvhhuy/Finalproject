namespace Api.Models.Admin;

/// <summary>Admin lock user command body.</summary>
public sealed record LockUserRequest(string? Reason, uint RowVersion);

public sealed record AdminCreateUserRequest(
    string FirstName,
    string LastName,
    string UserName,
    string Email,
    string Password,
    string Role = "User");

public sealed record AdminUpdateUserRequest(
    string FirstName,
    string LastName,
    string UserName,
    string Email,
    string Role,
    uint RowVersion);

public sealed record AdminResetPasswordRequest(string NewPassword, uint RowVersion);