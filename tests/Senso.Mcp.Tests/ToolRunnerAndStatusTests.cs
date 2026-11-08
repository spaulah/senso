using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using Senso.Mcp.Resources;
using Senso.Mcp.Tools;
using Xunit;

namespace Senso.Mcp.Tests;

public class ToolRunnerAndStatusTests
{
    private static ToolRunner Runner() => new(Samples.Options(), NullLogger<ToolRunner>.Instance);

    [Fact]
    public async Task Returns_value_on_success() =>
        Assert.Equal(7, await Runner().RunAsync("t", null, default, _ => Task.FromResult(7)));

    [Fact]
    public async Task Senso_errors_become_mcp_errors_with_actionable_message()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            Runner().RunAsync<int>("t", null, default, _ => throw new SensoException(SensoErrorCode.Configuration, "set the key")));
        Assert.Equal("set the key", ex.Message);
    }

    [Fact]
    public async Task Timeout_is_reported_as_timeout()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            Runner().RunAsync<int>("slow", 1, default, async ct => { await Task.Delay(TimeSpan.FromSeconds(30), ct); return 1; }));
        Assert.Contains("timed out", ex.Message);
    }

    [Fact]
    public async Task Client_cancellation_propagates_as_cancellation()
    {
        using var cts = new CancellationTokenSource();
        var task = Runner().RunAsync<int>("slow", null, cts.Token, async ct => { await Task.Delay(TimeSpan.FromSeconds(30), ct); return 1; });
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Unexpected_exceptions_do_not_leak_their_message()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            Runner().RunAsync<int>("t", null, default, _ => throw new IOException("secret path /home/me/private.png")));
        Assert.DoesNotContain("private.png", ex.Message);
    }

    [Fact]
    public async Task Network_failures_are_reported_without_internal_detail()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            Runner().RunAsync<int>("t", null, default, _ => throw new HttpRequestException("dns failure for secret.host")));
        Assert.Contains("could not reach", ex.Message);
        Assert.DoesNotContain("secret.host", ex.Message);
    }

    [Fact]
    public void Status_reports_real_configuration_and_never_secrets()
    {
        var json = StatusResource.Build(Samples.Options().Value);
        Assert.DoesNotContain("test-vision-key", json);
        Assert.DoesNotContain("test-speech-key", json);
        Assert.DoesNotContain("example.test", json);
        Assert.Contains("\"remoteUrls\": false", json);
        Assert.Contains("\"ocr_image\"", json);
        Assert.Contains("\"transcribe_audio\"", json);
        Assert.DoesNotContain("sharepoint", json, StringComparison.OrdinalIgnoreCase);

        var unconfigured = StatusResource.Build(new SensoOptions());
        Assert.Contains("\"configured\": false", unconfigured);
    }
}
