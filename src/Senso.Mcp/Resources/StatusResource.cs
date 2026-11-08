using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Senso.Mcp.Resources;

[McpServerResourceType]
public sealed class StatusResource
{
    private readonly SensoOptions _options;

    public StatusResource(IOptions<SensoOptions> options) => _options = options.Value;

    [McpServerResource(UriTemplate = "senso://status", Name = "Senso status", MimeType = "application/json")]
    [Description("Server version and which capabilities are configured. Never includes keys or endpoints.")]
    public string Status() => Build(_options);

    internal static string Build(SensoOptions o)
    {
        var version = typeof(StatusResource).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0];
        var status = new
        {
            name = "senso",
            version,
            capabilities = new
            {
                ocr_image = new
                {
                    provider = "Azure AI Vision Image Analysis 4.0 (Read)",
                    configured = !string.IsNullOrWhiteSpace(o.Vision.Endpoint) && !string.IsNullOrWhiteSpace(o.Vision.Key),
                    maxImageSizeMb = o.Limits.MaxImageSizeMb,
                    formats = Input.MediaSniffer.ImageFormats
                },
                transcribe_audio = new
                {
                    provider = "Azure OpenAI audio transcription",
                    configured = !string.IsNullOrWhiteSpace(o.Speech.Endpoint) && !string.IsNullOrWhiteSpace(o.Speech.Key),
                    maxAudioSizeMb = o.Limits.MaxAudioSizeMb,
                    formats = Input.MediaSniffer.AudioFormats,
                    outputFormats = new[] { "json", "text", "srt", "vtt" }
                }
            },
            inputs = new { filePath = true, base64Data = true, remoteUrls = false },
            fileAccessRestricted = o.AllowedDirectories.Count > 0
        };
        return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
