# Releasing

Releases are automated from [Conventional Commits](https://www.conventionalcommits.org/).
No one hand-picks a version number.

## How it works

1. **Commits land on `main`.** `feat:` / `fix:` / `perf:` commits are release-worthy;
   `docs:` / `chore:` / `refactor:` are not.
2. **release-please keeps a "Release PR" open** ([`.github/workflows/release-please.yml`](../.github/workflows/release-please.yml))
   that bumps the version in [`.release-please-manifest.json`](../.release-please-manifest.json)
   and updates [`CHANGELOG.md`](../CHANGELOG.md). Review it like any PR.
3. **Merging the Release PR** makes release-please create the GitHub Release as a
   **draft** and the `vX.Y.Z` tag, then dispatch the signed build.
4. **[`release.yml`](../.github/workflows/release.yml)** runs `build → sign → publish`:
   - `build` — `TateYoko.Pack bundle` assembles the self-contained bundle and a
     CycloneDX SBOM.
   - `sign` — Authenticode-signs our five first-party PEs with SSL.com eSigner
     (see [SIGNING.md](SIGNING.md)). Runs in the approval-gated `release` environment.
   - `publish` — `TateYoko.Pack package` zips + checksums the signed bundle, writes
     keyless build-provenance + SBOM attestations, attaches the assets to the draft,
     and publishes it (publishing creates the public release).

## One-time repository setup

- Create a **`release` environment** (Settings → Environments) and, optionally, add
  required reviewers so `sign`/`publish` pause for human approval.
- Add the SSL.com eSigner secrets to that environment (see [SIGNING.md](SIGNING.md)).
  Until they're set, releases still build and publish — **unsigned**, with a warning.

## Manual smoke test

You can dry-run the pipeline without cutting a release:

```sh
gh workflow run release.yml -f tag_name=main -f publish=false
```

This builds + signs + verifies but creates no Release or attestations.

## Verifying a downloaded release

```sh
gh attestation verify TateYoko-vX.Y.Z-win-x64.zip --repo P4suta/TateYoko
sha256sum -c SHA256SUMS.txt
```
