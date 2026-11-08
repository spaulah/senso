namespace Senso.Mcp.Speech;

public interface ISpeechClient
{
    /// <param name="language">Optional ISO-639-1 code (e.g. "en"); null lets the service auto-detect.</param>
    Task<SpeechProviderResult> TranscribeAsync(byte[] audio, string mimeType, string fileName, string? language, CancellationToken ct);
}
