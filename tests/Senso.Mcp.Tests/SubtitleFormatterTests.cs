using Senso.Mcp.Speech;
using Xunit;

namespace Senso.Mcp.Tests;

public class SubtitleFormatterTests
{
    private static readonly TranscriptSegment[] Segs =
    {
        new("A", 0, 1.5, "Hi there."),
        new(null, 3661.007, 3662.5, "No speaker."),
    };

    [Fact]
    public void Srt_has_index_comma_timestamps_and_speaker_prefix()
    {
        var srt = SubtitleFormatter.ToSrt(Segs);
        Assert.Equal("1\n00:00:00,000 --> 00:00:01,500\nA: Hi there.\n\n2\n01:01:01,007 --> 01:01:02,500\nNo speaker.\n", srt);
    }

    [Fact]
    public void Vtt_has_header_and_dot_timestamps_without_indexes()
    {
        var vtt = SubtitleFormatter.ToVtt(Segs);
        Assert.Equal("WEBVTT\n\n00:00:00.000 --> 00:00:01.500\nA: Hi there.\n\n01:01:01.007 --> 01:01:02.500\nNo speaker.\n", vtt);
    }

    [Theory]
    [InlineData(1.9996, "00:00:02,000")]
    [InlineData(59.9995, "00:01:00,000")]
    [InlineData(-3, "00:00:00,000")]
    public void Timestamp_rounds_once_and_never_overflows_milliseconds(double seconds, string expected) =>
        Assert.Equal(expected, SubtitleFormatter.Timestamp(seconds, ','));

    [Fact]
    public void Empty_input_yields_valid_documents()
    {
        Assert.Equal("\n", SubtitleFormatter.ToSrt(Array.Empty<TranscriptSegment>()));
        Assert.StartsWith("WEBVTT", SubtitleFormatter.ToVtt(Array.Empty<TranscriptSegment>()));
    }
}
