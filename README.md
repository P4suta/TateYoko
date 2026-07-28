<div align="center">

<img src="assets/AppIcon.png" width="104" height="104" alt="TateYoko app icon" />

# TateYoko

**Turn vertical-writing PDFs into natural right-to-left spreads for wide screens.**

[![CI](https://github.com/P4suta/TateYoko/actions/workflows/ci.yml/badge.svg)](https://github.com/P4suta/TateYoko/actions/workflows/ci.yml)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/P4suta/TateYoko/badge)](https://scorecard.dev/viewer/?uri=github.com/P4suta/TateYoko)
[![REUSE status](https://api.reuse.software/badge/github.com/P4suta/TateYoko)](https://api.reuse.software/info/github.com/P4suta/TateYoko)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
![Windows 11](https://img.shields.io/badge/Windows%2011-x64%20%7C%20ARM64-0078D6?logo=windows11&logoColor=white)

</div>

## What it does

TateYoko places two portrait pages side by side in right-to-left reading order.
Drop one PDF, choose how page 1 should start, and select a destination.

```text
Input:  1, 2, 3, 4, 5
Output: [ 2 | 1 ] [ 4 | 3 ] [ blank | 5 ]
```

Page 1 can:

- start on the right;
- remain alone as a cover; or
- start on the left with a blank right half.

Mixed page sizes, odd page counts, rotations, crop boxes, and encrypted PDFs are
supported. Every conversion opens the Windows Save dialog with
`<source>_spread.pdf` as the suggested name. Existing files are replaced only after
Windows confirms the overwrite and TateYoko verifies the complete temporary PDF.

The product contract is deliberately narrow: TateYoko preserves the static visual
appearance of self-scanned pages. It does not rebuild OCR, bookmarks, links, page
labels, or input metadata. Forms, signatures, annotations, attachments, scripts,
page actions, layers, and other active features are rejected before output is
written.

## Install

TateYoko supports Windows 11 on x64 and ARM64. Install
[`TateYoko.appinstaller`](https://github.com/P4suta/TateYoko/releases) to receive
the signed MSIX bundle and Windows-managed updates. The bundle carries .NET and
uses the serviced Windows App SDK framework package.

Every executable release is Authenticode-signed and RFC 3161 timestamped. Releases
also include checksums, runtime and source SBOMs, third-party notices, and GitHub
artifact attestations.

```powershell
gh attestation verify TateYoko.msixbundle --repo P4suta/TateYoko
Get-FileHash TateYoko.msixbundle -Algorithm SHA256
Get-AuthenticodeSignature TateYoko.msixbundle
```

## Privacy and exposed surface

Conversion is local and offline. The app has no telemetry, advertising, accounts,
cloud storage, or application-controlled network traffic. Windows alone checks for
AppInstaller updates after the user installs that distribution.

The user selects one input and one output through Windows pickers. TateYoko exposes
no file association, protocol, Explorer command, or command-line input. PDF
passwords exist only during the active conversion and are never persisted or
logged. Diagnostic logs contain neither file paths nor PDF content.

## Design

There is no public API. `TateYoko.Engine` is an internal static-page conversion
implementation referenced only by the app and tests. `TateYoko.App` owns the WinUI
state machine and operating-system integration.

The converter writes a unique temporary file beside the selected destination,
reopens it to verify page count, dimensions, and encryption, then commits it
atomically. Cancellation and failures preserve the previous destination.

## Develop

The .NET SDK, command runner, analyzers, REUSE, spelling checker, and release tools
are version-pinned.

```powershell
mise install
mise exec -- just restore
mise exec -- just ci
mise exec -- just run
mise exec -- just dist-build 0.1.0
```

`just run` requires Windows Developer Mode. Pass its process ID to
`mise exec -- just ui-test <PID>` for keyboard, picker, accessibility, and visual
state checks. Evidence is written to `build/ui-tests/`.

`just ci` enforces locked dependencies, REUSE 3.3, spelling and formatting, a
warning-free build, tests, line and branch coverage, vulnerability auditing,
third-party notices, and mutation testing. Poppler golden tests verify rendered
pixels, and a 1,000-page fixture enforces time, memory, and output-size budgets.

See [CONTRIBUTING.md](CONTRIBUTING.md) and
[docs/RELEASING.md](docs/RELEASING.md).

## License

TateYoko is licensed under [Apache-2.0](LICENSE). File-level copyright and license
data live in [REUSE.toml](REUSE.toml) and [LICENSES](LICENSES/).
