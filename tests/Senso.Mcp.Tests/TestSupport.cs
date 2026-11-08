using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Senso.Mcp;

namespace Senso.Mcp.Tests;

internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> Bodies { get; } = new();

    public FakeHandler(HttpStatusCode status, string body)
        : this(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }) { }

    public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? "" : Encoding.Latin1.GetString(await request.Content.ReadAsByteArrayAsync(ct)));
        return _respond(request);
    }
}

internal static class Samples
{
    public static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
    public static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0, 0 };
    public static readonly byte[] Wav = Encoding.ASCII.GetBytes("RIFF\0\0\0\0WAVEfmt ");
    public static readonly byte[] Garbage = Encoding.ASCII.GetBytes("this is not media at all");

    public static IOptions<SensoOptions> Options(Action<SensoOptions>? configure = null)
    {
        var o = new SensoOptions
        {
            Vision = { Endpoint = "https://vision.example.test", Key = "test-vision-key" },
            Speech = { Endpoint = "https://speech.example.test/openai/deployments/d/audio/transcriptions?api-version=x", Key = "test-speech-key" }
        };
        configure?.Invoke(o);
        return Microsoft.Extensions.Options.Options.Create(o);
    }

    public static string TempFile(string name, byte[] content, string? dir = null)
    {
        dir ??= Path.Combine(Path.GetTempPath(), "senso-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var p = Path.Combine(dir, name);
        File.WriteAllBytes(p, content);
        return p;
    }

    public const string VisionResponse = """
    {
      "modelVersion": "2024-02-01",
      "metadata": { "width": 200, "height": 100 },
      "readResult": { "blocks": [ { "lines": [
        { "text": "Hello World", "boundingPolygon": [ {"x":10,"y":20}, {"x":110,"y":20}, {"x":110,"y":40}, {"x":10,"y":40} ],
          "words": [
            { "text": "Hello", "boundingPolygon": [ {"x":10,"y":20}, {"x":50,"y":20}, {"x":50,"y":40}, {"x":10,"y":40} ], "confidence": 0.99 },
            { "text": "World", "boundingPolygon": [ {"x":60,"y":20}, {"x":110,"y":20}, {"x":110,"y":40}, {"x":60,"y":40} ], "confidence": 0.97 }
          ] },
        { "text": "mail me: jane@example.com", "boundingPolygon": [ {"x":10,"y":50}, {"x":190,"y":50}, {"x":190,"y":70}, {"x":10,"y":70} ],
          "words": [ { "text": "jane@example.com", "boundingPolygon": [ {"x":100,"y":50}, {"x":190,"y":50}, {"x":190,"y":70}, {"x":100,"y":70} ], "confidence": 0.9 } ] }
      ] } ] }
    }
    """;

    public const string SpeechResponse = """
    { "text": "Hi there. Hello.", "segments": [
      { "speaker": "A", "start": 0.0, "end": 1.5, "text": " Hi there." },
      { "speaker": "B", "start": 1.5, "end": 3.25, "text": "Hello." } ] }
    """;
}
