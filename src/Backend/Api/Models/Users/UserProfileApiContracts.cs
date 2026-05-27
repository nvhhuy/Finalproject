namespace Api.Models.Users;

/// <summary>Authenticated user profile update.</summary>
public sealed record UpdateProfileRequest(string FirstName, string LastName, uint RowVersion);
