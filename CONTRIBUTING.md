# Contributing to Senso

Thanks for your interest. Senso is intentionally small: OCR and speech transcription as an MCP server.
Changes that keep it lean and honest are the most welcome.

## Scope

In scope: improving the two tools, Azure provider correctness, safety of input handling, tests, docs.

Please open an issue before starting work on anything larger, in particular:
new providers, PDF processing, speaker identification, remote URL fetching, or hosted/HTTP deployment.
These were left out on purpose (see the README's Limitations and Safety model).

## Setup

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build Senso.sln
dotnet test Senso.sln
```

Tests use mocked HTTP and need **no Azure credentials and no network**. Please keep it that way.

## Guidelines

- **Don't claim what isn't implemented and tested.** Tool descriptions, the status resource and the README
  must match behavior. Remove or clearly label unsupported options rather than accepting them silently.
- **Errors, not placeholders.** Provider or configuration failures must surface as errors, never as
  success-shaped results.
- **Add tests** for new behavior (mapping, validation, limits, cancellation, formatting). Provider
  interactions are tested with a fake `HttpMessageHandler`; see `tests/Senso.Mcp.Tests/TestSupport.cs`.
- **Keep secrets and sensitive data out**: no keys, endpoints, tenant/subscription IDs, internal URLs,
  or real images/audio in code, tests, docs, issues or logs. Use obvious placeholders and synthetic samples.
- **Logging**: stdout is the MCP channel; log to stderr only, and never log media, extracted text,
  file paths or credentials.
- Match the existing code style; keep nullable annotations clean and the build warning-free.

## Pull requests

1. Fork and branch from `main`.
2. Keep the change focused; update the README if behavior changes.
3. Make sure `dotnet build` and `dotnet test` pass.
4. Describe what changed and how you verified it. Live-Azure results are valuable; redact anything sensitive.

By contributing you agree that your contribution is licensed under the project's MIT license.

## Security issues

Do not open a public issue for vulnerabilities. See [SECURITY.md](SECURITY.md).
