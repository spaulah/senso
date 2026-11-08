namespace Senso.Mcp;

/// <summary>Root configuration, bound from the "Senso" section (env vars: Senso__Vision__Key, ...).</summary>
public sealed class SensoOptions
{
    public const string SectionName = "Senso";

    public VisionOptions Vision { get; set; } = new();
    public SpeechOptions Speech { get; set; } = new();
    public LimitsOptions Limits { get; set; } = new();

    /// <summary>
    /// Optional allow-list of directories that file inputs may be read from.
    /// When empty, any readable file with a supported extension and matching content signature is allowed.
    /// </summary>
    public List<string> AllowedDirectories { get; set; } = new();
}

public sealed class VisionOptions
{
    /// <summary>Azure AI Vision resource endpoint, e.g. https://&lt;resource&gt;.cognitiveservices.azure.com</summary>
    public string? Endpoint { get; set; }
    public string? Key { get; set; }
    /// <summary>Image Analysis 4.0 REST API version.</summary>
    public string ApiVersion { get; set; } = "2024-02-01";
}

public sealed class SpeechOptions
{
    /// <summary>
    /// Full Azure OpenAI audio transcription URL for your deployment, including api-version, e.g.
    /// https://&lt;resource&gt;.openai.azure.com/openai/deployments/&lt;deployment&gt;/audio/transcriptions?api-version=...
    /// </summary>
    public string? Endpoint { get; set; }
    public string? Key { get; set; }
    /// <summary>Value sent as the "model" form field (your deployment/model name).</summary>
    public string Model { get; set; } = "gpt-4o-transcribe-diarize";
    /// <summary>"response_format" form field. Speaker labels need a diarization-capable format.</summary>
    public string ResponseFormat { get; set; } = "diarized_json";
    /// <summary>"chunking_strategy" form field; set to empty to omit.</summary>
    public string? ChunkingStrategy { get; set; } = "auto";
}

public sealed class LimitsOptions
{
    public int MaxImageSizeMb { get; set; } = 20;
    /// <summary>Azure OpenAI audio uploads are limited to 25 MB.</summary>
    public int MaxAudioSizeMb { get; set; } = 25;
    public int DefaultTimeoutSeconds { get; set; } = 120;
    public int MaxTimeoutSeconds { get; set; } = 900;
}
