# Senso

**Senses for your AI client: read images, hear audio.**

Senso is a small [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server that gives an AI
client two abilities:

| Tool | What it does |
|---|---|
| `ocr_image` | Extract text from an image (printed or handwritten), with line/word bounding boxes and confidence. |
| `transcribe_audio` | Transcribe audio with speaker labels and timestamped segments; output as JSON, plain text, SRT or WebVTT. |

I spent many years working with OCR in document management systems. Senso is the lean, focused MCP
server I wanted from that experience: OCR and speech recognition in one place, local-first, with nothing
else bolted on.

> **Status: early (0.1.0).** Unit and mocked-HTTP tests pass, and the stdio protocol layer has been
> smoke-tested. It has **not** yet been verified against live Azure services. See [Limitations](#limitations).

## How it works

Senso runs locally and talks to your MCP client over **stdio**. It reads files from your machine and
sends the media to **your own Azure resources**:

- OCR → [Azure AI Vision Image Analysis 4.0](https://learn.microsoft.com/azure/ai-services/computer-vision/how-to/call-analyze-image-40) (`features=read`, API `2024-02-01`)
- Speech → an [Azure OpenAI](https://learn.microsoft.com/azure/ai-foundry/openai/) audio transcription deployment (default model name `gpt-4o-transcribe-diarize`)

## Data handling and cost

- **Your images and audio are uploaded to Azure** when you call a tool. Nothing is sent anywhere else.
  Review your Azure data-handling terms before using sensitive media.
- **Azure usage is billed to your subscription.** Both calls are paid API requests; check current pricing.
- Senso stores nothing and has no telemetry. Logs go to stderr and contain only tool names, error
  categories and sizes, never media, text, file paths or keys.
- Keys are read from environment variables. Don't put them in files that get committed.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An Azure AI Vision (or multi-service Azure AI) resource: its endpoint and key
- An Azure OpenAI resource with an audio transcription deployment: the deployment's full URL and key

### Azure setup

1. **Vision:** create an *Azure AI Vision* resource in the Azure portal. Copy its **Endpoint** and a **Key**
   (Keys and Endpoint blade). Use a region where Image Analysis 4.0 Read is available.
2. **Speech:** in your Azure OpenAI / Foundry resource, deploy an audio transcription model (for speaker
   labels use a diarization-capable model such as `gpt-4o-transcribe-diarize`, if available in your region).
   Build the endpoint as
   `https://<resource>.openai.azure.com/openai/deployments/<deployment>/audio/transcriptions?api-version=<version>`,
   using an api-version your resource supports.

## Build and run

```bash
git clone https://github.com/spaulah/senso.git
cd senso
dotnet build Senso.sln
dotnet test Senso.sln          # no Azure credentials needed
```

Configuration is by environment variable (see [`.env.example`](.env.example) for every setting):

```bash
export Senso__Vision__Endpoint="https://<your-vision-resource>.cognitiveservices.azure.com"
export Senso__Vision__Key="<your-vision-key>"
export Senso__Speech__Endpoint="https://<your-openai-resource>.openai.azure.com/openai/deployments/<deployment>/audio/transcriptions?api-version=<api-version>"
export Senso__Speech__Key="<your-openai-key>"
dotnet run --project src/Senso.Mcp
```

You normally won't run it by hand; your MCP client launches it.

## MCP client configuration

Build once (`dotnet build -c Release Senso.sln`), then register the server. Replace the path and
placeholders.

**Claude Code**

```bash
claude mcp add senso \
  --env Senso__Vision__Endpoint=https://<vision-resource>.cognitiveservices.azure.com \
  --env Senso__Vision__Key=<vision-key> \
  --env Senso__Speech__Endpoint="https://<openai-resource>.openai.azure.com/openai/deployments/<deployment>/audio/transcriptions?api-version=<api-version>" \
  --env Senso__Speech__Key=<openai-key> \
  -- dotnet /absolute/path/to/senso/src/Senso.Mcp/bin/Release/net10.0/senso-mcp.dll
```

**Claude Desktop and other clients using a JSON config**

```json
{
  "mcpServers": {
    "senso": {
      "command": "dotnet",
      "args": ["/absolute/path/to/senso/src/Senso.Mcp/bin/Release/net10.0/senso-mcp.dll"],
      "env": {
        "Senso__Vision__Endpoint": "https://<vision-resource>.cognitiveservices.azure.com",
        "Senso__Vision__Key": "<vision-key>",
        "Senso__Speech__Endpoint": "https://<openai-resource>.openai.azure.com/openai/deployments/<deployment>/audio/transcriptions?api-version=<api-version>",
        "Senso__Speech__Key": "<openai-key>",
        "Senso__AllowedDirectories__0": "/absolute/path/to/a/folder/of/media"
      }
    }
  }
}
```

You only need to configure the capability you use; the other tool returns a clear "not configured" error.

## Tools

Provide **exactly one** of `filePath` (absolute local path) or `base64Data` (base64 or a `data:` URL).

### `ocr_image`

| Parameter | Default | Notes |
|---|---|---|
| `filePath` / `base64Data` | | Image input. JPEG, PNG, GIF, BMP, WebP, TIFF. Max 20 MB. |
| `includeLayout` | `true` | Include line and word blocks with `bbox` `[x, y, width, height]` in pixels. |
| `redactPii` | `false` | Best-effort masking. See [Limitations](#limitations). |
| `timeoutSeconds` | 120 | Max 900. |

Illustrative output (shape only):

```json
{
  "text": "Hello World\nmail me: [REDACTED]",
  "blocks": [
    { "type": "line", "text": "Hello World", "bbox": [10, 20, 100, 20], "confidence": 0.98 },
    { "type": "word", "text": "Hello", "bbox": [10, 20, 40, 20], "confidence": 0.99 }
  ],
  "modelVersion": "2024-02-01",
  "imageWidth": 200,
  "imageHeight": 100,
  "piiRedacted": true,
  "warnings": ["PII redaction is best-effort pattern matching and may miss personal data."]
}
```

Azure reports confidence per word; a line's confidence is the mean of its words.

### `transcribe_audio`

| Parameter | Default | Notes |
|---|---|---|
| `filePath` / `base64Data` | | Audio input. WAV, MP3, M4A, OGG, FLAC, WebM. Max 25 MB (Azure OpenAI's upload limit). |
| `language` | auto-detect | e.g. `en`, `es-MX` (reduced to the two-letter code). |
| `format` | `json` | `json` (text + segments), `text`, `srt`, `vtt`. |
| `timeoutSeconds` | 120 | Max 900. |

Illustrative `json` output:

```json
{
  "format": "json",
  "text": "Hi there. Hello.",
  "segments": [
    { "speaker": "A", "startSeconds": 0.0, "endSeconds": 1.5, "text": "Hi there." },
    { "speaker": "B", "startSeconds": 1.5, "endSeconds": 3.25, "text": "Hello." }
  ]
}
```

With `format: "srt"` or `"vtt"` the result has a `subtitles` field instead:

```
1
00:00:00,000 --> 00:00:01,500
A: Hi there.
```

### Resource: `senso://status`

Reports version, which tools are configured, limits and supported formats. It never includes keys or endpoints.

## Safety model

- **No remote URLs.** Only local files and base64 are accepted, so Senso can't be steered into fetching
  loopback, private-network or cloud-metadata addresses.
- Files are accepted only if the extension is a supported one **and** the content signature matches an
  image/audio format. Size limits are checked before reading or decoding.
- Set `Senso__AllowedDirectories__N` to restrict which directories file inputs may come from (symlinks
  are resolved). **Without it, the AI client can ask Senso to read any supported media file you can
  read**, so setting it is recommended.
- Provider endpoints must be `https`.
- Failures are returned as MCP tool errors (`isError`), never as fabricated results.

## Limitations

- **Not yet tested against live Azure.** Request/response handling follows Microsoft's documentation and
  is covered by mocked tests only. Azure's documentation for diarized transcription output is thin and has
  varied by api-version; if your deployment returns no `segments`, `srt`/`vtt` will fail with a clear
  error and `json` will warn. `Senso__Speech__ResponseFormat` and `ChunkingStrategy` are configurable for
  that reason.
- **Azure only.** One provider per capability; no provider abstraction beyond the internal interfaces.
- **Images only for OCR**: no PDF page processing. Azure AI Vision Image Analysis 4.0 is documented by
  Microsoft as scheduled for retirement on 2028-09-25.
- **No speaker identification** (matching voices to known people). Labels such as "A"/"B" separate voices
  within one file only.
- OCR language is auto-detected; there is no language hint or handwriting toggle (Read handles both).
- **PII redaction is best-effort regex matching** of emails, card-like numbers, US SSN-like numbers and
  phone-like numbers. It misses names, addresses, non-US formats and OCR-garbled values, and can mask
  harmless numbers. It is not a compliance or privacy guarantee. It applies to OCR output only.
- Whole files are held in memory; the size limits keep this bounded.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `OCR is not configured` / `Transcription is not configured` | Missing `Senso__Vision__*` / `Senso__Speech__*` variables in the environment your MCP client passes to the server. |
| `HTTP 401/403` | Wrong key, or key doesn't belong to the endpoint's resource. |
| `HTTP 404` from speech | Wrong deployment name in the URL, or an `api-version` the resource doesn't support. |
| `HTTP 429` | Azure rate limiting; retry later. |
| `Unrecognized image/audio content` | The file isn't one of the supported formats, even if its extension looks right. |
| `outside the directories permitted` | The path isn't under `Senso__AllowedDirectories__N`. |
| `timed out` | Raise `timeoutSeconds` (max 900) for long audio. |
| Client shows no tools | Run the `dotnet` command by hand to check for startup errors (written to stderr). The path must point at the built `senso-mcp.dll`. |

## Project layout

```
src/Senso.Mcp/       the server (Input/, Ocr/, Speech/, Tools/, Resources/)
tests/Senso.Mcp.Tests/   unit + mocked-HTTP tests (no credentials, no network)
.env.example         configuration template (placeholders only)
```

## License

[MIT](LICENSE). See also [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md).
