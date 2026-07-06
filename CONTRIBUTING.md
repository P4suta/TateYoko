# Contributing to TateYoko

Thanks for your interest! TateYoko is a small, single-purpose tool — please read
the scope note in the [feature request template](.github/ISSUE_TEMPLATE/feature_request.yml)
before proposing new features.

## Setup

The toolchain is pinned with [mise](https://mise.jdx.dev/) (`.NET 10`):

```sh
mise install
```

Building the release bundle also needs **Visual Studio C++ build tools** (the
launcher is NativeAOT): `winget install Microsoft.VisualStudio.2022.BuildTools`,
then add "Desktop development with C++".

## Development loop

```sh
mise exec -- dotnet test TateYoko.slnx                 # Core unit + Pdf integration tests
mise exec -- dotnet run --project src/TateYoko.App     # run the app (unpackaged)
mise run publish                                       # assemble the distribution bundle
mise run icons                                         # regenerate icon assets from assets/AppIcon.png
```

## Architecture rules

TateYoko is a **hexagonal** design (see the README). The one rule that matters:

- **`TateYoko.Core` depends on neither PDFsharp nor WinUI.** The domain is pure;
  PDF work lives in `TateYoko.Pdf`, UI in `TateYoko.App`. Don't add a PDF or UI
  reference to `Core` — put the logic behind a port instead.

## Pull requests

- Use [Conventional Commits](https://www.conventionalcommits.org/) for the PR title
  (`feat:`, `fix:`, `perf:`, `docs:`, `chore:`, …). We squash-merge, so the title
  becomes the commit message and drives the automated version bump + CHANGELOG
  (release-please — see [docs/RELEASING.md](docs/RELEASING.md)).
- Make sure `dotnet test` passes and the Core boundary is intact.

## Code of Conduct

This project follows the [Contributor Covenant](.github/CODE_OF_CONDUCT.md).
