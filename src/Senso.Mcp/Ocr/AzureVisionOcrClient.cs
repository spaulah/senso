using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Senso.Mcp.Ocr;

/// <summary>
/// Azure AI Vision Image Analysis 4.0 "Read" feature via REST:
/// POST {endpoint}/computervision/imageanalysis:analyze?api-version=...&amp;features=read
/// </summary>
public sealed class AzureVisionOcrClient : IOcrClient
{
    private readonly HttpClient _http;
    private readonly VisionOptions _options;

    public AzureVisionOcrClient(HttpClient http, IOptions<SensoOptions> options)
    {
        _http = http;
        _options = options.Value.Vision;
    }

    public async Task<OcrProviderResult> ReadAsync(byte[] image, string mimeType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.Key))
            throw new SensoException(SensoErrorCode.Configuration,
                "OCR is not configured. Set Senso__Vision__Endpoint and Senso__Vision__Key (see README).");
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            throw new SensoException(SensoErrorCode.Configuration, "Senso:Vision:Endpoint must be an absolute https URL.");

        var url = new Uri(endpoint,
            $"/computervision/imageanalysis:analyze?api-version={Uri.EscapeDataString(_options.ApiVersion)}&features=read");

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(image) };
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.Key);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw ProviderError.From("Azure AI Vision", response.StatusCode, body);

        try { return Map(body); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new SensoException(SensoErrorCode.Provider, "Azure AI Vision returned a response that could not be parsed.", ex);
        }
    }

    /// <summary>Maps an Image Analysis 4.0 response to domain types.</summary>
    internal static OcrProviderResult Map(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var blocks = new List<OcrBlock>();
        var lineTexts = new List<string>();

        if (root.TryGetProperty("readResult", out var read) && read.TryGetProperty("blocks", out var rBlocks))
        {
            foreach (var block in rBlocks.EnumerateArray())
            {
                if (!block.TryGetProperty("lines", out var lines)) continue;
                foreach (var line in lines.EnumerateArray())
                {
                    var text = line.GetProperty("text").GetString() ?? "";
                    lineTexts.Add(text);

                    var words = new List<OcrBlock>();
                    if (line.TryGetProperty("words", out var ws))
                        foreach (var w in ws.EnumerateArray())
                            words.Add(new OcrBlock("word", w.GetProperty("text").GetString() ?? "",
                                BoundingBox(w), w.TryGetProperty("confidence", out var c) ? c.GetDouble() : null));

                    // The service reports confidence per word only; a line's confidence is the mean of its words.
                    double? lineConf = words.Count > 0 && words.All(x => x.Confidence.HasValue)
                        ? Math.Round(words.Average(x => x.Confidence!.Value), 4) : null;
                    blocks.Add(new OcrBlock("line", text, BoundingBox(line), lineConf));
                    blocks.AddRange(words);
                }
            }
        }

        int? w0 = null, h0 = null;
        if (root.TryGetProperty("metadata", out var meta))
        {
            if (meta.TryGetProperty("width", out var w)) w0 = w.GetInt32();
            if (meta.TryGetProperty("height", out var h)) h0 = h.GetInt32();
        }

        var model = root.TryGetProperty("modelVersion", out var mv) ? mv.GetString() : null;
        return new OcrProviderResult(string.Join("\n", lineTexts), blocks, model, w0, h0);
    }

    private static double[] BoundingBox(JsonElement e)
    {
        if (!e.TryGetProperty("boundingPolygon", out var poly) || poly.ValueKind != JsonValueKind.Array)
            return Array.Empty<double>();
        var xs = new List<double>();
        var ys = new List<double>();
        foreach (var p in poly.EnumerateArray())
        {
            xs.Add(p.GetProperty("x").GetDouble());
            ys.Add(p.GetProperty("y").GetDouble());
        }
        if (xs.Count == 0) return Array.Empty<double>();
        var minX = xs.Min(); var minY = ys.Min();
        return new[] { minX, minY, xs.Max() - minX, ys.Max() - minY };
    }
}

internal static class ProviderError
{
    public static SensoException From(string provider, HttpStatusCode status, string body)
    {
        var detail = ExtractMessage(body);
        var hint = status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => " Check the configured key and endpoint.",
            HttpStatusCode.NotFound => " Check the configured endpoint/deployment and api-version.",
            HttpStatusCode.TooManyRequests => " The service is rate limiting requests; retry later.",
            _ => ""
        };
        return new SensoException(SensoErrorCode.Provider,
            $"{provider} returned HTTP {(int)status} ({status}){(detail is null ? "" : $": {detail}")}.{hint}");
    }

    // Provider error bodies look like {"error":{"code":"..","message":".."}}. Only that message is surfaced.
    private static string? ExtractMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                var msg = err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var m) ? m.GetString() : err.ToString();
                return msg is { Length: > 300 } ? msg[..300] + "…" : msg;
            }
        }
        catch (JsonException) { }
        return null;
    }
}
