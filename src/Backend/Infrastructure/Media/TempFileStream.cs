namespace Infrastructure.Media;

/// <summary>
///     A <see cref="FileStream" /> wrapper that deletes the underlying temp file on dispose. Lets
///     FFmpeg callers (poster, transcode) return the produced binary as a stream without first
///     reading the entire file into a <see cref="MemoryStream" /> — important for multi-hundred-MB
///     video variants that would otherwise pin large managed buffers per concurrent rung.
/// </summary>
/// <remarks>
///     Wraps the file with <c>FileOptions.DeleteOnClose</c> + a defensive <see cref="File.Delete" />
///     in dispose: <c>DeleteOnClose</c> works on Linux only when the OS supports it, so the explicit
///     delete is the cross-platform safety net. Errors from delete are swallowed — temp files are
///     cleaned by the OS sweeper if our process dies before disposing.
/// </remarks>
internal sealed class TempFileStream : FileStream
{
    private readonly string _path;
    private bool _disposed;

    private TempFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize,
        FileOptions options)
        : base(path, mode, access, share, bufferSize, options)
    {
        _path = path;
    }

    public static TempFileStream OpenRead(string path)
    {
        return new TempFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch
        {
            // Best-effort: if the OS already removed the file (DeleteOnClose) or another process
            // is holding it, the next reboot's temp sweep will clean up.
        }
    }
}