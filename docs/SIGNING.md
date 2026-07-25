# Code signing

Windowsに配布する3ファイルをSSL.com eSignerでAuthenticode署名します。

| File | Purpose |
| --- | --- |
| `TateYoko-win-x64.exe` | x64 portable ZIPに格納する実行ファイル |
| `TateYoko-win-arm64.exe` | ARM64 portable ZIPに格納する実行ファイル |
| `TateYoko.msixbundle` | x64/ARM64 MSIX installation and AppInstaller updates |

MSIX bundleの署名は含まれるMSIXにも再帰適用されます。`TateYoko.appinstaller`は実行可能
コードではないためAuthenticode対象ではなく、SHA-256とbuild provenanceで完全性を担保します。

署名対象の唯一の定義は`tools/TateYoko.Pack`です。`stage-signing`は上の3ファイルだけを
flat directoryへコピーし、外部署名後の`collect-signing`は直ちに次を検証します。
公開時は各署名済みexeを必須の`TateYoko.pri`と組にしたCPU別ZIPへ格納します。

- SignToolのdefault Authenticode policyでchainが有効
- 全署名が有効
- RFC 3161 timestampが存在
- signer subjectが`CN=Yasunobu Sakashita`と完全一致
- MSIX identity/publisher/minimum OS/capabilityが期待値どおり
- bundleにx64とARM64が1つずつ存在
- file association、protocol、execution aliasなどのextensionがない

`publish` jobでも同じ検証を再実行します。検証ロジックをworkflow内へ複製しません。

## Required secrets

GitHubの`release` environmentへ次を設定します。

| Secret | Meaning |
| --- | --- |
| `ES_USERNAME` | SSL.com account username |
| `ES_PASSWORD` | SSL.com account password |
| `CREDENTIAL_ID` | eSigner code-signing certificate credential |
| `ES_TOTP_SECRET` | automated TOTP secret |

4つすべてが必要です。部分設定や未設定はrelease失敗です。workflowは署名なしで公開を続けません。
`release` environmentにはrequired reviewersを設定し、secretにアクセスできるjobを人の承認後に
開始してください。

証明書を変更する場合は、先にmanifest publisherと`tools/TateYoko.Pack`の
`ExpectedPublisher`を同じidentityへ更新し、`publish=false` smoke testを通します。

## Local verification

```powershell
Get-AuthenticodeSignature .\TateYoko-win-x64.exe | Format-List
Get-AuthenticodeSignature .\TateYoko-win-arm64.exe | Format-List
Get-AuthenticodeSignature .\TateYoko.msixbundle | Format-List
```

表示が`Valid`でも、release gateはtimestampと期待したsigner subjectまで確認します。
