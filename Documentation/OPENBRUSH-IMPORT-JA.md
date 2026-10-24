# Open Brush のスケッチを Resonite に取り込む手順

GUIから一括で読み込む場合は [アプリの手順](OPENBRUSH-APP-JA.md) を参照してください。

更新日 2026年10月10日。この手順は `resonite-unity-sdk-openbrush` に追加したカスタム変換機能用です。ブラシの色と見た目を変換し、筆先を追うように線が伸びる書き順アニメーションと再生ボタンをResoniteに送信します。`--completed` を付けると、書き順再生なしで完成形だけを読み込めます。

作業は **Open BrushでGLBを書き出す → Pythonで変換用フォルダーを作る → Unity SDKからResoniteへ送る** の3段階です。`.tilt` をSDKへ直接渡す機能や専用GUIはまだありません。任意のスケッチを選べますが、[ブラシ対応表](OPENBRUSH-BRUSH-SUPPORT-JA.md)に記載した入力条件と制約があります。

## 事前に用意するもの

| 必要なもの | この環境での場所と条件 |
| --- | --- |
| カスタムSDK | `D:\Document\VerseTV\resonite-unity-sdk-openbrush`。通常のUnitySDKだけではこの変換機能は使えない。 |
| Unity Editor | SDKの `ProjectSettings/ProjectVersion.txt` に一致する **2022.3.62f3**。ライセンス認証と初回パッケージ読み込みを済ませる。 |
| Python | 3.10以降を推奨。以下は `py -3` が使えるWindows環境の例。使えなければPython実行ファイルのパスを指定する。 |
| Open Brush | 時刻・ブラシGUID・生の頂点チャンネルを含む旧GLB2を書き出せる版。この作業で修正したソースは `D:\Document\VerseTV\open-brush-versetv`。 |
| Resonite | 取り込み先ワールドを開き、ResoniteLinkを有効にする。 |

ソース側のOpen BrushプロジェクトはUnity **6000.6.0f1**、SDK側は **2022.3.62f3** です。プロジェクトを取り違えないでください。Sliceはソース修正後のEditor再生または再ビルドしたアプリが必要で、古い実行ファイルのままでは書き出せません。

以降のPowerShellはSDKフォルダーで実行します。

```powershell
$sdk = 'D:\Document\VerseTV\resonite-unity-sdk-openbrush'
Set-Location -LiteralPath $sdk
py -3 -m venv .venv-openbrush
$python = Join-Path $sdk '.venv-openbrush\Scripts\python.exe'
& $python -m pip install -r Support/Python/requirements-brush-animation.txt
```

環境作成とインストールは初回だけです。2回目以降も `$sdk` と `$python` は設定してください。

## 1 Open Brushから元のGLBを書き出す

1. Open Brushで取り込みたいスケッチを開き、保存します。元の `.tilt` は残します。
2. 通常のExport操作で現在のスケッチを書き出します。
3. ユーザーフォルダーの `Documents\Open Brush\Exports` を開きます。保存先を変更している場合は、そのExportsフォルダーを使います。
4. 今回作成されたフォルダーの **`glb\スケッチ名.glb`** を選びます。同名の再出力には ` 0`、` 1` などの番号が付くため、更新日時も確認します。

この経路が必要とするのは、**旧エクスポーターが出力したglTF 2.0のGLB**です。`newglb` 内のGLBは一部の形状がすでに変形済みで、生の半径やUV等も不足するため使用しません。`.glb1`、FBX、OBJ、Blender等を通したGLB、以前生成した `drawing.glb` も入力にしません。

書き出し設定を明示する場合は、Open Brushを終了して `Documents\Open Brush\Open Brush.cfg` をバックアップし、既存JSONの `Export` 設定に次を反映します。ファイル全体をこの短い例で置き換えないでください。

```json
{
  "Export": {
    "ExportStrokeTimestamp": true,
    "Formats": {
      "glb": true,
      "newglb": false
    }
  }
}
```

既存の `Formats` の他形式を使うなら、その項目は残します。設定変更後はOpen Brushを起動し直して再出力します。`ExportStrokeMetadata` や `KeepStrokes` は新エクスポーター側の設定で、この経路の代わりにはなりません。

HTTP APIを使っている場合は、**アプリ実行中またはEditorがPlay中**であることを確認してから、現在のスケッチを次で書き出すこともできます。40074はOpen Brush側のAPIポートであり、ResoniteLinkのポートとは別です。

```powershell
Invoke-WebRequest 'http://localhost:40074/api/v1?export.current' -UseBasicParsing
```

APIの応答だけで書き出し完了とは判定せず、処理が終わり、GLBが作成されてから次へ進みます。0バイトのファイル、出力が失敗したフォルダー、参照テクスチャの欠けたファイルは使わないでください。

## 2 変換用フォルダーを作る

GLBのパスと、まだ存在しない出力先を指定します。最初は短いスケッチで試すと結果を確認しやすくなります。

```powershell
$sourceGlb = 'C:\Users\star_\Documents\Open Brush\Exports\MySketch\glb\MySketch.glb'
$bundleDir = 'D:\Document\VerseTV\OpenBrushImports\MySketch-01'

& $python Support/Python/prepare-openbrush-bundle.py $sourceGlb $bundleDir
if ($LASTEXITCODE -ne 0) { throw '変換に失敗しました。表示されたエラーを確認してください。' }
```

`MySketch` とユーザー名は実際のものへ変更します。この段階はオフラインで実行でき、Open BrushやResoniteを起動しておく必要はありません。GLBが外部テクスチャを参照している場合、そのファイルを元の位置に置いておきます。

成功すると画面にブラシ名、再生時間、分割数、`bundle.json` のパスが表示されます。フォルダーには `drawing.glb`、`materials.json`、`timeline.json`、必要なブラシ専用データ、`conversion-report.json`、`bundle.json` が作られます。特殊ブラシ用の補助ファイルは自動選択されます。

完成したフォルダーは丸ごと移動できます。送信時には元のGLBや参照テクスチャは不要です。ただし、内部ファイルを個別に編集するとハッシュ検査で拒否されます。設定を変えたい場合は、元のGLBから別名の新しいフォルダーへ作り直してください。

### Remote textures と表示された場合

旧GLBはテクスチャを `https://www.tiltbrush.com/shaders/brushes/...` のURLで参照する場合があります。`Preparation failed` は変換失敗です。先頭に形状検査のJSONが出ても、bundle完成を意味しません。

2026年10月10日の修正で、`--brush-assets` にOpen Brushの `Support/shaders/brushes` を指定すると、同じフォルダー構造・ファイル名のローカル画像を取り込めるようになりました。この環境では次を使えます。

```powershell
$brushAssets = 'D:\Document\VerseTV\Builds\OpenXR_Release_OpenBrush\Support\shaders\brushes'
& $python Support/Python/prepare-openbrush-bundle.py $sourceGlb $bundleDir --brush-assets $brushAssets
if ($LASTEXITCODE -ne 0) { throw '変換に失敗しました。' }
```

別のPCでは実際のOpen Brushインストール先を指定してください。ソースリポジトリの同名フォルダーに画像が揃っているとは限りません。ファイルが足りない場合は不足パスを表示して停止します。単に同じブラシ名のFBX用画像へ置き換えず、参照URLと対応する版の資産を用意してください。

インターネットへの自動アクセスは行いません。元のGLBは変更せず、完成bundleの `drawing.glb` 内へ画像を取り込みます。取り込んだ枚数は `conversion-report.json` の `localizedTextureCount` で確認できます。失敗した準備処理は出力先を完成扱いで残さないため、出力先が存在しなければ同じ `$bundleDir` で再実行できます。

### 完成形だけを読み込む（書き順アニメーションなし）

準備コマンドに **`--completed`** を追加します。既存のフォルダーへ上書きせず、別名の出力先を指定してください。

```powershell
$bundleDir = 'D:\Document\VerseTV\OpenBrushImports\MySketch-completed-01'
& $python Support/Python/prepare-openbrush-bundle.py $sourceGlb $bundleDir --completed --brush-assets $brushAssets
if ($LASTEXITCODE -ne 0) { throw '変換に失敗しました。' }
```

`$brushAssets` は上の「Remote textures」の例で設定します。外部URLの画像を参照していないGLBなら `--brush-assets $brushAssets` は省略できます。

その後は手順4と同じ送信コマンドを使い、モデル名を `-Name 'MySketch completed'` にすると区別しやすくなります。モードはbundleに保存されるため、Unity側の追加引数は不要です。

- 取り込み直後から線全体を表示します。Restart / Play / Pause / Clearボタンは作りません。
- Fire、Disco、粒子など、対応済みのブラシ固有の動きは残します。完全な静止画へ固定するオプションではありません。
- 書き順用の細分化を省き、原則1ストローク1メッシュにします。`--speed`、`--lead-in`、`--hold`、`--max-gap`、分割密度は完成形の表示や動きの速さに影響しません。
- Flow系、Waveform、Electricityなど有限時間の画像・メッシュ列を使う表現は、既定で **8秒の繰り返し** にします。非周期的な模様にはループ境界で飛びが見える場合があります。`--effect-loop-seconds 12` のように0.1〜60秒で変更できます。長くするとデータ量と準備・送信時間が増えます。FireやDiscoなど既存の固有ループの周期は変更しません。
- 入力条件は従来と同じです。完成形でもブラシ識別に使う時刻や専用チャンネルを含む旧GLB2が必要です。

送信レポートでは `mode: completed`、`growthPartCount: 0`、`duration: 0` になります。`effectLoopSeconds` はブラシ模様用の内部タイマーです。`playbackId` があっても書き順再生用ではありません。

### 書き順の速さと待ち時間

| オプション | 既定値 | 意味 |
| --- | --- | --- |
| `--speed` | `1` | 描画の速度。`2`なら描画とストローク間の時間を約半分にする。装飾シェーダー全体の速度変更ではない。 |
| `--lead-in` | `1` | 最初の描画までの空白秒数。`0`も指定可。 |
| `--hold` | `3` | 描き終わった状態を見せる秒数。正の値が必要。 |
| `--max-gap` | 未指定 | 長いストローク間の休止を圧縮する上限秒数。未指定なら休止を保持する。速度適用前の時間に対する上限。 |
| `--slices-per-second` | `30` | 一般ブラシの描画分割密度。増やすほど細かく、オブジェクト数が増える。 |
| `--max-slices` | `96` | 1ストロークの最大分割数。長い線で上限に達すると分割間隔が粗くなる。 |

最初の記録時刻を基準に並べ直し、指定した `lead-in` を加えます。筆先の全履歴を厳密に復元する方式ではなく、GLBの補間済み頂点時刻に基づく表面の描画です。

例えば、長い休止を0.5秒まで圧縮し、分割密度を少し下げる場合は次のようにします。

```powershell
$bundleDir = 'D:\Document\VerseTV\OpenBrushImports\MySketch-02'
& $python Support/Python/prepare-openbrush-bundle.py $sourceGlb $bundleDir --max-gap 0.5 --slices-per-second 16
if ($LASTEXITCODE -ne 0) { throw '変換に失敗しました。' }
```

分割密度を下げても、特殊ブラシの30/60fpsメッシュ・テクスチャ列は別の設定です。特殊表現で容量上限に達した場合は、スケッチを分ける、休止を縮める、`--speed` を上げる、`--hold` を短くする方法も検討してください。

### 長いスケッチの模様の解像度

Flow系（ChromaticWave、Rainbow等）とWaveformは、全再生時間の模様を画像列として作ります。2026年10月10日以降は128Mフレーム画素の上限を超える場合、ストロークごとの模様の解像度を半分ずつ下げます。書き順、再生時間、模様のフレームレートは保持します。短いスケッチで上限に収まる場合は解像度を変えません。

変更内容は送信レポートの `bakeAdjustments` とUnityログに表示されます。微細な模様がぼやける・欠ける場合があるため、完成形を確認してください。画質を優先するなら、`--max-gap` で休止を圧縮したり、スケッチを分けたりしてから再生成します。最短辺16ピクセルでも収まらない場合は停止します。他のブラシの個別上限や、スケッチ全体のメモリ制約は引き続きあります。

Highlighterも大きなスケッチでは、16Mピクセル上限に収まるよう三角形ごとのマスク画像の解像度を下げます。UVの範囲が大きい三角形も、最大512サンプル/辺から同じ調整を行います。繰り返し模様が細かい場合はディテールが変わることがあります。最小16サンプル/辺でも収まらない場合は停止します。形状や色を省略する処理ではありませんが、細い輪郭は変わる場合があります。変更は `bakeAdjustments` に記録されます。

## 3 Resoniteの取り込み先を準備する

1. Resoniteで対象ワールドを開きます。
2. ダッシュのSessionで **Enable Resonite Link** を有効にします。
3. 表示されたポート番号を控えます。再起動後は変わることがあります。
4. 送信する位置を決めます。次の例の `0,0,2` はワールド座標で、ユーザーの目の前へ自動配置する指定ではありません。

この送信はスケッチ用の新しいルートを作成します。既存モデルを自動更新・置換するものではありません。再送すれば別のモデルが増えます。

## 4 Unity SDKから送信する

**このSDKプロジェクトを開いているUnity Editorを閉じてから**実行します。別プロジェクトのUnityまで閉じる必要はありません。未保存の作業は先に保存します。

SDKフォルダーで、準備したbundleのフォルダー、現在のLinkポート、モデル名を指定します。

```powershell
$bundleDir = 'D:\Document\VerseTV\OpenBrushImports\MySketch-completed-01'
& .\Support\send-openbrush-bundle.ps1 -BundleDirectory $bundleDir -Port 50334 -Name 'MySketch completed'
```

フォルダーとポートは実際の値に置き換えてください。書き順付きbundleでも同じコマンドで送信でき、モードはbundleから判定します。位置は既定でワールド座標 `0,0,2` です。変える場合は `-Position '2,0,2'` を追加します。Unityの場所が違う場合は `-Unity 'C:\実際の場所\Unity.exe'` を追加します。

このスクリプトは毎回送信引数を作り直し、別名のログ・レポートを作ります。Unity終了まで待ち、終了コードとレポートを確認して、失敗なら原因を表示します。**`completed : True` と「送信完了」が完了の目印**です。画面に表示したBundleのパスが今回のものか確認してください。初回インポートや大きなスケッチは時間がかかります。進捗を見る場合は、表示されたLogパスを別のPowerShellで開きます。

```powershell
Get-Content -LiteralPath '画面に表示されたログのフルパス' -Tail 40
```

### 以前の `$unityArgs` を使っていた場合

`$bundleDir` を変更しても、すでに作成した `$bundleFile` や `$unityArgs` は自動更新されません。`Start-Process` の行だけを再実行すると、前のスケッチと前のレポート名が使われます。`Inspect and archive the previous send report before retrying` は、そのレポートが既存だったため送信前に停止したことを示します。古いレポートを削除して済ませず、上の送信用スクリプトで新しいbundleを指定してください。

手動でUnityの引数を組む場合は、`$bundleFile`・`$runId`・`$report`・`$log`・`$unityArgs` を毎回作り直し、終了後のエラーチェックまで実行する必要があります。`-nographics` は付けません。模様のベイクにGPUを使います。`-openbrushBundle` と個別ファイル引数は混在させないでください。通常の「Send Current Scene」だけでは、この専用準備処理を実行できません。

## 5 ワールド内で再生を確認する

送信した名前のモデルを探します。書き順付きの場合はその下に再生パネルがあり、完成形モードにはパネルはありません。スケッチ本来の座標を保持するため、ルート位置と見た目の中心が一致しない場合があります。見つからないときはレポートの `boundsCenter` を確認します。向きはインポーターがY軸180度の補正を自動適用します。

| ボタン | 動作 |
| --- | --- |
| Restart | 最初から描き直す。 |
| Play / Pause | 書き順の再開と一時停止。 |
| Clear | 再生を停止して描画を先頭へ戻す。 |

最後まで描くと `hold` 秒間表示してループします。裏から見てボタン文字が見えない場合は、反対側へ回ってください。Fire・LightWire・WigglyGraphite等の独立した模様は、描画を一時停止しても動き続けます。

完成形の色・ブラシ数・位置が元と合うこと、途中で未来のストロークが出ないこと、再開とループができることを確認します。取得したローカルアセットを含むアイテムの保存・アップロードは送信とは別です。長期保存したい場合は、Resonite側で保存し、別の機会に読み戻せることも確認してください。

## 見た目と環境の合わせ方

SDK送信はワールド全体の空・ライトを自動で置き換えません。光が強すぎる、色が暗い、反射が違う場合は、ブラシに加えてワールド側の照明・露出・Bloomの影響も確認します。

現在の23944試験環境には、比較用の主光源と補助光源、暗い空、環境光を復元してあります。この状態は他ワールドや再起動後へ自動継承されません。検証用の `restore-parity-environment.ps1` は特定のLight/Skybox構成を想定した開発用で、任意ワールドへの汎用インポート手順には含めません。

3D Printing Brushは元の環境の一部を色へ焼き込みますが、全ブラシが元の照明を内蔵するわけではありません。カスタムスカイボックスの球面調和環境光や完全な影の一致も未対応です。

## 必要な場合だけ使う追加オプション

次のフラグは `$unityArgs` に追加してから起動します。標準経路より常に正確という意味ではありません。

| 引数 | 用途と制約 |
| --- | --- |
| `-openbrushCombineMeshes` | 完成形モード専用。同じマテリアルの静的な不透明メッシュを近傍ごとに結合。透明・動的・特殊な視線依存表現は除外。元のストローク単位の編集はできなくなる。書き順ありではエラー。 |
| `-openbrushDiamondFilm` | DiamondHull/Membraneの薄膜色変化。標準の簡易透明材質から変更。斜め・裏側の見え方、反射、色勾配に制約。 |
| `-openbrushExperimentalNeonMatcap` | NeonPulseの角度依存表現の試験版。標準Fresnelと異なる近似で、光の芯を改善。視線と反射の差が残る。 |
| `-openbrushPrintingShadows` | 3D Printing Brush同士の影の試験ベイク。境界に筋が出ることがあり、他ブラシからの遮蔽は含まない。 |

完成形の結合は、`--completed` で準備したbundleに対し、送信時に指定します。準備コマンドには結合フラグを付けません。

```powershell
& ./Support/send-openbrush-bundle.ps1 -BundleDirectory $bundleDir -Port 12575 -Name 'MySketch completed' -CombineMeshes
```

ポートは対象ワールドの番号に置き換えてください。Unityを直接起動する場合は `$unityArgs += '-openbrushCombineMeshes'` を起動前に追加します。省略時は従来の分割状態を維持します。結合の範囲と制約は[アプリ手順書](OPENBRUSH-APP-JA.md#メッシュを結合)を参照してください。

例えばDiamondの試験版は、`Start-Process` の前に `$unityArgs += '-openbrushDiamondFilm'` を挿入します。比較用にモデル名を変えて送ると結果を見比べられます。

## うまくいかないとき

| 症状またはエラー | 確認と対処 |
| --- | --- |
| `Unsupported or missing original brush GUID` | `newglb`や一般GLBを選んでいないか確認。正しい入力でも未対応ブラシなら対応表を参照し、元スケッチの複製から除くか置き換える。 |
| 時刻、半径、UV、生チャンネルがない／変形済みと判定される | 元の旧GLB2から再出力する。中間GLBを再変換しない。必要チャンネルを削除する最適化ツールを通さない。 |
| 出力フォルダーがすでに存在する | `MySketch-02`等の新しい出力先を指定。中の一部だけを書き換えて再利用しない。 |
| ハッシュ不一致 | bundleフォルダーを全体コピーし直すか再生成。生成物の手動修正は戻す。 |
| `Remote textures`／`Remote texture references found` | 上記の `--brush-assets` を追加して、ローカルのOpen Brushブラシ資産を指定する。Failedのまま送信へ進まない。 |
| 接続できない | 対象ワールドのResoniteLink有効状態と最新のポートを確認。API40074と混同しない。 |
| Unityが起動しない／プロジェクト使用中 | Unityの版・ライセンス・SDKの二重起動を確認。ログを読む。 |
| レポートが既存と表示される | 前回の完了状態とワールドのモデルを確認後、新しいレポート名で実行。再送は新規モデルを作る。 |
| `completed:false`／途中で切断 | 部分的なモデルが残ることがある。レポートの `rootId` と対象モデルを確認してから再試行。既存の別モデルは消さない。 |
| メモリやフレーム画素数の上限（自動調整後も停止する場合） | スケッチ分割、休止圧縮、描画高速化、終了後表示時間の短縮。全体メモリ監視は未実装で、大規模スケッチの安定動作は未保証。 |
| 一部のブラシが出力にない／GLBが0バイト | Open Brushのエクスポートログとmanifestを確認。エクスポーターで省略されたブラシはSDK側だけでは復元できない。 |
| テクスチャファイルがない | 元GLBの参照先およびOpen Brushの `Support/shaders/brushes` を確認。同じ版の正しい資産を復元して再出力。空画像で代用しない。 |

### 実験ブラシの書き出し登録

Dark、Slice、MatteMembrane、ShinyMembrane、DiamondMembraneは、Open Brushの版によって `Support/exportManifest.json` への追加登録が必要です。この作業環境では追加済みですが、他のインストールへは自動導入されません。

SDKの以下の補助スクリプトは「元manifest、新しい出力ファイル」の順に指定し、既存ファイルを直接上書きしません。Open Brushを終了してバックアップを取り、対象の版と差分を確認してから、生成結果を実際に起動するOpen BrushのSupportフォルダーへ配置します。

```powershell
# 例: 自分のOpen Brushインストール先へ変更する
$manifest = 'D:\YourOpenBrush\Support\exportManifest.json'
& $python Support/Python/extend-openbrush-export-manifest.py $manifest 'D:\Work\exportManifest-membranes.json'
# Darkだけ追加する場合
& $python Support/Python/register-dark-export.py $manifest 'D:\Work\exportManifest-dark.json'
# Sliceだけ追加する場合
& $python Support/Python/register-slice-export.py $manifest 'D:\Work\exportManifest-slice.json'
```

各例は独立です。複数を追加するなら、前段で作ったファイルを次段の入力に渡してください。DarkとSliceは登録済みならエラーになります。生成だけではインストール先に反映されません。Sliceはmanifestに加え、`SliceBrush` と `GeometryPool` のUV保持修正が入ったOpen Brushの実行環境も必要です。Dark/Sliceのmanifest内の代替材質はデータ運搬用で、一般GLBビューア上の見た目の保証ではありません。

## 問題を報告するときに残すもの

元の `.tilt`、変換前GLBと参照テクスチャ、bundleフォルダー、送信レポートJSON、Unityログを保管してください。見た目の問題には、元のOpen BrushとResoniteの同じ方向・近い再生時刻の画像が役立ちます。公開する場合はスケッチとテクスチャに共有してよい内容だけが含まれるか確認します。

この手順のCLI引数は [prepare-openbrush-bundle.py](../Support/Python/prepare-openbrush-bundle.py) と [OpenBrushSendModel.cs](../Assets/OpenBrushResonite/Editor/OpenBrushSendModel.cs)、入力検証は [OpenBrushConversionBundle.cs](../Assets/OpenBrushResonite/Editor/OpenBrushConversionBundle.cs) に対応しています。

### NumPyのメモリ確保エラーが出る場合

`_ArrayMemoryError: Unable to allocate ...` は、準備処理がメモリを確保できなかった状態です。表示される8MiB等は最後に失敗した追加確保量であり、それまでの総使用量ではありません。

2026年10月10日の修正では、同じメッシュにまとめられた複数ストロークを処理する際、メッシュ全体の頂点をストロークごとに複製していた問題を解消しました。書き順あり・完成形の両方で、各ストロークに必要な頂点だけを保持し、GLB保存時の全体コピーも減らしています。該当エラーが出た旧版では、更新後に準備コマンドを再実行してください。失敗時の出力先は完成フォルダーとして残さない仕組みです。

大きなスケッチでは修正後も相応のメモリを使います。不足する場合は64bit Pythonを使い、メモリを多く使うアプリを閉じるか、スケッチを分けてください。準備の成功と、Unity側のブラシ別ベイク上限・Resoniteへの送信成功は別の確認です。
