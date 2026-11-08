namespace Senso.Mcp.Ocr;

/// <summary>A recognized line or word. Bbox is [x, y, width, height] in image pixels.</summary>
public sealed record OcrBlock(string Type, string Text, double[] Bbox, double? Confidence);

/// <summary>Raw OCR output from a provider.</summary>
public sealed record OcrProviderResult(string Text, IReadOnlyList<OcrBlock> Blocks, string? ModelVersion, int? ImageWidth, int? ImageHeight);

/// <summary>Tool-facing OCR result.</summary>
public sealed record OcrResult(
    string Text,
    IReadOnlyList<OcrBlock>? Blocks,
    string? ModelVersion,
    int? ImageWidth,
    int? ImageHeight,
    bool PiiRedacted,
    IReadOnlyList<string>? Warnings);
