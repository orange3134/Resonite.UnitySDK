# 別PCでの初回セットアップ

Windows 10/11の64bit環境で、このカスタムブランチをクローンしたフォルダーの **Start-OpenBrush-Importer.cmd** をダブルクリックします。PowerShellでコマンドを順番に入力する必要はありません。通常のユーザー権限で起動してください。必要になったインストーラーだけがWindowsの管理者確認を表示します。

セットアップの進み具合がコンソールに表示され、完了するとGUIが開きます。初回のUnityダウンロードは数GBあり、パッケージ取得・コンパイルにも時間がかかります。既存環境が使える場合はダウンロードせず再利用します。

## 自動で行うこと

| 項目 | 既存環境がある場合 | ない場合 |
| --- | --- | --- |
| Python | 64bit CPython 3.10〜3.14とTcl/Tkの動作を確認して再利用 | 公式のPython 3.13.17をユーザー用フォルダーへインストール |
| 変換用環境 | `.venv-openbrush`と必要なライブラリを確認 | 仮想環境を作り、requirementsのライブラリをインストール |
| Unity | プロジェクト指定の版とリビジョンを確認して再利用 | 公式Editorインストーラーで指定版をインストール。他の版は削除しない |
| Git | PATHと標準インストール先から検出 | WinGetの `Git.Git` をインストール |
| Unityプロジェクト | 前回の初期化結果・ソース・Libraryを確認 | パッケージ取得、C#コンパイル、ブラシ用シェーダーの存在確認 |
| ブラシ素材 | 保存済み設定、既存のBuilds配置、SteamのOpen Brushと追加ライブラリを探す | フォルダー選択を表示。後からGUIの設定でも指定可能 |
| 起動用exe | 生成済みのものを維持 | `OpenBrush Importer.exe`を生成。生成できない環境でもPythonからGUIを起動 |

PythonとUnityの公式ダウンロードは、実行前にWindowsの署名と発行元を確認します。Pythonの自動インストールでは、既存のPythonのPATH設定・関連付けを変更しません。Unityは `ProjectSettings/ProjectVersion.txt` の指定に従い、このブランチでは2022.3.62f3です。

## 本人操作が必要な場面

- **Unityのライセンスが未認証**：Unity Hubを開きます。未導入ならHubも準備します。自分でログイン・認証を済ませ、セットアップ画面に戻ってEnterを押してください。アカウント情報をスクリプトへ渡す必要はありません。
- **Windowsの管理者確認**：必要なインストーラーの確認に応答してください。拒否した場合はそこで中断します。
- **ブラシ素材が見つからない**：Open Brushの `Support/shaders/brushes` を指定してください。Open Brushのインストールフォルダー自体を選んでも、その下の素材フォルダーを検出します。素材を後で用意する場合はキャンセルできますが、外部画像参照のあるGLBを変換する前にGUIの設定で指定する必要があります。
- **GitがなくWinGetも使えない**：表示される案内に従ってGit for Windowsを用意し、同じ起動ファイルを再実行してください。

Unity Hubでのライセンスの選択・契約・アカウント操作は自動化しません。管理されたPCでは、会社・学校のインストール制限や実行ポリシーが優先されます。この起動ファイルは、そのPowerShellプロセスだけに実行ポリシーを指定し、PC全体の設定を変更しません。

## GUIが開いたら

1. 同じPCでResoniteを起動し、対象ワールドのResoniteLinkを有効にします。
2. GLBファイルと現在のポート番号を指定します。
3. 書き順アニメーションの有無、完成形のメッシュ結合を選びます。
4. **読み込み**を押します。

セットアップ自体はResoniteのワールドを変更しません。Resoniteのインストールやログインも自動では行いません。Open Brushは変換中に起動しておく必要がありません。

## 再実行と保存先

途中で失敗しても、同じ **Start-OpenBrush-Importer.cmd** を実行してください。完了済みのインストールや使える仮想環境は再利用します。セットアップに失敗した状態でGUIを成功扱いで起動することはありません。

- 実行ログ：SDK内 `.local/importer/setup-logs/<日時-ID>/`
- 初期化の確認記録：SDK内 `.local/importer/setup-ready.json`
- GUIの設定：SDK内 `.local/importer/settings.json`。ファイル選択・ポート・アニメーション・結合の設定を維持します。
- 自動インストール先：`%LOCALAPPDATA%\OpenBrushImporter\Python313` と `Unity\<バージョン>`
- 公式インストーラーのキャッシュ：`%LOCALAPPDATA%\OpenBrushImporter\Downloads`

別PCからコピーした仮想環境が壊れている場合は、`.venv-openbrush.broken-<ID>` に退避して作り直します。退避環境は自動削除しません。SDKをUnityで開いたままの場合は閉じてから再実行してください。

## 通常と異なる場所にインストールしている場合

Unity Hubの登録先・Windowsの登録情報も検索しますが、見つからない場合は一度だけフルパスを渡せます。SDKフォルダーで次を実行します。

```powershell
.\Start-OpenBrush-Importer.cmd -UnityPath 'E:\Unity\2022.3.62f3\Editor\Unity.exe' -PythonPath 'E:\Python313\python.exe' -BrushAssets 'E:\Open Brush\Support\shaders\brushes'
```

各引数は必要なものだけ指定します。Unityとブラシ素材の場所はGUIの設定へ保存します。指定されたUnityの版が違う場合は、勝手に別の版へ切り替えずにエラーを表示します。

診断用に `-NoInstall`（不足があれば停止）、`-NoLaunch`（GUIを起動しない）、`-NonInteractive`（本人操作が必要なら停止または未設定のまま進む）、`-ForceInitialize`（Unity初期化を再確認）も使えます。

## 検証と実装上の範囲

既存環境の再利用・GUI起動・キャッシュ済みの再起動を実環境で検証済みです。Library・仮想環境・exeを除いたクローン相当のコピーでも、NumPy導入・Unity初期化・exe生成まで確認しました。日本語・空白・`&`を含むフォルダー名でも実行しています。テストでは未導入・インストーラー失敗・未署名ファイル・壊れた仮想環境・ライセンス不足・設定保持を検証します。全ソフト未導入の別PCでのインストール完了は、この作業PC上のテストだけでは保証できません。

実装は[Python公式Windows配布](https://www.python.org/downloads/windows/)と[Unity公式コマンドラインインストール仕様](https://docs.unity3d.com/2022.3/Documentation/Manual/InstallingUnity.html)を使用します。Unity Hubのログイン・ライセンス管理は[Unityの案内](https://docs.unity.com/en-us/hub/manage-license)に従ってください。
