# Open Brush ブラシ対応表

対応一覧の基準日 2026年10月5日。容量制約の補足更新 2026年10月10日。BubbleWand追加・大規模Disco修正 2026年10月24日。対象はこのカスタムSDKの現在の実装です。Open Brushの通常の `m_Material` を基準に調整しており、Testing Materialは対象にしていません。

2026年10月24日の再監査では、ブラシ定義134 GUIDと、書き出しManifestだけにある4 GUIDの合計138 GUIDを照合しました。再監査時は105対応・33未対応でした。その後19 GUIDを追加し、現在は変換プロファイル124、未対応14です。Manifest登録分だけでは116 GUID中107にプロファイルがあります。通常Manifestの66項目中58項目という内訳は変わりません（残り8項目はBlocks・環境・モデル用）。名称が同じでもGUIDが違えば別項目として扱います。数は見た目の一致率や利用頻度を表しません。

[インポート手順書](OPENBRUSH-IMPORT-JA.md)と併せて参照してください。[CSV版](OPENBRUSH-BRUSH-SUPPORT.csv)には同名ブラシを区別するGUIDとSDK内部名も収録しています。

## 状態の読み方

| 状態 | 意味 |
| --- | --- |
| 対応あり | 基本の色・形状・書き順の変換経路あり。完全一致の保証ではない。 |
| 制約あり | 変換経路あり。ただし個別の近似・未再現・追加条件がある。 |
| 未対応 | プロファイルなし。その部分を警告付きで省略し、対応部分の変換を続ける。全件未対応なら送信しない。 |

「確認根拠」は実装または試験記録です。複数色・描画再生・写真比較を実施したものでも、任意のスケッチ・全方向・全環境での一致を保証しません。記録名が同じ接頭辞のものは、Open Brush側の `Support/Exports/parity/progress-*.txt` を参照できます。各行の機能は原則として標準のbundle変換経路です。追加オプションは明記しています。

## 全ブラシに共通する制約

| 項目 | 現状 |
| --- | --- |
| 書き順 | 書き出された時刻に沿って線が伸びる。頂点の時刻は制御点からの補間で、筆先の完全な記録再生ではない。Hullなどは生成シミュレーションの再実行ではなく完成面の段階表示。 |
| 材質 | Unityの独自シェーダーを直接転送せず、Resonite材質と画像・メッシュのベイクで近似する。 |
| 色と照明 | Gamma/Linear、Bloom、露出、反射、透過順序に差がある。試験ワールドの照明補正は他の環境での一致を保証しない。 |
| 動き | 有限の30/60fps等のサンプリング。長時間・多色・多ストロークでは容量が増える。Flow系とWaveformは上限超過時に模様の解像度を段階的に下げ、時間とfpsを保持する。短辺16pxでも不足すれば停止。個別上限はあるが全体メモリ上限は未実装。 |
| 一時停止 | 書き順は停止する。Fire、LightWire、WigglyGraphite等の独立した装飾時計は動き続ける。全ブラシの模様を一括停止する機能ではない。 |
| 音声と追加パラメーター | 音声反応、任意のマテリアル変更、スクリプトによるclip/dissolve等は全面対応していない。通常材質・既定値が中心。 |
| 対象データ | ブラシGUID・時刻・必要な生チャンネルを含む旧エクスポーターのGLB2が必要。任意のglTFやモデル・画像を含む全シーンの移植は対象外。 |
| 汎用性 | 複数色・複数ブラシ混在を検証済み。ただし任意のレイヤー変換、三角形内色勾配、極端な筆圧・サイズ・長さは未検証または個別に拒否する。 |

## 対応ありと制約あり

| ブラシ | 状態 | 対応内容 | 制約とメモ | 確認根拠 |
| --- | --- | --- | --- | --- |
| 3D Printing Brush | 制約あり | 面の色と法線・元の照明の焼き込み・書き順 | Flat環境の主光源・環境光が前提。カスタム天空のSH未対応。影は標準では再現せず、追加の影ベイクは境界アーティファクトあり。 | progress-flat-lighting／printing-normals／printing-shadow |
| Bubbles | 制約あり | 泡の移動・白い反射・書き順 | 色と白い反射を分離。有限ベイクの継ぎ目、フィルタ差。全方向の完全一致は未確認。 | progress-bubbles |
| BubbleWand | 制約あり | 膨らみ・揺れ・角度による虹色・書き順 | 通常材質の膨らみとcurl揺れを30fpsでベイク。虹色は20秒周期のMatcap近似で、視線・照明による差が残る。有限形状ループには継ぎ目があり得る。元の半径チャンネルが必須。三角形内色勾配は未対応、薄膜は最大16材質色、形状は20Mフレーム頂点まで。 | OpenBrushBubbleWandChecks（元HLSLとのGPU比較・描画境界）／AYAKASHI完成形・結合オン実送信（2026-10-24） |
| CelVinyl | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| Charcoal | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| ChromaticWave | 制約あり | 動く模様・色・書き順 | 描画時計に同期した有限テクスチャ列。HDR抑制・細部の解像度制約。音声反応未対応。SparksとRainは形状補正あり。 | progress-flow-hdr／chromatic／fire-sparks／weather |
| CoarseBristles | 制約あり | 色・テクスチャ・書き順 | 照明差あり。CoarseBristlesの細い筆毛内部は色測定の十分な対象になっていない。 | プロファイル／塗料ブラシ比較 |
| CoarseBristlesSingleSided | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| Comet | 制約あり | 彗星状の模様・書き順 | 元の時間変化を近似。加算・発光・サンプリングの差が残る。 | progress-comet |
| ConcaveHull | 制約あり | 形状・色・材質・書き順 | 照明を補正した近似。Membraneは書き出しmanifestへの追加登録が必要な版あり。Hullの描画は完成面の表示近似。 | progress-solid-extra |
| DanceFloor | 制約あり | 形状・模様・位相・書き順 | 元の位相を保持。メッシュと模様のベイクが大容量になりやすい。 | progress-dancefloor |
| Dark | 制約あり | 背景を暗くする乗算表現・書き順 | Gammaの背景依存乗算を一定の補正で近似。任意の背景で完全一致しない。追加の書き出し登録が必要な版あり。 | progress-dark |
| DiamondHull | 制約あり | 透明材質・書き順／追加オプションで薄膜色変化 | 標準は簡易透明材質。薄膜は -openbrushDiamondFilm が必要。Matcapの角度・模様固定方向・光沢に差、三角形内色勾配は未対応。 | progress-diamond |
| DiamondMembrane | 制約あり | 透明材質・書き順／追加オプションで薄膜色変化 | 標準は簡易透明材質。薄膜は -openbrushDiamondFilm が必要。Matcapの角度・模様固定方向・光沢に差、三角形内色勾配は未対応。 | progress-diamond |
| Digital | 制約あり | 格子模様・色・書き順 | 長いUVに対応した静的アトラス。遠距離の細線や画面微分AAは異なる。 | progress-digital-race／digital-length |
| Disco | 制約あり | 変形・きらめき・複数ストロークの書き順 | 描画と変形を同期。大きな面数は複数行のアトラスに配置する。光沢とGamma照明差が残る。三角形内の色勾配は未対応。 | progress-disco-multi／disco-smoothness |
| DotMarker | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| Dots | 制約あり | 粒子の形状・色・出現・書き順 | 生データから有限メッシュ列を作成。粒子数×時間で容量増。 | progress-dots |
| DoubleFlat | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| DoubleTaperedFlat | 制約あり | 両端の先細り形状・色・書き順 | 元シェーダーの形状補正を焼き込む。Flat側の陰影は近似。 | progress-experimental-paint |
| DoubleTaperedMarker | 制約あり | 両端の先細り形状・色・書き順 | 元シェーダーの形状補正を焼き込む。Flat側の陰影は近似。 | progress-experimental-paint |
| Drafting | 制約あり | 表示状態の模様・色・書き順 | DraftingVisibility=1の状態を再現。任意の可視性操作・音声反応は未対応。 | progress-additive-extra |
| DryBrush | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| DuctTape | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| DuctTapeGeometry | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| DuctTapeSingleSided | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| Electricity | 制約あり | 電気の変形・色・書き順 | 元の生チャンネルが必須。有限メッシュ列、HDRとBloomは近似。 | progress-electricity／mixed-bundle |
| Embers | 制約あり | 粒の動き・描画時刻に応じた出現 | ネイティブ表現による近似。元のシミュレーションの完全再現ではない。 | BrushTest描画・粒子検証 |
| FacetedTube | 制約あり | 面ごとの色・書き順 | 元の面表現を変換。レンダラーと環境差に共通制約。 | progress-solid-extra |
| Fairy | 制約あり | 星雲等の動く模様・5色・書き順 | 元HDRが強すぎるため出力を抑制。Spaceの色・粒状感、Fairyの微細模様に差。音声反応なし。 | progress-fairy-space |
| Feather | 制約あり | 柔らかいテクスチャ・色・書き順 | アトラス端・ミップを修正。Featherに薄い三角形境界が残る。暗色と背景依存の加算差あり。 | progress-soft-mip-isolation |
| Fire | 制約あり | 炎模様の揺らぎ・色・書き順 | 模様の時計は描画と独立。停止中も揺らぐ。HDRを抑制。三角形内の色勾配は未対応でエラー。 | progress-fire-color／sampling |
| Fire2 | 制約あり | 動く模様・色・書き順 | 描画時計に同期した有限テクスチャ列。HDR抑制・細部の解像度制約。音声反応未対応。SparksとRainは形状補正あり。 | progress-flow-hdr／chromatic／fire-sparks／weather |
| Flat | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| FlatDeprecated | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| FlatSingleSided | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| Gouache | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| GouacheSingleSided | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| Guts | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Highlighter | 制約あり | 色・マスク・書き順 | 元のフラグメント表現を焼き込む。UVの範囲が大きい場合は最大512サンプル/辺に制限し、16Mピクセルを超える場合はさらに輪郭用画像の解像度を下げる（最小16サンプル/辺）。細い輪郭や繰り返し模様は差が出る場合がある。重なりと背景依存の合成差が残る。 | progress-highlighter／sampling |
| Hypercolor | 制約あり | 色変化する模様・書き順 | 有限ベイクとネイティブ材質。光沢・環境・色勾配の全条件での一致は未保証。 | progress-hypercolor |
| HypercolorSingleSided | 制約あり | Hypercolor系の追加GUIDの変換 | 名前に反して元descriptorは裏面も生成する。Manifest未登録の別GUID。今回の全定義監査には含む。独立した全方向の実写検証は未確認。 | プロファイル／progress-hypercolor |
| HyperGrid | 制約あり | グリッドの形状・出現・色・書き順 | パディングとミップを修正済み。明るさ・鋭さが異なる。静止専用経路は未整備。 | progress-hypergrid-sampling |
| Icing | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Ink | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| InkGeometry | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| InkSingleSided | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| KeijiroTube | 制約あり | 進行する収縮・5色・書き順 | 描画と変形を同期。60fps有限ベイク。暗色・陰影差、ループ時の位相リセットあり。生の半径・接線が必要。 | progress-keijiro-23944 |
| Lacewing | 制約あり | 色・高いアルファしきい値・法線・書き順 | 通常材質のSpecularColorは黒で、元の動的反射項も寄与ゼロ。任意の材質変更は未対応。 | progress-lacewing |
| LeakyPen | 制約あり | 色・カットアウト・法線・書き順 | 5色と両面で調整。Gamma照明と暗色に近似が残る。 | progress-leakypen |
| Leaves | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Leaves2 | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Light | 制約あり | 発光・色・書き順 | Bloomと露出による発光の広がりは近似。 | プロファイル／BrushTest |
| LightWire | 制約あり | 膨らみ・走る光・色・書き順 | 光の時計は描画と独立。光沢・Bloom差、極端に長いUVの細部制約。 | progress-lightwire-fullhdr |
| Lofted | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 UV重複・異色のストロークは面ごと64×64画素でベイク。 | プロファイル／BrushTest・塗料ブラシ比較 |
| Lofted (Hue Shift) | 制約あり | 元の色相変換・書き順 | 元の頂点色を基に色変換。Lofted側のGamma照明を経験的補正。全照明一致ではない。 | progress-hueshift-brushtest |
| Marbled Rainbow | 制約あり | マーブル色変換・法線・書き順 | 元の色計算を保持。照明・光沢はネイティブPBS近似。 | progress-marbled |
| Marker | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| MarkerGeometry | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Manifest未登録。材質・変換経路を確認したが、この旧Geometry版の実写比較は未実施。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| MatteHull | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| MatteMembrane | 制約あり | 形状・色・材質・書き順 | 照明を補正した近似。Membraneは書き出しmanifestへの追加登録が必要な版あり。Hullの描画は完成面の表示近似。 | progress-solid-extra |
| Muscle | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| MylarTube | 制約あり | くびれ・角度依存の光・時間変化・書き順 | 標準経路に実装済み。Matcapは真の視線方向を近似し、白・紫の明るさや帯の色が異なる。生半径が必須。三角形内の色勾配は未対応。 | progress-mylar |
| NeonPulse | 制約あり | 走る光・書き順 | 標準Fresnelの角度応答に差。追加オプションのMatcapで改善するが視線・反射は近似。 | progress-neon／neon-matcap |
| OilPaint | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| OilPaintGeometry | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Manifest未登録。材質・変換経路を確認したが、この旧Geometry版の実写比較は未実施。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| OilPaintSingleSided | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| Paper | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| PaperGeometry | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Manifest未登録。材質・変換経路を確認したが、この旧Geometry版の実写比較は未実施。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| PaperSingleSided | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| Petal | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| Plasma | 制約あり | 動く模様・色・書き順 | 描画時計に同期した有限テクスチャ列。HDR抑制・細部の解像度制約。音声反応未対応。SparksとRainは形状補正あり。 | progress-flow-hdr／chromatic／fire-sparks／weather |
| QuillCube | 制約あり | 頂点色・アルファ・形状・書き順 | 元のMSAA alpha-to-coverageをアルファ合成で近似。縁や重なりが異なる。 | プロファイル／progress-experimental-paint |
| QuillCylinder | 制約あり | 頂点色・アルファ・形状・書き順 | 元のMSAA alpha-to-coverageをアルファ合成で近似。縁や重なりが異なる。 | プロファイル／progress-experimental-paint |
| QuillEllipse | 制約あり | 頂点色・アルファ・形状・書き順 | 元のMSAA alpha-to-coverageをアルファ合成で近似。縁や重なりが異なる。 | プロファイル／progress-experimental-paint |
| QuillRibbon | 制約あり | 頂点色・アルファ・形状・書き順 | 元のMSAA alpha-to-coverageをアルファ合成で近似。縁や重なりが異なる。 | プロファイル／progress-experimental-paint |
| Race | 制約あり | 格子模様・色・書き順 | 長いUVに対応した静的アトラス。遠距離の細線や画面微分AAは異なる。 | progress-digital-race／digital-length |
| Rain | 制約あり | 動く模様・色・書き順 | 描画時計に同期した有限テクスチャ列。HDR抑制・細部の解像度制約。音声反応未対応。SparksとRainは形状補正あり。 | progress-flow-hdr／chromatic／fire-sparks／weather |
| Rainbow | 制約あり | 虹模様・書き順 | 元の色変換を近似。加算重なり・Bloom・細部に差。 | progress-rainbow |
| Rising Bubbles | 制約あり | 上昇・泡模様・色・書き順 | 位相と模様をサンプリング。細い縁と複雑なレイヤー変換の一般性に課題。 | progress-rising |
| ShinyHull | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| ShinyMembrane | 制約あり | 形状・色・材質・書き順 | 照明を補正した近似。Membraneは書き出しmanifestへの追加登録が必要な版あり。Hullの描画は完成面の表示近似。 | progress-solid-extra |
| SingleSided | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Slice | 制約あり | 断面の虹・彩度明度模様・書き順 | 選択した筆色を使わないのは元の仕様。透明面の順序差あり。修正済みソースと書き出し登録が必要。 | progress-slice-23944 |
| Smoke | 制約あり | 煙テクスチャ・加算合成・書き順 | 表示を復元。元の煙の変形や見え方の完全再現は未完了。 | BrushTestでの表示確認／初期変換実装 |
| SmoothHull | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Snow | 制約あり | 粒子の動き・出現・書き順 | 有限フレーム列で近似。フィルタ・見え方と長時間再生に共通制約。 | progress-snow／particles |
| SoftHighlighter | 制約あり | 柔らかいテクスチャ・色・書き順 | アトラス端・ミップを修正。Featherに薄い三角形境界が残る。暗色と背景依存の加算差あり。 | progress-soft-mip-isolation |
| Space | 制約あり | 星雲等の動く模様・5色・書き順 | 元HDRが強すぎるため出力を抑制。Spaceの色・粒状感、Fairyの微細模様に差。音声反応なし。 | progress-fairy-space |
| Sparks | 制約あり | 動く模様・色・書き順 | 描画時計に同期した有限テクスチャ列。HDR抑制・細部の解像度制約。音声反応未対応。SparksとRainは形状補正あり。 | progress-flow-hdr／chromatic／fire-sparks／weather |
| Spikes | 制約あり | 形状・色・書き順 | 照明下の明るさに目立つ差が残る。 | プロファイル／塗料ブラシ比較 |
| Splatter | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| SplatterSingleSided | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| SquarePaper | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Stars | 制約あり | 粒の点滅・HDR色・書き順 | HDR頂点色を保持。元のBloom・色変換・フィルタ差は残る。音声反応未対応。 | progress-stars-hdr／particles |
| Streamers | 制約あり | 動く模様・色・書き順 | 描画時計に同期した有限テクスチャ列。HDR抑制・細部の解像度制約。音声反応未対応。SparksとRainは形状補正あり。 | progress-flow-hdr／chromatic／fire-sparks／weather |
| Taffy | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| TaperedFlat | 制約あり | 色・テクスチャ・書き順 | 照明差あり。CoarseBristlesの細い筆毛内部は色測定の十分な対象になっていない。 | プロファイル／塗料ブラシ比較 |
| TaperedFlatSingleSided | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| TaperedHueShift | 制約あり | 元の色相変換・書き順 | 元の頂点色を基に色変換。Lofted側のGamma照明を経験的補正。全照明一致ではない。 UV重複・異色のストロークは面ごと64×64画素でベイク。 | progress-hueshift-brushtest |
| TaperedMarker | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| TaperedMarker_Flat | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| TaperedMarkerGeometry | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Manifest未登録。材質・変換経路を確認したが、この旧Geometry版の実写比較は未実施。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| TaperedWire | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| ThickGeometry | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| ThickPaint | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| ThickPaintSingleSided | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| Toon | 制約あり | 塗りと輪郭・筆圧・書き順 | 元のworld Yによる明るさを焼き込む。輪郭幅は基準距離での近似。移動・回転・距離変更で差が出る。 | progress-toon |
| TubeAdditive | 制約あり | 加算合成・色・書き順 | 元より暗い箇所と、前後の面・背景との重なり方の差が残る。 | progress-additive-extra／soft-mip-isolation |
| TubeToonInverted | 制約あり | 黒い本体と色付き外殻・書き順 | 元の変形を焼き込む。任意の姿勢・拡縮や全視点での一致は未保証。 | progress-inverted-toon |
| UnlitHull | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| VelvetInk | 制約あり | 柔らかいテクスチャ・色・書き順 | アトラス端・ミップを修正。Featherに薄い三角形境界が残る。暗色と背景依存の加算差あり。 | progress-soft-mip-isolation |
| Waveform | 制約あり | 音声なしの波形模様・色・書き順 | 60fps有限ベイク、発光上限を調整。音声入力・FFT連動は再現しない。 | progress-waveform |
| WaveformFFT | 制約あり | 音声なしのFFT状波形模様・色・書き順 | 通常シェーダーの非AUDIO_REACTIVE式を60fpsでベイク。音声入力や実際のFFT値は渡さない。HDRを抑制し、有限時間の反復境界・解像度・Bloomに差が残る。 | Waveform GPU/scalar誤差0.00218／coverage-variants2実機 |
| WaveformParticles | 制約あり | 元の三角形粒子・curl運動・粒の出現 | 生オフセット必須。ビルボード化せず30fpsのメッシュ列で再現。完成形は粒の経過時間10秒から開始、書き順では粒の出現から経過時間を再開。元アプリの絶対時刻・音声・位相は引き継がない。10Mフレーム頂点上限と反復境界あり。 | 生データの三角形検証／GPU粒サイズ・単位検証／coverage-audio実機 |
| WaveformTube | 制約あり | 音声入力なしの帯模様・UV変形・色・書き順 | 累積ビート0・初期波形テクスチャ状態を再現。無音状態では模様は静止し、重複画像を2端点に削減。音声連動・録音済み波形の再生は未対応。HDRとBloomに差が残る。 | 通常シェーダー照合／OpenBrushCoverageChecks／coverage-audio実機 |
| WetPaint | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| WetPaintSingleSided | 制約あり | 通常マテリアルの色・模様・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| WigglyGraphite | 制約あり | 揺らぐ模様・色・書き順 | 模様の時計は描画と独立。色を補正したが明るさに差が残る。 | progress-sampling／BrushTest |
| WigglyGraphiteSingleSided | 制約あり | 揺らぐ鉛筆模様・色・書き順 | 通常m_Materialが既存対応版と同一であることをGUIDごとに確認。元のGLB形状を保持。Gamma/Linear照明・光沢の近似は通常版と共通。現行Open Brushはロード時に現行GUIDへ置換するため、比較写真は同材質の現行形状。旧版固有形状の完全一致を検証したものではない。6段階・12fpsの模様アニメーションを共用。 | 通常材質のGUID照合／coverage-variants／OpenBrushCoverageChecks |
| Wind | 制約あり | 動く模様・色・書き順 | 描画時計に同期した有限テクスチャ列。HDR抑制・細部の解像度制約。音声反応未対応。SparksとRainは形状補正あり。 | progress-flow-hdr／chromatic／fire-sparks／weather |
| Wire | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| Wire (Lit) | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Wireframe | 制約あり | 輪郭模様・色・書き順 | 基準背景に対する加算近似。任意背景・三角形内色勾配・音声・スクリプトのclip/dissolveは未対応または未検証。 | progress-wireframe |

## 未対応

未対応項目を含むGLBは準備段階でエラーになります。部分を黙って落として成功扱いにはしません。Open Brush自体が出力を省略した箇所はSDKでは検出できないため、元スケッチとの比較も必要です。

| ブラシ | 状態 | 対応内容 | 制約とメモ | 確認根拠 |
| --- | --- | --- | --- | --- |
| BlocksBasic | 未対応 | 変換対象外 | モデル／環境インポート用のFakeBrush定義で、描画ストロークを生成する通常ブラシではない。モデルごとの材質・テクスチャ・LightMap・透明度と、時刻なしメッシュを保持する別の変換経路が必要。現行のスケッチ変換経路では未対応。 | 2026-10-24：通常材質とFakeBrush/BlocksBrushScript等を確認 |
| BlocksGem | 未対応 | 変換対象外 | モデル／環境インポート用のFakeBrush定義で、描画ストロークを生成する通常ブラシではない。モデルごとの材質・テクスチャ・LightMap・透明度と、時刻なしメッシュを保持する別の変換経路が必要。現行のスケッチ変換経路では未対応。 | 2026-10-24：通常材質とFakeBrush/BlocksBrushScript等を確認 |
| BlocksGlass | 未対応 | 変換対象外 | モデル／環境インポート用のFakeBrush定義で、描画ストロークを生成する通常ブラシではない。モデルごとの材質・テクスチャ・LightMap・透明度と、時刻なしメッシュを保持する別の変換経路が必要。現行のスケッチ変換経路では未対応。 | 2026-10-24：通常材質とFakeBrush/BlocksBrushScript等を確認 |
| Braid3 | 未対応 | 変換対象外 | 旧exportManifestにGUIDが残るが、現行のBrushDescriptor・通常マテリアル・該当シェーダーは見つからない。旧版の実アセットまたは元の見た目を確認できるGLBが必要。名前だけで別ブラシへ置換しない。 | GUID再監査（2026-10-24）／Support/exportManifest.json・実行ビルド版 |
| CandyCane | 未対応 | 変換対象外 | 旧exportManifestにGUIDが残るが、現行のBrushDescriptor・通常マテリアル・該当シェーダーは見つからない。旧版の実アセットまたは元の見た目を確認できるGLBが必要。名前だけで別ブラシへ置換しない。 | GUID再監査（2026-10-24）／Support/exportManifest.json・実行ビルド版 |
| EnvironmentDiffuse | 未対応 | 変換対象外 | モデル／環境インポート用のFakeBrush定義で、描画ストロークを生成する通常ブラシではない。モデルごとの材質・テクスチャ・LightMap・透明度と、時刻なしメッシュを保持する別の変換経路が必要。現行のスケッチ変換経路では未対応。 | 2026-10-24：通常材質とFakeBrush/BlocksBrushScript等を確認 |
| EnvironmentDiffuseLightMap | 未対応 | 変換対象外 | モデル／環境インポート用のFakeBrush定義で、描画ストロークを生成する通常ブラシではない。モデルごとの材質・テクスチャ・LightMap・透明度と、時刻なしメッシュを保持する別の変換経路が必要。現行のスケッチ変換経路では未対応。 | 2026-10-24：通常材質とFakeBrush/BlocksBrushScript等を確認 |
| HolidayTree | 未対応 | 変換対象外 | 旧exportManifestにGUIDが残るが、現行のBrushDescriptor・通常マテリアル・該当シェーダーは見つからない。旧版の実アセットまたは元の見た目を確認できるGLBが必要。名前だけで別ブラシへ置換しない。 | GUID再監査（2026-10-24）／Support/exportManifest.json・実行ビルド版 |
| PassthroughHull | 未対応 | 変換対象外 | 通常シェーダーはRGBとカメラ出力alphaをクリアするMRパススルーの穴。普通の透明マテリアルでは代用できず、Resonite側のMR/compositor連携が別途必要。 | カタログとプロファイル照合 |
| PbrTemplate | 未対応 | 変換対象外 | モデル／環境インポート用のFakeBrush定義で、描画ストロークを生成する通常ブラシではない。モデルごとの材質・テクスチャ・LightMap・透明度と、時刻なしメッシュを保持する別の変換経路が必要。現行のスケッチ変換経路では未対応。 | 2026-10-24：通常材質とFakeBrush/BlocksBrushScript等を確認 |
| PbrTransparentTemplate | 未対応 | 変換対象外 | モデル／環境インポート用のFakeBrush定義で、描画ストロークを生成する通常ブラシではない。モデルごとの材質・テクスチャ・LightMap・透明度と、時刻なしメッシュを保持する別の変換経路が必要。現行のスケッチ変換経路では未対応。 | 2026-10-24：通常材質とFakeBrush/BlocksBrushScript等を確認 |
| Snowflake | 未対応 | 変換対象外 | 旧exportManifestにGUIDが残るが、現行のBrushDescriptor・通常マテリアル・該当シェーダーは見つからない。旧版の実アセットまたは元の見た目を確認できるGLBが必要。名前だけで別ブラシへ置換しない。 | GUID再監査（2026-10-24）／Support/exportManifest.json・実行ビルド版 |
| SvgTemplate | 未対応 | 変換対象外 | モデル／環境インポート用のFakeBrush定義で、描画ストロークを生成する通常ブラシではない。モデルごとの材質・テクスチャ・LightMap・透明度と、時刻なしメッシュを保持する別の変換経路が必要。現行のスケッチ変換経路では未対応。 | 2026-10-24：通常材質とFakeBrush/BlocksBrushScript等を確認 |
| TubeUnlit | 未対応 | 変換対象外 | テスト用定義。Manifest・exportManifest未登録。DurableNameが空で、通常m_Materialも未設定。変換対象として未実装。 | GUID再監査（2026-10-24）／Assets/Resources/X/Brushes/Test/TubeUnlit.asset |

## 更新する際の確認先

プロファイルの有無は [OpenBrushProfiles.cs](../Assets/OpenBrushResonite/OpenBrushProfiles.cs)、自動選択される補助データは [prepare-openbrush-bundle.py](../Support/Python/prepare-openbrush-bundle.py)、SDKの適用順とオプションは [OpenBrushSendModel.cs](../Assets/OpenBrushResonite/Editor/OpenBrushSendModel.cs) が根拠です。対応プロファイルを追加しただけで「実機検証済み」へ変更しないでください。

### 完成形モード

`prepare-openbrush-bundle.py --completed` は書き順の再生とボタンを省き、完成した線を最初から表示する。対応ブラシと入力の制約は上表と同じ。ブラシ固有の動きは残す。有限時間で焼き込む表現は既定8秒の反復になり、非周期的な模様は境界がつながらない場合がある。`--effect-loop-seconds` で0.1〜60秒に変更できる。書き順用の分割が減っても、ブラシの画像列・メッシュ列の容量上限は残る。


## 掲載漏れの再監査（2026年10月24日）

従来の「カタログ115種類」は全ブラシ定義の一覧ではありませんでした。次の二段階の不備がありました。

1. Open Brush側の `Support/Python/openbrush-material-inventory.py` がGUIDを `[0-9a-f-]` で抽出していました。BubbleWandの定義は大文字のGUIDだったため、Manifest_Experimentalに登録されているのに抽出時点で除外されていました。GUIDを大文字・小文字に関係なく正規化するよう修正しました。不正なGUIDは黙って除外せずエラーにします。
2. 対応表の生成元 `converter-coverage.json` は、その一覧のうちManifest登録分115項目だけを収録していました。Manifest未登録の18定義は別枠となり、その中で実装済みだったHypercolorSingleSidedだけを後から追加していました。未登録の残り17定義と、exportManifestにだけある4 GUIDが表に載っていませんでした。

このため、前の更新でBubbleWandを「カタログ外」と説明したのは不正確でした。現在はManifestへの登録状態で一覧から除外せず、定義・書き出しManifest・SDKプロファイルのGUIDの和集合で掲載を確認します。

| 再監査時に見つかった未対応項目（後述の実装前） | 件数 | 対象 |
| --- | --- | --- |
| SingleSidedという名前の別GUID版 | 12 | CoarseBristles、DuctTape、Flat、Gouache、Ink、OilPaint、Paper、Splatter、TaperedFlat、ThickPaint、WetPaint、WigglyGraphite。上表では末尾SingleSidedで区別。通常版は既存の対応状態のまま。 |
| Geometry版 | 4 | MarkerGeometry、OilPaintGeometry、PaperGeometry、TaperedMarkerGeometry |
| テスト用 | 1 | TubeUnlit |
| 書き出しManifestのみ | 4 | CandyCane、HolidayTree、Snowflake、Braid3 |

調査範囲は `open-brush-versetv` のAssets・Packages・導入済みcom.icosaパッケージ、3つのManifest、ソースと `Builds/OpenXR_Release_OpenBrush` のexportManifestです。書き出しManifestの登録はソース128、実行ビルド132 GUIDでした。Manifestのブラシ参照に未解決はなく、134定義にGUID重複もありません。全asset探索で見つかった非テキストの `Assets/Scenes/Loading/LightingData.asset` は照明データのため一覧化対象外です。

さらにユーザーのExports内160 GLBをJSONメタデータだけで確認しました。154ファイルから105種類のGUIDが読め、上記138種類の範囲外のGUIDはありませんでした。残り6ファイルは0バイトで読み取り不能です。`FunkyXmas-1.glb` のWaveformFFTは再監査時点で既に未対応表に掲載されており、新規の掲載漏れではありません。全バージョン・外部カスタムブラシの完全な一覧を保証する調査ではありません。

### 今後の掲載漏れチェック

SDKルートで実行します。入力データやResoniteワールドは変更しません。

```powershell
& $python Support/Python/audit-brush-support.py `
    --open-brush-root ../open-brush-versetv `
    --export-manifest ../Builds/OpenXR_Release_OpenBrush/Support/exportManifest.json `
    --output .local/brush-support-audit.json --check
```

必要に応じて `--glb-root 'C:/Users/star_/Documents/Open Brush/Exports'` も追加できます。GLBのバイナリ形状は読み込まず、JSONにあるブラシGUIDだけを照合します。0バイトなどの読めないファイルも報告し、`--check` はその場合にも失敗するため、失敗がすべて未掲載ブラシを意味するわけではありません。

チェックでは未掲載GUID、Manifestの未解決参照、定義・プロファイル・CSVのGUID重複、CSVとMarkdownの行の不一致、プロファイル有無と対応状態の矛盾を検出します。これは一覧の完全性の確認であり、プロファイルがあるだけで見た目や任意のスケッチの変換を保証するものではありません。従来のローカル `refresh-converter-coverage.py` は既存のリスト内の対応数を数え直すだけなので、掲載漏れの確認には使わないでください。


## 追加対応（2026年10月24日）

追加した19 GUIDはSingleSided別版12、Geometry別版4、WaveformFFT・WaveformTube・WaveformParticlesの3です。完成形と書き順の両経路に組み込み、通常GUIからも同じSDK・Pythonスクリプトを利用します。

比較ワールド12575にはStandard環境の方向光2灯・環境光と、元の空の式を焼き込んだ全天画像を適用しました。塗料系は5色・筆圧変化で比較。色、光沢、Bloomや透明面の重なりには差が残ります。Geometry別版4件は材質一致とGUID経路の確認であり、現行アプリでの独立実写確認は済んでいません。旧SingleSidedを現行アプリが新GUIDに置換する点も、比較記録に明記しています。

残る14項目は、モデル・環境用FakeBrush 8、現行アセットの見つからない旧exportManifest 4、MRパススルー1、通常マテリアル未設定のTubeUnlit 1です。これらを普通の描画ブラシとして対応済みに数えてはいません。モデル用8件には別のインポート処理が必要です。

検証はPython 83テスト、Unityの波形GPU/scalar比較と粒サイズ・座標検証が成功。12575で完成形と書き順を送信・撮影確認しました。`FunkyXmas-1`の元FFT部分11ストロークも送信成功しましたが、作品全体は別のDisco生形状チェックで停止します。FFT部分の成功を全作品の成功とは扱っていません。

## 未対応部分の扱い（2026年10月25日）

未対応のGUIDとGUIDがないメッシュ部分は、名前で別ブラシへ置き換えず省略します。GUI・CLIに警告を出し、変換結果とSDK送信レポートの `warnings` に名前、GUID、理由、プリミティブ数を保存します。完成形・書き順の両モードで共通です。すべて未対応なら空モデルを作らず終了します。対応済みブラシの不正な生データなど、未対応以外のエラーを無視する機能ではありません。音声反応の実装は対象外のままです。
