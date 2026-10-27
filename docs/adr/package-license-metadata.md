---
date: 2026-10-21
---

# Package license metadata

## Status

Accepted.

## Context

Dependency review reports custom or unknown identifiers for six packages.
The reviewed package declarations identify their supplied licenses.
Its dependency exceptions compare package names without checking versions.
Unconditional exceptions would also admit an unreviewed future version.

## Decision

Bind the six metadata corrections to the combined hash of every NuGet lock file in [CI](../../.github/workflows/ci.yml).
Keep the normal approved licenses for every other dependency.
A changed graph clears these exceptions and requires a new review.

`Microsoft.Windows.SDK.BuildTools` version `10.0.28000.2705` supplies the existing [Windows SDK terms](https://aka.ms/WinSDKLicenseURL) through an unchanged license URL.
The Windows App SDK terms cover `Microsoft.WindowsAppSDK.Foundation` 2.3.12, `Microsoft.WindowsAppSDK.InteractiveExperiences` 2.1.9, `Microsoft.WindowsAppSDK.Runtime` 2.5.1, and `Microsoft.WindowsAppSDK.WinUI` 2.3.9.

The October 27, 2026 graph review updates the icon generator to the `Magick.NET-Q16-AnyCPU` 15.0.0 package.
Its [exact NuGet declaration](https://api.nuget.org/v3-flatcontainer/magick.net-q16-anycpu/15.0.0/magick.net-q16-anycpu.nuspec) specifies `Apache-2.0`, matching the previous 14.17.2 declaration.
The other five corrections keep their reviewed versions and terms.
The final graph includes the accepted xUnit test dependency update.

The official GitHub glob implementation computes `91a90a1a2eff6bd3d8778101ca4ff11424f89d6591d622ae52cb8f83bd9807b5` for all nine checked-out NuGet lock files.
The workflow uses that exact fingerprint and logs the computed value for review.
Any graph mismatch still produces an empty correction list.

The four component packages supply the same `license.txt`, with SHA-256 `5b11e6347756e40fe0274bc08c97f89201b94f0d50181a09a00f1f4740840501`.
That text also ships with the previous runtime, foundation, and interactive packages.
The previous WinUI package carried preview terms.
The new package uses the stable SDK agreement and its distribution conditions.
Those vendor terms continue to govern redistribution.
PDFsharp keeps its separate [6.2 compatibility boundary](pdfsharp-license-compatibility.md).

## Consequences

The reviewed graph can pass despite incomplete registry metadata.
Changed graphs and other packages still receive the normal license checks.
Locked restore and generated notices remain required.
Reject global unknown-license allowances and permanent package-name exceptions.
