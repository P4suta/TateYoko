# Security Policy

## Reporting a vulnerability

Please report security vulnerabilities **privately** via
[GitHub Security Advisories](https://github.com/P4suta/TateYoko/security/advisories/new).
**Do not open a public issue for a vulnerability.**

We aim to acknowledge a report within a few days and to ship a fix or mitigation
as quickly as the severity warrants.

## Supported versions

TateYoko is pre-1.0; only the latest release receives security fixes.

| Version | Supported |
| ------- | --------- |
| latest  | ✅        |
| older   | ❌        |

## Scope

TateYoko is a local, offline desktop app: it reads one PDF you choose and writes a
new PDF either next to it or at the output path you explicitly choose. The app
makes no network connections or telemetry calls.
Windows may contact GitHub only when the user installs the AppInstaller build and
Windows checks for an update. Passwords for encrypted PDFs are held only for the
active conversion and are never persisted or logged. Examples of in-scope reports:

- A crafted/malicious PDF that leads to code execution, path traversal, or writing
  outside the intended output location when opened or converted
- Exposure of PDF paths, contents, or passwords through logs, temporary files, or
  retained UI state
- Tampering with a release artifact or its signature/attestation chain (see
  [`docs/SIGNING.md`](../docs/SIGNING.md))

Out of scope: denial of service from a deliberately malformed PDF that merely fails
to convert (it should fail gracefully — a crash report is a normal bug, not a
security issue).
