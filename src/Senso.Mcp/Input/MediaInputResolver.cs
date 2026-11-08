using Microsoft.Extensions.Options;

namespace Senso.Mcp.Input;

/// <summary>
/// Loads media from a local file path or base64 data. Remote URLs are intentionally NOT supported
/// (avoids SSRF toward loopback/private/link-local/metadata addresses).
/// </summary>
public sealed class MediaInputResolver
{
    private readonly SensoOptions _options;

    public MediaInputResolver(IOptions<SensoOptions> options) => _options = options.Value;

    public async Task<ResolvedMedia> ResolveAsync(string? filePath, string? base64Data, MediaKind kind, CancellationToken ct)
    {
        var hasPath = !string.IsNullOrWhiteSpace(filePath);
        var hasB64 = !string.IsNullOrWhiteSpace(base64Data);
        if (hasPath == hasB64)
            throw Invalid("Provide exactly one of 'filePath' or 'base64Data'.");

        var maxBytes = (kind == MediaKind.Image ? _options.Limits.MaxImageSizeMb : _options.Limits.MaxAudioSizeMb) * 1024L * 1024L;
        var label = kind == MediaKind.Image ? "image" : "audio";

        byte[] bytes;
        string fileName;
        if (hasPath)
        {
            (bytes, fileName) = await ReadFileAsync(filePath!.Trim(), kind, maxBytes, ct).ConfigureAwait(false);
        }
        else
        {
            bytes = DecodeBase64(base64Data!, maxBytes, label);
            fileName = label;
        }

        var sniffed = MediaSniffer.Detect(bytes, kind)
            ?? throw Invalid($"Unrecognized {label} content. Supported formats: " +
                             string.Join(", ", kind == MediaKind.Image ? MediaSniffer.ImageFormats : MediaSniffer.AudioFormats) + ".");

        return new ResolvedMedia(bytes, sniffed.Format, sniffed.Mime, $"{Path.GetFileNameWithoutExtension(fileName)}.{sniffed.Format}");
    }

    private async Task<(byte[], string)> ReadFileAsync(string path, MediaKind kind, long maxBytes, CancellationToken ct)
    {
        if (path.Contains("://", StringComparison.Ordinal))
            throw Invalid("URLs are not supported. Provide a local 'filePath' or 'base64Data'.");
        if (path.Contains('\0'))
            throw Invalid("Invalid file path.");

        string full;
        try { full = Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { throw Invalid("Invalid file path."); }

        var info = new FileInfo(full);
        if (!info.Exists) throw Invalid("File not found.");

        // Resolve symlinks so the allow-list cannot be bypassed through links.
        var real = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? full;
        EnforceAllowedDirectories(real);

        var allowed = kind == MediaKind.Image ? ImageExtensions : AudioExtensions;
        var ext = Path.GetExtension(full);
        if (!allowed.Contains(ext))
            throw Invalid($"Unsupported file extension '{ext}'. Supported: {string.Join(", ", allowed)}.");

        var realInfo = new FileInfo(real);
        if (realInfo.Length > maxBytes)
            throw Invalid($"File is {realInfo.Length} bytes; the limit for {kind.ToString().ToLowerInvariant()} is {maxBytes} bytes (Senso:Limits).");

        return (await File.ReadAllBytesAsync(real, ct).ConfigureAwait(false), Path.GetFileName(full));
    }

    private void EnforceAllowedDirectories(string realPath)
    {
        if (_options.AllowedDirectories.Count == 0) return;
        foreach (var dir in _options.AllowedDirectories)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var root = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (realPath.StartsWith(root, cmp)) return;
        }
        throw Invalid("File is outside the directories permitted by Senso:AllowedDirectories.");
    }

    private static byte[] DecodeBase64(string data, long maxBytes, string label)
    {
        var s = data.Trim();
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = s.IndexOf(',');
            if (comma < 0 || !s[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
                throw Invalid("Data URLs must be base64 encoded.");
            s = s[(comma + 1)..];
        }

        // Check the encoded size before allocating the decoded buffer.
        if (s.Length / 4L * 3L > maxBytes + 3)
            throw Invalid($"Decoded {label} would exceed the limit of {maxBytes} bytes (Senso:Limits).");

        try { return Convert.FromBase64String(s); }
        catch (FormatException) { throw Invalid("'base64Data' is not valid base64."); }
    }

    private static SensoException Invalid(string message) => new(SensoErrorCode.InvalidInput, message);

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff" };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".wav", ".mp3", ".m4a", ".mp4", ".ogg", ".flac", ".webm" };
}
