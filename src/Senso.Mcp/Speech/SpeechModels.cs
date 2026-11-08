namespace Senso.Mcp.Speech;

/// <summary>A timestamped piece of speech. Times are seconds from the start of the audio.</summary>
public sealed record TranscriptSegment(string? Speaker, double StartSeconds, double EndSeconds, string Text);

public sealed record SpeechProviderResult(string Text, IReadOnlyList<TranscriptSegment> Segments);

public enum TranscriptFormat { Json, Text, Srt, Vtt }

/// <summary>Tool-facing transcription result. Only the fields relevant to the requested format are populated.</summary>
public sealed record TranscriptionResult(
    string Format,
    string? Text,
    IReadOnlyList<TranscriptSegment>? Segments,
    string? Subtitles,
    IReadOnlyList<string>? Warnings);
