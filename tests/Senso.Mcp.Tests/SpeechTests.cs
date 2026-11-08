using System.Net;
using Senso.Mcp.Input;
using Senso.Mcp.Speech;
using Xunit;

namespace Senso.Mcp.Tests;

public class SpeechTests
{
    private static AzureOpenAiSpeechClient Client(FakeHandler h, Action<SensoOptions>? c = null) => new(new HttpClient(h), Samples.Options(c));

    private static TranscriptionService Service(FakeHandler h, Action<SensoOptions>? c = null) =>
        new(new MediaInputResolver(Samples.Options(c)), Client(h, c));

    private static string WavB64 => Convert.ToBase64String(Samples.Wav);

    [Fact]
    public void Maps_segments_with_speakers_and_trims_text()
    {
        var r = AzureOpenAiSpeechClient.Map(Samples.SpeechResponse);
        Assert.Equal("Hi there. Hello.", r.Text);
        Assert.Collection(r.Segments,
            s => { Assert.Equal("A", s.Speaker); Assert.Equal(0, s.StartSeconds); Assert.Equal(1.5, s.EndSeconds); Assert.Equal("Hi there.", s.Text); },
            s => { Assert.Equal("B", s.Speaker); Assert.Equal(3.25, s.EndSeconds); });
    }

    [Fact]
    public void Plain_json_without_segments_maps_to_text_only()
    {
        var r = AzureOpenAiSpeechClient.Map("""{"text":"just text"}""");
        Assert.Equal("just text", r.Text);
        Assert.Empty(r.Segments);
    }

    [Fact]
    public async Task Sends_configured_model_format_chunking_language_and_api_key()
    {
        var h = new FakeHandler(HttpStatusCode.OK, Samples.SpeechResponse);
        var svc = Service(h, o => o.Speech.Model = "my-deployment");
        await svc.TranscribeAsync(null, WavB64, "en-US", "json", default);

        var req = h.Requests.Single();
        Assert.Equal("speech.example.test", req.RequestUri!.Host);
        Assert.Equal("test-speech-key", req.Headers.GetValues("api-key").Single());
        Assert.Null(req.Headers.Authorization);
        var body = h.Bodies.Single();
        Assert.Contains("my-deployment", body);
        Assert.Contains("diarized_json", body);
        Assert.Contains("name=chunking_strategy", body);
        Assert.Contains("name=language", body);
        Assert.Contains("RIFF", body);
        Assert.Contains("filename=audio.wav", body);
    }

    [Fact]
    public async Task Omits_language_when_not_given_and_chunking_when_blank()
    {
        var h = new FakeHandler(HttpStatusCode.OK, Samples.SpeechResponse);
        await Service(h, o => o.Speech.ChunkingStrategy = "").TranscribeAsync(null, WavB64, null, "json", default);
        Assert.DoesNotContain("name=language", h.Bodies.Single());
        Assert.DoesNotContain("name=chunking_strategy", h.Bodies.Single());
    }

    [Fact]
    public async Task Json_format_returns_text_and_segments_only()
    {
        var r = await Service(new FakeHandler(HttpStatusCode.OK, Samples.SpeechResponse)).TranscribeAsync(null, WavB64, null, "json", default);
        Assert.Equal("json", r.Format);
        Assert.Equal(2, r.Segments!.Count);
        Assert.Null(r.Subtitles);
    }

    [Fact]
    public async Task Text_format_omits_segments()
    {
        var r = await Service(new FakeHandler(HttpStatusCode.OK, Samples.SpeechResponse)).TranscribeAsync(null, WavB64, null, "TEXT", default);
        Assert.Equal("text", r.Format);
        Assert.Equal("Hi there. Hello.", r.Text);
        Assert.Null(r.Segments);
    }

    [Theory]
    [InlineData("srt", "00:00:01,500 --> 00:00:03,250")]
    [InlineData("vtt", "00:00:01.500 --> 00:00:03.250")]
    public async Task Srt_and_vtt_are_honored(string format, string expectedCue)
    {
        var r = await Service(new FakeHandler(HttpStatusCode.OK, Samples.SpeechResponse)).TranscribeAsync(null, WavB64, null, format, default);
        Assert.Equal(format, r.Format);
        Assert.Contains(expectedCue, r.Subtitles);
        Assert.Contains("B: Hello.", r.Subtitles);
        Assert.Null(r.Text);
        Assert.Null(r.Segments);
    }

    [Fact]
    public async Task Subtitles_without_segments_fail_instead_of_returning_empty_subtitles()
    {
        var svc = Service(new FakeHandler(HttpStatusCode.OK, """{"text":"only text"}"""));
        var ex = await Assert.ThrowsAsync<SensoException>(() => svc.TranscribeAsync(null, WavB64, null, "srt", default));
        Assert.Equal(SensoErrorCode.Provider, ex.Code);
    }

    [Fact]
    public async Task Unknown_format_is_rejected_before_any_work()
    {
        var h = new FakeHandler(HttpStatusCode.OK, Samples.SpeechResponse);
        var ex = await Assert.ThrowsAsync<SensoException>(() => Service(h).TranscribeAsync(null, WavB64, null, "docx", default));
        Assert.Equal(SensoErrorCode.InvalidInput, ex.Code);
        Assert.Empty(h.Requests);
    }

    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("es_MX", "es")]
    [InlineData("FR", "fr")]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    public void Normalizes_language(string? input, string? expected) =>
        Assert.Equal(expected, TranscriptionService.NormalizeLanguage(input));

    [Theory]
    [InlineData("english please")]
    [InlineData("1")]
    [InlineData("e")]
    public void Rejects_bad_language(string input) =>
        Assert.Throws<SensoException>(() => TranscriptionService.NormalizeLanguage(input));

    [Fact]
    public async Task Missing_configuration_is_an_actionable_error()
    {
        var h = new FakeHandler(HttpStatusCode.OK, "{}");
        var ex = await Assert.ThrowsAsync<SensoException>(() => Service(h, o => o.Speech.Endpoint = null).TranscribeAsync(null, WavB64, null, "json", default));
        Assert.Equal(SensoErrorCode.Configuration, ex.Code);
        Assert.Contains("Senso__Speech__Endpoint", ex.Message);
        Assert.Empty(h.Requests);
    }

    [Fact]
    public async Task Provider_error_surfaces_message_without_echoing_key()
    {
        var h = new FakeHandler(HttpStatusCode.NotFound, """{"error":{"code":"DeploymentNotFound","message":"The API deployment does not exist."}}""");
        var ex = await Assert.ThrowsAsync<SensoException>(() => Service(h).TranscribeAsync(null, WavB64, null, "json", default));
        Assert.Contains("deployment does not exist", ex.Message);
        Assert.Contains("api-version", ex.Message);
        Assert.DoesNotContain("test-speech-key", ex.Message);
    }

    [Fact]
    public async Task Audio_over_limit_is_rejected_before_upload()
    {
        var h = new FakeHandler(HttpStatusCode.OK, Samples.SpeechResponse);
        var big = new byte[2 * 1024 * 1024];
        Array.Copy(Samples.Wav, big, Samples.Wav.Length);
        await Assert.ThrowsAsync<SensoException>(() =>
            Service(h, o => o.Limits.MaxAudioSizeMb = 1).TranscribeAsync(null, Convert.ToBase64String(big), null, "json", default));
        Assert.Empty(h.Requests);
    }
}
