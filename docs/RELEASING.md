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

Two environments (already created) hold the secrets, scoped to `main` only:

**`release-please` — the version bot's GitHub App.** release-please runs as a
GitHub App (not the default `GITHUB_TOKEN`) so its Release PR triggers CI: the
branch ruleset requires the `test` check to pass before merging, and a
`GITHUB_TOKEN`-opened PR does not run CI. Until the App is configured, the job
no-ops with a notice.

1. Create a **GitHub App** (Settings → Developer settings → GitHub Apps → New).
   Permissions: **Contents: Read & write**, **Pull requests: Read & write**.
   No webhook needed. Note its **Client ID** and generate a **private key**.
2. **Install** the App on the `P4suta/TateYoko` repository.
3. Add its credentials to the **`release-please`** environment (Settings →
   Environments → `release-please` → Secrets):
   - `RELEASE_PLEASE_CLIENT_ID` — the App's Client ID
   - `RELEASE_PLEASE_PRIVATE_KEY` — the generated `.pem`, pasted whole
4. (Optional) In each branch ruleset, add the App as a **bypass actor** if you
   later give release-please tasks that push to `main` directly. The default
   PR-based flow needs no bypass.

**`release` — code signing.** Add the SSL.com eSigner secrets to this environment
(see [SIGNING.md](SIGNING.md)); optionally add required reviewers so `sign`/`publish`
pause for approval. Until the secrets are set, releases still build and publish —
**unsigned**, with a warning.

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
