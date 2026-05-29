namespace Application.Modules.Media.Common;

/// <summary>
///     Lightweight magic-byte detector used to verify that an uploaded blob's actual bytes match the
///     content type the client declared at <c>InitiateMediaUpload</c>. Defends against the common
///     social-platform abuse vector of declaring <c>image/jpeg</c> while uploading an executable,
///     HTML, or polyglot payload that gets served back to other users with the trusted content-type.
/// </summary>
/// <remarks>
///     <para>
///         This is intentionally <b>not exhaustive</b> — it covers the formats our media pipeline
///         will actually decode (JPEG, PNG, WebP, GIF for images; MP4, MOV/QuickTime, WebM for video).
///         Anything outside this list is rejected at the <c>CompleteMediaUpload</c> boundary so the
///         worker never has to deal with unknown payloads.
///     </para>
///     <para>
///         <b>What this is NOT:</b> a virus scanner. Magic bytes only protect against trivial
///         MIME-type lies. For threat models that include weaponised media files (CVEs in image decoders,
///         MP4 atom-injection attacks) layer ClamAV / EnGarde / similar between this verifier and the
///         processing pipeline.
///     </para>
/// </remarks>
public static class MediaContentTypeVerifier
{
    /// <summary>
    ///     Minimum bytes a HEAD-then-range probe needs to fetch from storage to run all sniffers
    ///     below. Aligned to 32 to cover ISO-BMFF box headers (the longest signature we look at).
    /// </summary>
    public const int RequiredHeaderBytes = 32;

    /// <summary>
    ///     Returns the canonical media content type detected from <paramref name="header" />, or
    ///     <c>null</c> if the bytes don't match any supported format. Caller compares the returned
    ///     value against the client-declared type.
    /// </summary>
    /// <remarks>
    ///     Signatures sourced from Wikipedia "List of file signatures" + the WHATWG MIME Sniffing
    ///     Standard. ISO Base Media Format (MP4/MOV) is identified by the <c>ftyp</c> box at offset 4
    ///     with a known brand; this catches the major variants (mp4, isom, qt, M4V).
    /// </remarks>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4)
            return null;

        // JPEG: FF D8 FF (start of image)
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header.Length >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return "image/png";

        // GIF: GIF87a or GIF89a
        if (header.Length >= 6
            && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46
            && header[3] == 0x38 && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61)
            return "image/gif";

        // WebP: "RIFF" .... "WEBP"
        if (header.Length >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return "image/webp";

        // ISO Base Media (MP4 / MOV / M4V / 3GP). Layout: 4-byte size, then "ftyp" (0x66 0x74 0x79 0x70),
        // then the major brand. We accept any brand because containers occasionally lie but the
        // remux pipeline normalises everything to H.264/AAC anyway.
        if (header.Length >= 12
            && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
        {
            // QuickTime vs MP4 — distinguish by brand for client-side hints, otherwise treat as mp4.
            if (header[8] == 0x71 && header[9] == 0x74 && header[10] == 0x20 && header[11] == 0x20)
                return "video/quicktime";
            return "video/mp4";
        }

        // WebM / Matroska: 1A 45 DF A3 (EBML header)
        if (header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
            return "video/webm";

        return null;
    }

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="detected" /> is an acceptable substitute for
    ///     <paramref name="declared" />. Exact match always passes; image/jpeg ↔ image/jpg, and
    ///     MP4 ↔ QuickTime/M4V are treated as equivalent because the worker normalises them.
    /// </summary>
    public static bool IsCompatible(string declared, string detected)
    {
        if (string.Equals(declared, detected, StringComparison.OrdinalIgnoreCase))
            return true;

        // image/jpg is widely seen but not an IANA type — accept against image/jpeg.
        if (string.Equals(declared, "image/jpg", StringComparison.OrdinalIgnoreCase)
            && string.Equals(detected, "image/jpeg", StringComparison.OrdinalIgnoreCase))
            return true;

        // QuickTime and MP4 share an ISO-BMFF container — the worker can handle either.
        var declaredIsMp4Family = declared.Equals("video/mp4", StringComparison.OrdinalIgnoreCase)
                                  || declared.Equals("video/quicktime", StringComparison.OrdinalIgnoreCase)
                                  || declared.Equals("video/x-m4v", StringComparison.OrdinalIgnoreCase);
        var detectedIsMp4Family = detected.Equals("video/mp4", StringComparison.OrdinalIgnoreCase)
                                  || detected.Equals("video/quicktime", StringComparison.OrdinalIgnoreCase);
        return declaredIsMp4Family && detectedIsMp4Family;
    }
}