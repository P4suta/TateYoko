---
date: 2026-10-21
---

# Package license metadata

## Status

Accepted.

## Context

Dependency review reports custom or unknown identifiers for six packages whose supplied license declarations were reviewed.
Its dependency exceptions compare package names without checking versions.
Unconditional exceptions would also admit an unreviewed future version.

## Decision

Bind the six metadata corrections to the combined hash of every NuGet lock file in [CI](../../.github/workflows/ci.yml).
Keep the normal license allow list for every other dependency.
A changed graph clears these exceptions and requires a new review.

| Package | Version | Supplied license |
| --- | --- | --- |
| `Microsoft.Windows.SDK.BuildTools` | 10.0.28000.2705 | [Windows SDK terms](https://aka.ms/WinSDKLicenseURL), unchanged license URL |
| `Microsoft.WindowsAppSDK.Foundation` | 2.3.12 | Windows App SDK terms |
| `Microsoft.WindowsAppSDK.InteractiveExperiences` | 2.1.9 | Windows App SDK terms |
| `Microsoft.WindowsAppSDK.Runtime` | 2.5.1 | Windows App SDK terms |
| `Microsoft.WindowsAppSDK.WinUI` | 2.3.9 | Windows App SDK terms |
| `Magick.NET-Q16-AnyCPU` | 14.17.2 | `Apache-2.0` |

The four component packages supply the same `license.txt`, with SHA-256 `5b11e6347756e40fe0274bc08c97f89201b94f0d50181a09a00f1f4740840501`.
That text also ships with the previous runtime, foundation, and interactive packages.
The previous WinUI package carried preview terms; the new package uses the stable SDK agreement and its distribution conditions.
Those vendor terms continue to govern redistribution.
PDFsharp keeps its separate [6.2 compatibility boundary](pdfsharp-license-compatibility.md).

## Consequences

The reviewed graph can pass despite incomplete registry metadata.
Changed graphs and other packages still receive the normal license checks.
Locked restore and generated notices remain required.
Global unknown-license allowances and permanent package-name exceptions are rejected.
