namespace Senso.Mcp;

public enum SensoErrorCode
{
    Configuration,
    InvalidInput,
    Provider,
    Timeout
}

/// <summary>An error whose message is safe and useful to show to the MCP client.</summary>
public sealed class SensoException : Exception
{
    public SensoErrorCode Code { get; }

    public SensoException(SensoErrorCode code, string message, Exception? inner = null)
        : base(message, inner) => Code = code;
}
