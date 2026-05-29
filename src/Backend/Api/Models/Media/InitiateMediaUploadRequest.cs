namespace Api.Models.Media;

/// <summary>Request body for <c>POST /media/uploads</c>.</summary>
public sealed record InitiateMediaUploadRequest(string ContentType, long ByteSize, string? FileExtension);