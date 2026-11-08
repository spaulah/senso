# Security Policy

## Supported versions

Senso is pre-1.0. Only the latest release / `main` receives security fixes.

## Reporting a vulnerability

Please **do not** open a public issue. Report privately using GitHub's
"Report a vulnerability" feature (Security tab → Advisories) on this repository.
Include a description, reproduction steps, and the impact you see. Please do not include real
credentials or sensitive media in the report.

You can expect an acknowledgement within a few days. This is a volunteer-maintained project, so there is
no guaranteed response or fix time.

## Security model, in brief

- Runs locally over stdio; it has no network listener and **no authentication or OAuth**, because it
  trusts the local MCP client that launches it.
- Sends images and audio you provide to **your own Azure resources** over HTTPS (endpoints must be `https`).
- Does **not** fetch remote URLs. Inputs are local files or base64, checked by extension, content
  signature and size. `Senso__AllowedDirectories__N` can restrict readable directories; without it,
  the connected MCP client can have Senso read any supported media file you can read.
- Credentials come from environment variables and are never logged or returned by the status resource.
- PII redaction is best-effort and is not a privacy guarantee.

Things that are in scope for reports: path or allow-list bypasses, credential leakage in logs or
output, SSRF or unintended outbound requests, resource exhaustion that bypasses the size limits.

Out of scope: vulnerabilities in Azure services themselves, or in a misconfigured MCP client.
