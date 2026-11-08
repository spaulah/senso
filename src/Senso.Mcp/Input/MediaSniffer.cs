namespace Senso.Mcp.Input;

/// <summary>Identifies media formats from content signatures (never trusts extensions or client MIME types alone).</summary>
public static class MediaSniffer
{
    public static readonly string[] ImageFormats = { "jpeg", "png", "gif", "bmp", "webp", "tiff" };
    public static readonly string[] AudioFormats = { "wav", "mp3", "m4a", "ogg", "flac", "webm" };

    public static (string Format, string Mime)? Detect(ReadOnlySpan<byte> b, MediaKind kind) =>
        kind == MediaKind.Image ? DetectImage(b) : DetectAudio(b);

    private static (string, string)? DetectImage(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ("jpeg", "image/jpeg");
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ("png", "image/png");
        if (StartsWith(b, "GIF87a") || StartsWith(b, "GIF89a")) return ("gif", "image/gif");
        if (StartsWith(b, "BM") && b.Length > 14) return ("bmp", "image/bmp");
        if (b.Length >= 12 && StartsWith(b, "RIFF") && Ascii(b, 8, "WEBP")) return ("webp", "image/webp");
        if (b.Length >= 4 && ((b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x2A && b[3] == 0) ||
                              (b[0] == 0x4D && b[1] == 0x4D && b[2] == 0 && b[3] == 0x2A))) return ("tiff", "image/tiff");
        return null;
    }

    private static (string, string)? DetectAudio(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 12 && StartsWith(b, "RIFF") && Ascii(b, 8, "WAVE")) return ("wav", "audio/wav");
        if (StartsWith(b, "ID3")) return ("mp3", "audio/mpeg");
        if (b.Length >= 2 && b[0] == 0xFF && (b[1] & 0xE0) == 0xE0) return ("mp3", "audio/mpeg");
        if (b.Length >= 12 && Ascii(b, 4, "ftyp")) return ("m4a", "audio/mp4");
        if (StartsWith(b, "OggS")) return ("ogg", "audio/ogg");
        if (StartsWith(b, "fLaC")) return ("flac", "audio/flac");
        if (b.Length >= 4 && b[0] == 0x1A && b[1] == 0x45 && b[2] == 0xDF && b[3] == 0xA3) return ("webm", "audio/webm");
        return null;
    }

    private static bool StartsWith(ReadOnlySpan<byte> b, string ascii) => Ascii(b, 0, ascii);

    private static bool Ascii(ReadOnlySpan<byte> b, int offset, string ascii)
    {
        if (b.Length < offset + ascii.Length) return false;
        for (var i = 0; i < ascii.Length; i++)
            if (b[offset + i] != (byte)ascii[i]) return false;
        return true;
    }
}
