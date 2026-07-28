# Contributing to TateYoko

TateYoko has one job: convert one vertical-writing PDF into right-to-left spreads
without surprises. Prefer a shorter, safer interaction over a broader feature set.

## Set up

Use Windows 11:

```powershell
mise install
mise exec -- just restore
mise exec -- lefthook install
mise exec -- just ci
```

`mise.toml` pins the host tools. `.config/dotnet-tools.json` pins the .NET tools.
`just restore` refreshes the normal and MSIX ReadyToRun lock files. CI and releases
use `just restore-locked` and never resolve an unreviewed dependency graph.

Rendered golden tests download Poppler 26.02.0 to `build/tools/` and verify both
the release archive and executable SHA-256 values. Running the packaged app requires
Windows Developer Mode.

## Design constraints

- Do not add a public API. The engine is internal to the app and tests.
- Keep PDFsharp types, PDF I/O, verification, and atomic commit inside the engine.
- Keep the app as one explicit state machine. Do not add a service locator, generic
  dependency-injection container, or navigation framework.
- Put user-facing text in the English, Japanese, and Simplified Chinese resources.
- Treat capabilities, file associations, protocols, execution aliases, dependencies,
  logging, persistence, and network traffic as security-boundary changes.
- Enforce testable constraints in code, tests, or CI. Documentation is not a
  substitute for implementation.

## Verify a change

```powershell
mise exec -- just fmt
mise exec -- just reuse-lint
mise exec -- just typos-check
mise exec -- just build
mise exec -- just test
mise exec -- just coverage
mise exec -- just mutation
mise exec -- just run
```

Changes to rendering need a Poppler golden test. State changes need ViewModel tests.
XAML changes need UI Automation and Light, Dark, and High Contrast screenshots.

## Pull requests

- Use a Conventional Commit title; the squash title drives the changelog and version.
- Describe the user-visible outcome and attach relevant test or visual evidence.
- Review lock files, `REUSE.toml`, SBOMs, and notices as intentional dependency or
  legal changes.
- For third-party files, add the actual copyright holder, SPDX expression, and full
  license text under `LICENSES/` in the same pull request.
- Packaging changes require x64 and ARM64 MSIX, bundle, signature, and AppInstaller
  smoke tests.

## Releases

`just dist-build` may create unsigned release inputs. `just package` fails until
signatures, timestamps, both SBOMs, and notices are valid. See
[docs/RELEASING.md](docs/RELEASING.md) and [docs/SIGNING.md](docs/SIGNING.md).

## Code of Conduct

Follow the [Contributor Covenant](.github/CODE_OF_CONDUCT.md).
