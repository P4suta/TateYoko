# Code signing

Release binaries are Authenticode-signed so Windows SmartScreen doesn't warn end
users. Signing uses **SSL.com eSigner** (a cloud HSM) via the official
[`SSLcom/esigner-codesign`](https://github.com/SSLcom/esigner-codesign) Action.

## What gets signed

Only our own five PE files — the bundled .NET / Windows App SDK runtime DLLs are
already Microsoft-signed. The authoritative list lives in
[`tools/TateYoko.Pack`](../tools/TateYoko.Pack/Program.cs) (`FirstPartyPes`):

| File | Role |
| ---- | ---- |
| `TateYoko.exe` | root launcher (NativeAOT) |
| `app/TateYoko.App.exe` | apphost |
| `app/TateYoko.App.dll` | managed entry assembly |
| `app/TateYoko.Core.dll` | first-party domain library |
| `app/TateYoko.Pdf.dll` | first-party PDF library |

`TateYoko.Pack sign-stage` copies these into a flat `publish/sign-stage/` dir for
`batch_sign`; `sign-collect` copies the signed files back. After collection the
[`verify-signatures`](../.github/actions/verify-signatures/action.yml) action asserts
each PE has a valid chain, an RFC 3161 timestamp, and the expected signer subject.

## Dormant until configured

The signing steps are gated on the presence of the eSigner secrets. **With no
secrets set, a release still builds and publishes — unsigned, with a `::warning::`.**
Add the secrets to light signing up; no workflow change is needed.

## Enabling signing

Add these secrets to the **`release`** environment (Settings → Environments →
`release` → Secrets):

| Secret | Meaning |
| ------ | ------- |
| `ES_USERNAME` | SSL.com account username |
| `ES_PASSWORD` | SSL.com account password |
| `CREDENTIAL_ID` | eSigner credential ID for the code-signing cert |
| `ES_TOTP_SECRET` | eSigner TOTP secret (for automated 2FA) |

The expected signer subject is asserted in [`release.yml`](../.github/workflows/release.yml)
via `SIGNER_SUBJECT_CONTAINS` — update it if you sign with a different certificate.

Verify a release download:

```sh
gh attestation verify TateYoko-vX.Y.Z-win-x64.zip --repo P4suta/TateYoko
```
