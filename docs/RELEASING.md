# Releasing

The release proposal and [release workflow](../.github/workflows/release.yml) own versioning and publication.
Use the automated flow for release files.
Signing, release approval, and publication require an explicit owner instruction naming the version.

## Release contract

The selected commit must pass locked restoration, formatting, spelling, strict compilation, tests, coverage, dependency audit, and release-tool contracts.
The [verification decision](adr/pull-request-verification.md) separates pull-request verification from distribution builds.
Distribution requires an MSIX bundle for x64 and ARM64 within its size budgets, dependency inventories, and complete third-party notices.
The bundle must carry the approved Authenticode signature and RFC 3161 timestamp.
Independent verification checks the signer, certificate chain, timestamp, package identity, and architectures.
The candidate includes checksums and GitHub provenance and dependency attestations.
Windows UI Automation, accessibility, and visual review remain release requirements.
A failed condition leaves the release as a draft.
Publication requires valid signatures.

## Automated flow

Conventional Commits on main update the release proposal.
Merging the proposal creates a draft release and dispatches the release workflow.
The build job repeats the merge gate and uploads signing inputs and build results.
The sign job uses eSigner behind approval in the release environment.
The package job uses a clean runner to verify signatures and assemble the candidate.
Publication runs only when publish=true and attests the candidate.
A dry run still signs and verifies packages but skips publication.

The published file set contains the following files.

```text
TateYoko.appinstaller
TateYoko.msixbundle
SHA256SUMS.txt
tateyoko.cdx.json
tateyoko-sources.spdx
THIRD-PARTY-NOTICES.txt
LICENSE.txt
```

The AppInstaller uses the `releases/latest/download/TateYoko.msixbundle` URL.
Drafts and failed runs can't become update sources.
The bundle signature covers its embedded MSIX packages.
Individual MSIX files remain inside the bundle.

## Repository configuration

The main-only release-please environment prevents administrator bypass.
The [credential guide](credential-delivery.md) describes native synchronization of RELEASE_PLEASE_CLIENT_ID and RELEASE_PLEASE_PRIVATE_KEY.
Install the App on TateYoko with contents and pull request write permissions.
App-authored proposals trigger required CI.

Signing keeps Doppler OIDC through DOPPLER_SIGN_WINDOWS_IDENTITY_ID.
The release environment admits main, requires the owner's approval, and prevents administrator bypass.
See the [signing guide](SIGNING.md) for certificate requirements.
The default branch requires successful checks, signed linear history, and resolution of review threads.
Third-party approval isn't required for ordinary changes.
Build and package jobs receive no signing credentials.

## Interactive review

Use an interactive Windows 11 session for the selected release commit.
Run the following commands and review the screenshots and log beneath the UI test output directory.

```powershell
mise exec -- just run
mise exec -- just ui-test <PID>
```

Reject clipped text, overlap, misleading emphasis, poor spacing, and unreadable states even when automation passes.
An existing release tag can exercise signing without publication through a manual dispatch with publish=false.
The selected tag and SHA must resolve to the same commit on main.
The check uses the real certificate and needs environment approval.

## Download verification

Run the following commands to verify the downloaded distribution.

```powershell
gh attestation verify TateYoko.msixbundle --repo P4suta/TateYoko
Get-Content SHA256SUMS.txt
Get-FileHash TateYoko.msixbundle -Algorithm SHA256
Get-AuthenticodeSignature TateYoko.msixbundle | Format-List
```
