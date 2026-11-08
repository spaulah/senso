using System.ComponentModel;
using ModelContextProtocol.Server;
using Senso.Mcp.Ocr;
using Senso.Mcp.Speech;

namespace Senso.Mcp.Tools;

[McpServerToolType]
public sealed class SensoTools
{
    private readonly OcrService _ocr;
    private readonly TranscriptionService _speech;
    private readonly ToolRunner _runner;

    public SensoTools(OcrService ocr, TranscriptionService speech, ToolRunner runner)
    {
        _ocr = ocr;
        _speech = speech;
        _runner = runner;
    }

    [McpServerTool(Name = "ocr_image", ReadOnly = true, OpenWorld = true)]
    [Description("Extract text from an image (printed or handwritten) using Azure AI Vision. The image is uploaded to your Azure resource. " +
                 "Returns the text plus, optionally, lines and words with bounding boxes [x, y, width, height] in pixels and confidence. " +
                 "Supports JPEG, PNG, GIF, BMP, WebP and TIFF. Provide exactly one of filePath or base64Data.")]
    public Task<OcrResult> OcrImage(
        [Description("Absolute path of a local image file.")] string? filePath = null,
        [Description("Base64-encoded image bytes (a data: URL is also accepted).")] string? base64Data = null,
        [Description("Include line and word blocks with bounding boxes and confidence.")] bool includeLayout = true,
        [Description("Best-effort masking of emails, phone, SSN-like and card-like numbers. Not a privacy guarantee.")] bool redactPii = false,
        [Description("Timeout in seconds (default 120).")] int? timeoutSeconds = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync("ocr_image", timeoutSeconds, cancellationToken,
            ct => _ocr.RecognizeAsync(filePath, base64Data, includeLayout, redactPii, ct));

    [McpServerTool(Name = "transcribe_audio", ReadOnly = true, OpenWorld = true)]
    [Description("Transcribe an audio file with speaker labels and timestamped segments using an Azure OpenAI transcription deployment. " +
                 "The audio is uploaded to your Azure resource. Supports WAV, MP3, M4A, OGG, FLAC and WebM up to 25 MB. " +
                 "Formats: json (text + segments), text, srt, vtt. Provide exactly one of filePath or base64Data.")]
    public Task<TranscriptionResult> TranscribeAudio(
        [Description("Absolute path of a local audio file.")] string? filePath = null,
        [Description("Base64-encoded audio bytes (a data: URL is also accepted).")] string? base64Data = null,
        [Description("Optional spoken language, e.g. 'en' or 'es-MX'. Omit to auto-detect.")] string? language = null,
        [Description("Output format: json (default), text, srt or vtt.")] string format = "json",
        [Description("Timeout in seconds (default 120).")] int? timeoutSeconds = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync("transcribe_audio", timeoutSeconds, cancellationToken,
            ct => _speech.TranscribeAsync(filePath, base64Data, language, format, ct));
}
