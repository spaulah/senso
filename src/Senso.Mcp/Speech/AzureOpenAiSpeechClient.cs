using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Senso.Mcp.Ocr;

namespace Senso.Mcp.Speech;

/// <summary>
/// Azure OpenAI audio transcription (e.g. a gpt-4o-transcribe-diarize deployment) via REST multipart upload.
/// The endpoint URL (deployment + api-version) is supplied by configuration; nothing is hard-coded.
/// </summary>
public sealed class AzureOpenAiSpeechClient : ISpeechClient
{
    private readonly HttpClient _http;
    private readonly SpeechOptions _options;

    public AzureOpenAiSpeechClient(HttpClient http, IOptions<SensoOptions> options)
    {
        _http = http;
        _options = options.Value.Speech;
    }

    public async Task<SpeechProviderResult> TranscribeAsync(byte[] audio, string mimeType, string fileName, string? language, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.Key))
            throw new SensoException(SensoErrorCode.Configuration,
                "Transcription is not configured. Set Senso__Speech__Endpoint and Senso__Speech__Key (see README).");
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            throw new SensoException(SensoErrorCode.Configuration, "Senso:Speech:Endpoint must be an absolute https URL.");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(audio);
        file.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(_options.Model), "model");
        form.Add(new StringContent(_options.ResponseFormat), "response_format");
        if (!string.IsNullOrWhiteSpace(_options.ChunkingStrategy))
            form.Add(new StringContent(_options.ChunkingStrategy), "chunking_strategy");
        if (!string.IsNullOrWhiteSpace(language))
            form.Add(new StringContent(language), "language");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = form };
        request.Headers.Add("api-key", _options.Key);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw ProviderError.From("Azure OpenAI", response.StatusCode, body);

        try { return Map(body); }
        catch (JsonException ex)
        {
            throw new SensoException(SensoErrorCode.Provider, "Azure OpenAI returned a response that could not be parsed.", ex);
        }
    }

    /// <summary>Maps {"text": "...", "segments": [{"speaker","start","end","text"}]} to domain types.</summary>
    internal static SpeechProviderResult Map(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var segments = new List<TranscriptSegment>();

        if (root.TryGetProperty("segments", out var segs) && segs.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in segs.EnumerateArray())
            {
                var text = s.TryGetProperty("text", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(text)) continue;
                string? speaker = s.TryGetProperty("speaker", out var sp) && sp.ValueKind != JsonValueKind.Null ? sp.ToString() : null;
                segments.Add(new TranscriptSegment(speaker, Num(s, "start"), Num(s, "end"), text.Trim()));
            }
        }

        var full = root.TryGetProperty("text", out var tx) ? tx.GetString() : null;
        if (string.IsNullOrWhiteSpace(full))
            full = string.Join(" ", segments.Select(x => x.Text));
        return new SpeechProviderResult(full ?? "", segments);
    }

    private static double Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
}
