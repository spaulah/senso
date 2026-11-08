using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;

namespace Senso.Mcp.Tools;

/// <summary>Applies timeout/cancellation and turns failures into MCP tool errors (never success-shaped placeholders).</summary>
public sealed class ToolRunner
{
    private readonly SensoOptions _options;
    private readonly ILogger<ToolRunner> _logger;

    public ToolRunner(IOptions<SensoOptions> options, ILogger<ToolRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T> RunAsync<T>(string tool, int? timeoutSeconds, CancellationToken ct, Func<CancellationToken, Task<T>> work)
    {
        var limits = _options.Limits;
        var seconds = Math.Clamp(timeoutSeconds ?? limits.DefaultTimeoutSeconds, 1, limits.MaxTimeoutSeconds);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        try
        {
            return await work(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("{Tool} cancelled by client", tool);
            throw; // let the SDK report cancellation
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("{Tool} timed out after {Seconds}s", tool, seconds);
            throw new McpException($"{tool} timed out after {seconds}s. Increase 'timeoutSeconds' (max {limits.MaxTimeoutSeconds}).");
        }
        catch (SensoException ex)
        {
            _logger.LogWarning("{Tool} failed: {Code}", tool, ex.Code);
            throw new McpException(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "{Tool} could not reach the provider", tool);
            throw new McpException($"{tool} could not reach the Azure service ({ex.GetType().Name}). Check network access and the configured endpoint.");
        }
        catch (Exception ex)
        {
            // Log the type only: messages from I/O or parsing can contain paths or content.
            _logger.LogError("{Tool} failed unexpectedly: {Type}", tool, ex.GetType().Name);
            throw new McpException($"{tool} failed unexpectedly ({ex.GetType().Name}).");
        }
    }
}
