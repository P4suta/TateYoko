# Code signing

SSL.com eSigner Authenticode-signs the Windows MSIX bundle.

| File | Purpose |
| --- | --- |
| `TateYoko.msixbundle` | x64/ARM64 installation and AppInstaller updates |

The bundle signature applies recursively to its MSIX packages.
`TateYoko.appinstaller` is data, not executable code; SHA-256 and build provenance
protect it.

`tools/TateYoko.Pack` is the only definition of the signing set. `stage-signing`
copies only the bundle to a flat directory. After external signing,
`collect-signing` verifies:

- the default SignTool Authenticode chain;
- every signature;
- an RFC 3161 timestamp;
- signer subject `CN=Yasunobu Sakashita`;
- expected package identity, publisher, minimum OS, and capabilities;
- exactly one x64 and one ARM64 package; and
- no file association, protocol, execution alias, or other extension.

The publication job runs the same implementation again.

## Required secrets

Set all four in the GitHub `release` environment:

| Secret | Meaning |
| --- | --- |
| `ES_USERNAME` | SSL.com account username |
| `ES_PASSWORD` | SSL.com account password |
| `CREDENTIAL_ID` | eSigner code-signing certificate credential |
| `ES_TOTP_SECRET` | automated TOTP secret |

Missing or partial configuration fails the release. Require a human reviewer before
jobs may access these secrets.

Before rotating the certificate, update the manifest publisher and
`ExpectedPublisher` in `tools/TateYoko.Pack`, then pass a `publish=false` smoke test.

## Local verification

```powershell
Get-AuthenticodeSignature .\TateYoko.msixbundle | Format-List
```

The release gate also checks the timestamp and exact signer subject; `Valid` alone
is insufficient.
