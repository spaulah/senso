using Senso.Mcp.Input;

namespace Senso.Mcp.Ocr;

public sealed class OcrService
{
    private readonly MediaInputResolver _input;
    private readonly IOcrClient _client;

    public OcrService(MediaInputResolver input, IOcrClient client)
    {
        _input = input;
        _client = client;
    }

    public async Task<OcrResult> RecognizeAsync(string? filePath, string? base64Data, bool includeLayout, bool redactPii, CancellationToken ct)
    {
        var media = await _input.ResolveAsync(filePath, base64Data, MediaKind.Image, ct).ConfigureAwait(false);
        var raw = await _client.ReadAsync(media.Bytes, media.MimeType, ct).ConfigureAwait(false);

        var text = raw.Text;
        var blocks = raw.Blocks;
        var warnings = new List<string>();

        if (redactPii)
        {
            text = PiiRedactor.Redact(text);
            blocks = blocks.Select(b => b with { Text = PiiRedactor.Redact(b.Text) }).ToList();
            warnings.Add("PII redaction is best-effort pattern matching and may miss personal data.");
        }

        if (string.IsNullOrWhiteSpace(text))
            warnings.Add("No text was detected in the image.");

        return new OcrResult(text, includeLayout ? blocks : null, raw.ModelVersion, raw.ImageWidth, raw.ImageHeight,
            redactPii, warnings.Count > 0 ? warnings : null);
    }
}
