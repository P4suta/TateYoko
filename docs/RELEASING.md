# Releasing

`release-please` and `.github/workflows/release.yml` own versioning and publication.
There is no supported path for choosing a local version and uploading files by hand.

## Release contract

One commit must satisfy every condition:

1. restore exactly the committed dependency graphs;
2. pass formatting, spelling, strict build, tests, coverage, audit, and mutation gates;
3. build and inspect an x64/ARM64 MSIX bundle within its size budgets;
4. generate a CycloneDX runtime SBOM, REUSE source SPDX SBOM, and complete notices;
5. sign and RFC 3161 timestamp the bundle with the approved certificate;
6. independently recheck signer, chain, timestamp, identity, and architectures;
7. generate SHA-256 checksums and GitHub provenance/SBOM attestations; and
8. pass UI Automation, accessibility, and visual review on Windows 11.

The release remains a draft if any condition fails. There is no unsigned fallback.

## Automated flow

1. Conventional Commits on `main` update the release-please pull request.
2. Merging that pull request creates a draft release and dispatches `release.yml`.
3. `build` reruns the merge gate and uploads only signing inputs and evidence.
4. `sign` runs eSigner behind approval of the `release` environment.
5. `package` uses a clean runner to reverify signatures and assemble the candidate.
   It always runs, including dry runs.
6. `publish` attests and publishes that exact candidate only when `publish=true`.

Published files:

```text
TateYoko.appinstaller
TateYoko.msixbundle
SHA256SUMS.txt
tateyoko.cdx.json
tateyoko-sources.spdx
THIRD-PARTY-NOTICES.txt
LICENSE.txt
```

The AppInstaller points to `releases/latest/download/TateYoko.msixbundle`; drafts
and failed runs cannot become update sources.

## Repository configuration

The `release-please` environment needs `RELEASE_PLEASE_CLIENT_ID` and
`RELEASE_PLEASE_PRIVATE_KEY`. Its GitHub App needs read/write access to Contents and
Pull requests and an installation on this repository. App-authored pull requests
trigger the required CI workflow.

The `release` environment needs the four secrets in [SIGNING.md](SIGNING.md),
required reviewers, and a `main`-only deployment policy. Disable self-review and
administrator bypass. The default-branch ruleset also requires one independent
approval after the latest push and resolution of every review thread.

The repository currently has one collaborator. Merges and releases intentionally
remain blocked until a trusted independent reviewer is added. Build and package jobs
receive no release secrets.

## Smoke test

Test the release commit in an interactive Windows 11 session:

```powershell
mise exec -- just run
mise exec -- just ui-test <PID>
```

Review every screenshot and the log under `build/ui-tests/<timestamp>/`. Do not
approve clipped text, overlap, misleading emphasis, poor spacing, or unreadable
states merely because automation passed.

An existing release tag can exercise signing without publication:

```powershell
gh workflow run release.yml `
  -f tag_name=v0.0.1 `
  -f sha=v0.0.1 `
  -f publish=false
```

The tag and SHA must resolve to the same commit on `main`. This test requires the
real certificate and environment approval.

## Verify a download

```powershell
gh attestation verify TateYoko.msixbundle --repo P4suta/TateYoko
Get-Content SHA256SUMS.txt
Get-FileHash TateYoko.msixbundle -Algorithm SHA256
Get-AuthenticodeSignature TateYoko.msixbundle | Format-List
```

The bundle signature applies recursively to its MSIX packages. Individual MSIX
files are neither signed nor published separately.
