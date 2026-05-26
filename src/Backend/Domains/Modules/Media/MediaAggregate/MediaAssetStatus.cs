namespace Domains.MediaAggregate;

/// <summary>
/// Lifecycle of an uploaded asset:
/// <list type="number">
///   <item><description><see cref="Pending"/> — DB row exists, presigned URL handed out, original not yet uploaded.</description></item>
///   <item><description><see cref="Uploaded"/> — client confirmed upload; processing queued.</description></item>
///   <item><description><see cref="Processing"/> — worker actively producing variants.</description></item>
///   <item><description><see cref="Ready"/> — at least one consumable variant exists; safe to serve.</description></item>
///   <item><description><see cref="Failed"/> — processing exhausted retries; carries an error message.</description></item>
/// </list>
/// </summary>
public enum MediaAssetStatus
{
    Pending = 1,
    Uploaded = 2,
    Processing = 3,
    Ready = 4,
    Failed = 5,
}
