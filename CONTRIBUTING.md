# Contributing to TateYoko

TateYokoは「1つの縦書きPDFを、迷わず右綴じ見開きへ変換する」ことに集中した
Windows 11アプリです。機能を増やす前に、既存操作を短く、安全に、予測可能にできないかを
検討してください。

## Setup

Windows 11で次を実行します。

```powershell
mise install
mise exec -- just restore
mise exec -- lefthook install
mise exec -- just ci
```

`mise.toml`は.NET SDK、just、Git hookツールを固定し、`.config/dotnet-tools.json`は
CycloneDXとStrykerを固定します。`just restore`はNuGet通信を行い、各プロジェクトの
`packages.lock.json`を更新します。Appは通常ビルド、ReadyToRun portable、ReadyToRun
MSIXで依存グラフが異なるため、後者2つを`packages.portable.lock.json`と
`packages.msix.lock.json`へ分離しています。通常の検証とCIは3グラフすべてに
`just restore-locked`を使い、未レビューの依存解決を許しません。

Rendered golden testは`just test`がPoppler 26.02.0を`build/tools/`へ取得し、
release assetと`pdftoppm.exe`の両SHA-256を照合してから使用します。システムへの
インストールや暗黙のrunner依存はありません。

パッケージ版をローカル起動するにはWindows Developer Modeが必要です。`just run`は
プロジェクトで固定した`Microsoft.Windows.SDK.BuildTools.WinApp`からCLIを解決するため、
グローバルツールの追加インストールは不要です。

## Design rules

- `TateYoko.Engine`だけが再利用可能な公開APIです。公開面の変更にはXML documentation、
  引数・例外・キャンセル・並行実行の契約、PublicApiAnalyzer snapshot、契約テストが必要です。
- PDFsharpの型を公開面へ漏らさず、PDF I/Oと原子的な出力確定はEngine内部へ閉じます。
- `TateYoko.App`は単一画面の状態機械です。Service Locator、汎用DIコンテナー、不要な
  navigation abstractionは追加しません。
- UI文字列をコードへ直書きせず、英語・日本語・簡体字リソースを同時に更新します。
- 新しいOS capability、file association、protocol、AppExecutionAlias、ネットワーク通信は
  セキュリティ境界の変更です。必要性・代替案・テストをPRで明示してください。
- 「制約をREADMEへ書く」だけでは完了ではありません。検証できる制約は型、実装、テスト、
  CI gateのいずれかで強制します。

## Development loop

```powershell
mise exec -- just fmt
mise exec -- just build
mise exec -- just test
mise exec -- just coverage
mise exec -- just mutation
mise exec -- just run
```

`just ci`がmerge gateの全体です。PDFの見た目を変える場合はrendered golden testを、
状態遷移を変える場合はViewModel testを、XAMLを変える場合はUI Automation testと
Light/Dark/High Contrastのスクリーンショットを更新してください。

## Pull requests

- PRタイトルはConventional Commits形式にします。squash merge後のタイトルがCHANGELOGと
  version bumpの入力です。
- Linear issueをリンクし、ユーザーに見える結果と検証証跡を記載します。
- `packages.lock.json`、公開API snapshot、SBOM/notice生成結果の変化を意図的にレビューします。
- 配布経路を変えた場合はx64/ARM64 portable、MSIX bundle、署名、AppInstallerの全経路を
  release smoke testで確認します。

## Releases

公開物は署名済みでなければ作れません。ローカルで未署名の配布入力を作るには
`just dist-build`を使えますが、`just package`は正しい署名、タイムスタンプ、SBOM、
第三者noticeが揃うまで失敗します。詳細は[docs/RELEASING.md](docs/RELEASING.md)と
[docs/SIGNING.md](docs/SIGNING.md)を参照してください。

## Code of Conduct

[Contributor Covenant](.github/CODE_OF_CONDUCT.md)に従ってください。
