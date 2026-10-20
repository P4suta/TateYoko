---
date: 2026-10-21
---

# Pull-request verification

## Status

Accepted.

## Context

Pull requests need reproducible evidence for PDF conversion, app logic, dependencies, and security checks.
The former gate repeated Windows compilation for CodeQL, ran a full mutation campaign, and built both distribution architectures on every change.
Elapsed-time and process-wide allocation assertions also depended on runner conditions.

## Decision

Share the existing verification build with C# CodeQL extraction.
Keep locked restore, licenses, formatting, spelling, strict compilation, contract and rendered tests, coverage, vulnerability auditing, notices, and release-tool tests.
Keep the current required status and its security dependencies.
Run this gate on pull requests and merge groups without repeating it on main pushes.

Remove the full mutation campaign and the Scorecard workflow.
Build both distribution architectures in the release workflow.
Keep the thousand-page conversion check for page count and output size, without clock or process-wide allocation verdicts.
Use the native global.json SDK source and a prebuilt commit-message checker.

## Consequences

Pull requests need one Windows verification build.
The gate still checks rendered output and rejects dependency, license, and source-analysis findings.
Release packaging, signing, and publication keep their separate gates.

Keeping full campaigns on demand would preserve an unused tool and its maintenance burden.
Removing rendered or structural checks would weaken the conversion contract.
Both alternatives are rejected.
