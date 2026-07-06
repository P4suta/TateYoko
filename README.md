# 縦横 (TateYoko)

**A Windows app that merges a vertical-writing PDF two pages at a time into right-bound (RTL) spreads.**

[![CI](https://github.com/P4suta/TateYoko/actions/workflows/ci.yml/badge.svg)](https://github.com/P4suta/TateYoko/actions/workflows/ci.yml)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/P4suta/TateYoko/badge)](https://scorecard.dev/viewer/?uri=github.com/P4suta/TateYoko)

It takes a PDF of scanned portrait pages from a vertically written book and lays out two pages side
by side on one landscape page. Reading order is right-to-left (RTL), so any PDF viewer's spread view
shows the pages in the correct order. A single-purpose tool for comfortable reading on a wide screen.

## Usage

1. Launch the app and **drop a vertical-writing PDF anywhere on the window** (or click "Choose file").
2. Choose how the first page opens (from the right / cover alone / from the left).
3. Click "Make spread". `<name>_spread.pdf` is written next to the input.

## Architecture

A hexagonal design in three projects. Dependencies point inward (`Core` depends on neither PDF nor UI).

```
TateYoko.Core   Pure domain + use cases (PageDimension / Pagination /
                SpreadLayoutCalculator / SpreadConversionService). No PDF/UI dependency.
TateYoko.Pdf    Infrastructure. Implements Core's ports with PDFsharp (the only layer that depends on PDF).
TateYoko.App    WinUI 3 (unpackaged) + composition root. MVVM (CommunityToolkit.Mvvm) + DI.
```

The boundary is enforced at compile time: Core does not reference PDFsharp.

## Distribution

**Unpackaged / self-contained** (not MSIX; the .NET and Windows App SDK runtimes are bundled).
Copy the folder anywhere and run it — no install required.

To make it obvious which exe to run, the bundle root holds **only a launcher plus a README**, and the
app's ~350 files are confined to `app/`. The root `TateYoko.exe` is a native launcher that starts
`app/TateYoko.App.exe` and forwards its arguments.

```
publish/win-x64/
├─ TateYoko.exe      ← double-click this (launcher)
├─ README.txt
├─ BUILDINFO.txt
└─ app/             ← the app and its runtime (do not touch)
```

## Development

The toolchain is managed with **mise** (`mise install` sets up the .NET SDK).

```sh
mise install
mise exec -- dotnet test TateYoko.slnx          # all tests (Core unit + PDF integration)
mise exec -- dotnet run --project src/TateYoko.App   # run in development (unpackaged)
mise run publish                                 # assemble the distribution bundle into publish/
mise run icons                                   # regenerate icon assets from assets/AppIcon.png
```

Release packaging is orchestrated by the C# tool `tools/TateYoko.Pack` rather than a shell script.
`mise run publish` is a thin wrapper over `dotnet run --project tools/TateYoko.Pack`. In addition to
arranging the bundle, it writes a zip and `SHA256SUMS.txt` to `publish/package/`. The launcher is
built with **NativeAOT**, so building requires **Visual Studio C++ build tools** (MSVC linker +
Windows SDK) — install with `winget install Microsoft.VisualStudio.2022.BuildTools` and add
"Desktop development with C++".

## Releases & security

Versioning and releases are automated from [Conventional Commits](https://www.conventionalcommits.org/)
with release-please: merging its Release PR cuts a version, then CI builds a self-contained bundle,
Authenticode-signs the first-party binaries (SSL.com eSigner), attaches keyless build-provenance and a
CycloneDX SBOM, and publishes a signed `.zip` + `SHA256SUMS.txt`. See [docs/RELEASING.md](docs/RELEASING.md)
and [docs/SIGNING.md](docs/SIGNING.md). Report vulnerabilities privately per [SECURITY.md](.github/SECURITY.md).

Verify a download:

```sh
gh attestation verify TateYoko-vX.Y.Z-win-x64.zip --repo P4suta/TateYoko
sha256sum -c SHA256SUMS.txt
```

## Tech stack

| Category | Technology |
|---|---|
| Language / runtime | C# / .NET 10 |
| UI | WinUI 3 (Windows App SDK) — unpackaged / self-contained |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| PDF | PDFsharp 6.x (MIT) |
| Tests | xUnit |

## License

[Apache-2.0](LICENSE)
