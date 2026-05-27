namespace Api.Models.Admin;

/// <summary>Body for admin POST actions that only need an optimistic concurrency token.</summary>
public sealed record AdminRowVersionRequest(uint RowVersion);
