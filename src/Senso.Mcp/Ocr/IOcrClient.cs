namespace Senso.Mcp.Ocr;

public interface IOcrClient
{
    Task<OcrProviderResult> ReadAsync(byte[] image, string mimeType, CancellationToken ct);
}
