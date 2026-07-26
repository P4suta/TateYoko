# Releasing

公開リリースは`release-please`と`.github/workflows/release.yml`が行います。手元でversionを
決めてファイルをアップロードする経路はありません。

## Release contract

リリースが成立するには、同じcommitから次の全条件を満たす必要があります。

1. lock fileどおりに依存関係を復元できる
2. format、strict build、全テスト、coverage、dependency audit、mutation testが通る
3. .NETとWindows App SDKを内包したx64/ARM64の単一EXEとMSIX bundleを構築し、
   外部framework dependencyがないこと、architecture、内容、サイズ予算を再検証できる
4. CycloneDX SBOMと正確な第三者noticeを生成できる
5. portable EXE 2本とMSIX bundleを承認済み証明書で署名・タイムスタンプできる
6. 署名者、chain、timestamp、bundle identity、architectureを別工程で再検証できる
7. SHA-256一覧とGitHub build provenance/SBOM attestationを作成できる
8. release対象commitをWindows 11実機でUI Automation一括試験し、全状態のaccessibility監査と
   スクリーンショット目視確認を完了している

1つでも欠ければdraftは公開されません。未署名で継続するfallbackはありません。

## Automated flow

1. Conventional Commitsが`main`へ入ると、release-pleaseがRelease PRを保守します。
2. Release PRをmergeすると`vX.Y.Z`のdraft releaseを作り、`release.yml`をdispatchします。
3. `build` jobがmerge gateを再実行し、未署名の署名入力と法務・SBOM証跡だけを短期artifactへ
   渡します。
4. approval付き`release` environmentで`sign` jobがeSignerを実行します。4つのsecretが
   1つでもなければ即失敗します。
5. `publish` jobが署名を再検証し、成果物・チェックサム・ライセンスを組み立て、
   attestationを作ってからdraftを公開します。

公開されるファイルは次のとおりです。

```text
TateYoko.appinstaller
TateYoko.msixbundle
TateYoko-win-x64.zip
TateYoko-win-arm64.zip
SHA256SUMS.txt
tateyoko.cdx.json
THIRD-PARTY-NOTICES.txt
LICENSE.txt
```

AppInstallerは`releases/latest/download/TateYoko.msixbundle`を参照します。公開後の
latest releaseだけが更新対象になるため、draftや失敗したbuildは配信されません。

## One-time repository configuration

### release-please environment

`RELEASE_PLEASE_CLIENT_ID`と`RELEASE_PLEASE_PRIVATE_KEY`を設定します。GitHub Appには
ContentsとPull requestsのread/write、および対象repositoryへのinstallationが必要です。
App tokenで作ったPRだけが通常のCIを起動できます。

`v*` tag rulesetは削除とnon-fast-forward更新だけを禁止します。新規tag作成は許可されるため、
GitHub App、人間、通常の`GITHUB_TOKEN`のいずれにもbypassを与えません。

### release environment

[SIGNING.md](SIGNING.md)の4 secretを設定し、required reviewersと`main`限定deployment branch
policyを有効にします。build jobにsecretは渡らず、sign/publishだけがenvironmentを通ります。

## Smoke test

まずrelease対象commitをWindows 11の対話セッションで起動し、UI release candidateを検証します。

```powershell
mise exec -- just run
mise exec -- just ui-test <TateYokoのPID>
```

`build/ui-tests/<timestamp>/`の実行ログと全スクリーンショットを確認し、結果をRelease PRへ
添付してください。スクリプト成功だけではなく、切れ・重なり・不自然な余白・誤った強調・
読みづらい状態がないことを人間が確認するまで承認しません。

その後、既存のrelease tagを対象に、公開せず署名までを実行できます。`tag_name`と`sha`は
同じcommitへ解決され、そのcommitが`main`上にある必要があります。

```powershell
gh workflow run release.yml `
  -f tag_name=v0.0.1 `
  -f sha=v0.0.1 `
  -f publish=false
```

このテストも実証明書とenvironment approvalを必要とします。

## Verify a download

```powershell
gh attestation verify TateYoko.msixbundle --repo P4suta/TateYoko
Get-Content SHA256SUMS.txt
Get-FileHash TateYoko.msixbundle -Algorithm SHA256
Get-AuthenticodeSignature TateYoko.msixbundle | Format-List
```

MSIX bundleの署名は内部MSIXにも再帰適用されます。個別MSIXを別に署名・公開しません。
