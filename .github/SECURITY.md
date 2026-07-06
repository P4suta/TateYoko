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

TateYoko is a local, offline desktop app: it reads a PDF you choose and writes a
new PDF next to it. It makes no network connections and stores no credentials, so
the attack surface is small. Examples of in-scope reports:

- A crafted/malicious PDF that leads to code execution, path traversal, or writing
  outside the intended output location when opened or converted
- Tampering with the release bundle or its signature/attestation chain (see
  [`docs/SIGNING.md`](../docs/SIGNING.md))

Out of scope: denial of service from a deliberately malformed PDF that merely fails
to convert (it should fail gracefully — a crash report is a normal bug, not a
security issue).
