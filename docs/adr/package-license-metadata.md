---
date: 2026-10-21
---

# Package license metadata

## Status

Accepted.

## Context

Dependency review reports incomplete license metadata for six packages.
Its exceptions match package names across versions.

## Decision

Bind the six corrections to the combined hash of every NuGet lock file in [CI](../../.github/workflows/ci.yml).

`Microsoft.Windows.SDK.BuildTools` versions `10.0.28000.2705` and `10.0.28000.2957` declare the same [Windows SDK terms](https://aka.ms/WinSDKLicenseURL).
The Windows App SDK agreement covers the Foundation, InteractiveExperiences, Runtime, and WinUI packages at version 2.7.0.

The icon generator uses `Magick.NET-Q16-AnyCPU` 15.1.0.
Versions 15.0.0 and 15.1.0 both declare `Apache-2.0` in the NuGet manifest.

The nine NuGet lock files have the combined SHA-256 digest `12c72467944c5b85b204617ab0aa9259d21758004cf1743335b04c58e5727a3f`.
The workflow logs the digest and enables the six corrections only for the reviewed graph.

The four component packages supply the same `license.txt`, with SHA-256 `5b11e6347756e40fe0274bc08c97f89201b94f0d50181a09a00f1f4740840501`.
The four previously reviewed versions carry identical license text.
The SDK agreement continues to govern redistribution.
PDFsharp keeps its separate [6.2 compatibility boundary](pdfsharp-license-compatibility.md).

## Alternatives

A permanent package exception would admit future versions with different terms.

## Consequences

Each dependency graph change requires another review.

## Confirmation

CI checks the lock digest, restores locked packages, and generates third-party notices.
