using System.Net;
using Senso.Mcp.Input;
using Senso.Mcp.Ocr;
using Xunit;

namespace Senso.Mcp.Tests;

public class OcrTests
{
    private static AzureVisionOcrClient Client(FakeHandler h, Action<SensoOptions>? c = null) => new(new HttpClient(h), Samples.Options(c));

    [Fact]
    public void Maps_azure_response_to_lines_words_boxes_and_confidence()
    {
        var r = AzureVisionOcrClient.Map(Samples.VisionResponse);

        Assert.Equal("Hello World\nmail me: jane@example.com", r.Text);
        Assert.Equal(200, r.ImageWidth);
        Assert.Equal(100, r.ImageHeight);
        Assert.Equal("2024-02-01", r.ModelVersion);

        var line = r.Blocks[0];
        Assert.Equal("line", line.Type);
        Assert.Equal(new[] { 10.0, 20.0, 100.0, 20.0 }, line.Bbox);
        Assert.Equal(0.98, line.Confidence); // mean of 0.99 and 0.97
        Assert.Equal(new[] { "line", "word", "word", "line", "word" }, r.Blocks.Select(b => b.Type));
        Assert.Equal(0.97, r.Blocks[2].Confidence);
    }

    [Fact]
    public void Maps_response_without_text_to_empty_result()
    {
        var r = AzureVisionOcrClient.Map("""{ "modelVersion": "x", "metadata": {"width":1,"height":1}, "readResult": { "blocks": [] } }""");
        Assert.Equal("", r.Text);
        Assert.Empty(r.Blocks);
    }

    [Fact]
    public async Task Sends_key_header_binary_body_and_read_feature_to_configured_endpoint()
    {
        var h = new FakeHandler(HttpStatusCode.OK, Samples.VisionResponse);
        await Client(h).ReadAsync(Samples.Png, "image/png", default);

        var req = h.Requests.Single();
        Assert.Equal("vision.example.test", req.RequestUri!.Host);
        Assert.Equal("/computervision/imageanalysis:analyze", req.RequestUri.AbsolutePath);
        Assert.Contains("features=read", req.RequestUri.Query);
        Assert.Contains("api-version=2024-02-01", req.RequestUri.Query);
        Assert.Equal("test-vision-key", req.Headers.GetValues("Ocp-Apim-Subscription-Key").Single());
        Assert.Equal("application/octet-stream", req.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Missing_configuration_is_an_actionable_error_and_sends_nothing()
    {
        var h = new FakeHandler(HttpStatusCode.OK, "{}");
        var ex = await Assert.ThrowsAsync<SensoException>(() => Client(h, o => o.Vision.Key = null).ReadAsync(Samples.Png, "image/png", default));
        Assert.Equal(SensoErrorCode.Configuration, ex.Code);
        Assert.Contains("Senso__Vision__Key", ex.Message);
        Assert.Empty(h.Requests);
    }

    [Fact]
    public async Task Rejects_non_https_endpoint()
    {
        var h = new FakeHandler(HttpStatusCode.OK, "{}");
        var ex = await Assert.ThrowsAsync<SensoException>(() => Client(h, o => o.Vision.Endpoint = "http://169.254.169.254").ReadAsync(Samples.Png, "image/png", default));
        Assert.Equal(SensoErrorCode.Configuration, ex.Code);
        Assert.Empty(h.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Check the configured key")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate limiting")]
    [InlineData(HttpStatusCode.BadRequest, "InvalidImageFormat")]
    public async Task Provider_failures_are_errors_not_placeholder_results(HttpStatusCode status, string expected)
    {
        var h = new FakeHandler(status, """{"error":{"code":"X","message":"InvalidImageFormat: bad"}}""");
        var ex = await Assert.ThrowsAsync<SensoException>(() => Client(h).ReadAsync(Samples.Png, "image/png", default));
        Assert.Equal(SensoErrorCode.Provider, ex.Code);
        Assert.Contains(expected, ex.Message);
        Assert.DoesNotContain("test-vision-key", ex.Message);
    }

    [Fact]
    public async Task Unparseable_success_body_is_a_provider_error()
    {
        var h = new FakeHandler(HttpStatusCode.OK, "<html>oops</html>");
        await Assert.ThrowsAsync<SensoException>(() => Client(h).ReadAsync(Samples.Png, "image/png", default));
    }

    [Fact]
    public async Task Cancellation_propagates_to_the_http_call()
    {
        var h = new FakeHandler(_ => throw new OperationCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(h).ReadAsync(Samples.Png, "image/png", new CancellationToken(true)));
    }

    [Fact]
    public async Task Service_applies_redaction_to_text_and_blocks_and_can_omit_layout()
    {
        var path = Samples.TempFile("doc.png", Samples.Png);
        var h = new FakeHandler(HttpStatusCode.OK, Samples.VisionResponse);
        var svc = new OcrService(new MediaInputResolver(Samples.Options()), Client(h));

        var redacted = await svc.RecognizeAsync(path, null, includeLayout: true, redactPii: true, default);
        Assert.DoesNotContain("jane@example.com", redacted.Text);
        Assert.All(redacted.Blocks!, b => Assert.DoesNotContain("jane@example.com", b.Text));
        Assert.True(redacted.PiiRedacted);
        Assert.Contains(redacted.Warnings!, w => w.Contains("best-effort"));

        var plain = await svc.RecognizeAsync(path, null, includeLayout: false, redactPii: false, default);
        Assert.Contains("jane@example.com", plain.Text);
        Assert.Null(plain.Blocks);
        Assert.Null(plain.Warnings);
    }

    [Fact]
    public async Task Service_does_not_call_provider_when_input_is_invalid()
    {
        var h = new FakeHandler(HttpStatusCode.OK, Samples.VisionResponse);
        var svc = new OcrService(new MediaInputResolver(Samples.Options()), Client(h));
        await Assert.ThrowsAsync<SensoException>(() => svc.RecognizeAsync(null, Convert.ToBase64String(Samples.Garbage), true, false, default));
        Assert.Empty(h.Requests);
    }
}
