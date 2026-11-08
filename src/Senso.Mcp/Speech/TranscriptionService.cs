using Senso.Mcp.Input;

namespace Senso.Mcp.Speech;

public sealed class TranscriptionService
{
    private readonly MediaInputResolver _input;
    private readonly ISpeechClient _client;

    public TranscriptionService(MediaInputResolver input, ISpeechClient client)
    {
        _input = input;
        _client = client;
    }

    public static TranscriptFormat ParseFormat(string? format) => (format ?? "json").Trim().ToLowerInvariant() switch
    {
        "json" => TranscriptFormat.Json,
        "text" => TranscriptFormat.Text,
        "srt" => TranscriptFormat.Srt,
        "vtt" => TranscriptFormat.Vtt,
        _ => throw new SensoException(SensoErrorCode.InvalidInput, "Unsupported format. Use one of: json, text, srt, vtt.")
    };

    /// <summary>"en-US" and "en_US" become "en"; null/blank means auto-detect.</summary>
    public static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;
        var primary = language.Trim().Split('-', '_')[0].ToLowerInvariant();
        if (primary.Length is < 2 or > 3 || !primary.All(char.IsAsciiLetter))
            throw new SensoException(SensoErrorCode.InvalidInput, "'language' must be a language code such as 'en' or 'es-MX'.");
        return primary;
    }

    public async Task<TranscriptionResult> TranscribeAsync(string? filePath, string? base64Data, string? language, string? format, CancellationToken ct)
    {
        var fmt = ParseFormat(format);
        var lang = NormalizeLanguage(language);
        var media = await _input.ResolveAsync(filePath, base64Data, MediaKind.Audio, ct).ConfigureAwait(false);
        var raw = await _client.TranscribeAsync(media.Bytes, media.MimeType, media.FileName, lang, ct).ConfigureAwait(false);

        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(raw.Text)) warnings.Add("No speech was detected in the audio.");

        switch (fmt)
        {
            case TranscriptFormat.Srt or TranscriptFormat.Vtt:
                if (raw.Segments.Count == 0)
                    throw new SensoException(SensoErrorCode.Provider,
                        "The provider returned no timestamped segments, so subtitles cannot be produced. Use format 'text', or check that the speech deployment supports segment output (see README).");
                var subs = fmt == TranscriptFormat.Srt ? SubtitleFormatter.ToSrt(raw.Segments) : SubtitleFormatter.ToVtt(raw.Segments);
                return new TranscriptionResult(fmt.ToString().ToLowerInvariant(), null, null, subs, Nullable(warnings));

            case TranscriptFormat.Text:
                return new TranscriptionResult("text", raw.Text, null, null, Nullable(warnings));

            default:
                if (raw.Segments.Count == 0 && !string.IsNullOrWhiteSpace(raw.Text))
                    warnings.Add("The provider returned no timestamped segments; only the full text is available.");
                return new TranscriptionResult("json", raw.Text, raw.Segments, null, Nullable(warnings));
        }
    }

    private static IReadOnlyList<string>? Nullable(List<string> w) => w.Count > 0 ? w : null;
}
