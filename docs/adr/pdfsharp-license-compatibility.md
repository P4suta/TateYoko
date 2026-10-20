---
date: 2026-10-21
---

# PDFsharp license compatibility

## Status

Accepted.

## Context

TateYoko uses PDFsharp under its `MIT` license.
[PDFsharp 6.5](https://docs.pdfsharp.net/General/License/License.html) changes to `AGPL-3.0-only` or a commercial license and requires explicit license initialization.
That update stops conversion before the existing contract and rendered tests can run.

## Decision

Keep PDFsharp on its `MIT`-licensed 6.2 line and constrain Renovate to that line.
Keep locked restore, conversion tests, vulnerability auditing, and generated notices as release prerequisites.
Review a replacement or a different license as a separate decision.

## Consequences

Dependency updates preserve the current conversion and distribution terms.
Updates outside the 6.2 line require a deliberate compatibility and license review.
Enabling the new license implicitly or suppressing its runtime checks would change the distribution boundary and is rejected.
