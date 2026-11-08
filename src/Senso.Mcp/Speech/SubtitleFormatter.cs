using System.Globalization;
using System.Text;

namespace Senso.Mcp.Speech;

public static class SubtitleFormatter
{
    public static string ToSrt(IEnumerable<TranscriptSegment> segments) => Build(segments, ',', header: false);

    public static string ToVtt(IEnumerable<TranscriptSegment> segments) => Build(segments, '.', header: true);

    private static string Build(IEnumerable<TranscriptSegment> segments, char msSeparator, bool header)
    {
        var sb = new StringBuilder();
        if (header) sb.Append("WEBVTT\n\n");

        var index = 1;
        foreach (var s in segments)
        {
            if (!header) sb.Append(index.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append(Timestamp(s.StartSeconds, msSeparator)).Append(" --> ").Append(Timestamp(s.EndSeconds, msSeparator)).Append('\n');
            if (!string.IsNullOrWhiteSpace(s.Speaker)) sb.Append(s.Speaker).Append(": ");
            sb.Append(s.Text.Trim()).Append("\n\n");
            index++;
        }
        return sb.ToString().TrimEnd('\n') + "\n";
    }

    internal static string Timestamp(double seconds, char msSeparator)
    {
        // Round once to whole milliseconds, then split, so 1.9996s becomes 00:00:02,000 rather than 00:00:01,1000.
        var totalMs = (long)Math.Round(Math.Max(0, seconds) * 1000, MidpointRounding.AwayFromZero);
        var h = totalMs / 3_600_000;
        var m = totalMs % 3_600_000 / 60_000;
        var s = totalMs % 60_000 / 1000;
        var ms = totalMs % 1000;
        return string.Create(CultureInfo.InvariantCulture, $"{h:D2}:{m:D2}:{s:D2}{msSeparator}{ms:D3}");
    }
}
