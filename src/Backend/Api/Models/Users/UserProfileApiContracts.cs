namespace Api.Models.Users;

/// <summary>Authenticated user profile update.</summary>
public sealed record UpdateProfileRequest(string FirstName, string LastName, uint RowVersion);

public sealed record SetAvatarRequest(Guid MediaAssetId, uint RowVersion);