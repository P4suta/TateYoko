# Contributing to TateYoko

Thanks for your interest! TateYoko is a small, single-purpose tool — please read
the scope note in the [feature request template](.github/ISSUE_TEMPLATE/feature_request.yml)
before proposing new features.

## Setup

The toolchain is pinned with [mise](https://mise.jdx.dev/) (`.NET 10` + [`just`](https://just.systems)):

```sh
mise install
```

Developer commands live in the `justfile` — the single source of truth shared by local dev and
CI. Run them under mise: `mise exec -- just <recipe>` (or `just <recipe>` with mise activated).
`mise exec -- just --list` shows everything.

Building the release bundle also needs **Visual Studio C++ build tools** (the
launcher is NativeAOT): `winget install Microsoft.VisualStudio.2022.BuildTools`,
then add "Desktop development with C++".

## Development loop

```sh
mise exec -- just test        # Core unit + Pdf integration + ViewModel state machine tests
mise exec -- just run         # run the app (unpackaged)
mise exec -- just fmt         # format the code in place (.editorconfig rules)
mise exec -- just publish     # assemble the distribution bundle
mise exec -- just icons       # regenerate icon assets from assets/AppIcon.png
mise exec -- just ci          # what CI enforces: format check + tests
```

## Architecture rules

TateYoko is a **hexagonal** design (see the README). The rules that matter:

- **`TateYoko.Core` depends on neither PDFsharp nor WinUI.** The domain is pure;
  PDF work lives in `TateYoko.Pdf`, UI in `TateYoko.App`. Don't add a PDF or UI
  reference to `Core` — put the logic behind a port instead.
- **`TateYoko.Presentation` depends only on `Core`** (plus CommunityToolkit.Mvvm),
  never on WinUI or PDFsharp. Platform concerns reach it through the abstractions
  (`IUiDispatcher` / `IUiStrings` / `IShellLauncher`), whose WinUI adapters live in
  `TateYoko.App`. This keeps the view-model state machine unit-testable off the UI thread.

## Pull requests

- Use [Conventional Commits](https://www.conventionalcommits.org/) for the PR title
  (`feat:`, `fix:`, `perf:`, `docs:`, `chore:`, …). We squash-merge, so the title
  becomes the commit message and drives the automated version bump + CHANGELOG
  (release-please — see [docs/RELEASING.md](docs/RELEASING.md)).
- Make sure `just ci` (format check + `just test`) passes and the Core boundary is intact.

## Code of Conduct

This project follows the [Contributor Covenant](.github/CODE_OF_CONDUCT.md).
