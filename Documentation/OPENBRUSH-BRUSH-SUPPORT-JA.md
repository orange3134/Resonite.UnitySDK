# Open Brush ブラシ対応表

対応一覧の基準日 2026年10月5日。容量制約の補足更新 2026年10月10日。対象はこのカスタムSDKの現在の実装です。Open Brushの通常の `m_Material` を基準に調整しており、Testing Materialは対象にしていません。

カタログ115種類中103種類に変換プロファイルがあります。カタログ外のHypercolorSingleSidedを含めると104プロファイルです。通常Manifestでは66項目中58項目で、残り8項目はBlocks・環境・モデル用の項目です。数は見た目の一致率や利用頻度を表しません。

[インポート手順書](OPENBRUSH-IMPORT-JA.md)と併せて参照してください。[CSV版](OPENBRUSH-BRUSH-SUPPORT.csv)には同名ブラシを区別するGUIDとSDK内部名も収録しています。

## 状態の読み方

| 状態 | 意味 |
| --- | --- |
| 対応あり | 基本の色・形状・書き順の変換経路あり。完全一致の保証ではない。 |
| 制約あり | 変換経路あり。ただし個別の近似・未再現・追加条件がある。 |
| 未対応 | プロファイルなし。含まれるGLBは現在の準備処理でエラーになる。 |

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
| CelVinyl | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| Charcoal | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| ChromaticWave | 制約あり | 動く模様・色・書き順 | 描画時計に同期した有限テクスチャ列。HDR抑制・細部の解像度制約。音声反応未対応。SparksとRainは形状補正あり。 | progress-flow-hdr／chromatic／fire-sparks／weather |
| CoarseBristles | 制約あり | 色・テクスチャ・書き順 | 照明差あり。CoarseBristlesの細い筆毛内部は色測定の十分な対象になっていない。 | プロファイル／塗料ブラシ比較 |
| Comet | 制約あり | 彗星状の模様・書き順 | 元の時間変化を近似。加算・発光・サンプリングの差が残る。 | progress-comet |
| ConcaveHull | 制約あり | 形状・色・材質・書き順 | 照明を補正した近似。Membraneは書き出しmanifestへの追加登録が必要な版あり。Hullの描画は完成面の表示近似。 | progress-solid-extra |
| DanceFloor | 制約あり | 形状・模様・位相・書き順 | 元の位相を保持。メッシュと模様のベイクが大容量になりやすい。 | progress-dancefloor |
| Dark | 制約あり | 背景を暗くする乗算表現・書き順 | Gammaの背景依存乗算を一定の補正で近似。任意の背景で完全一致しない。追加の書き出し登録が必要な版あり。 | progress-dark |
| DiamondHull | 制約あり | 透明材質・書き順／追加オプションで薄膜色変化 | 標準は簡易透明材質。薄膜は -openbrushDiamondFilm が必要。Matcapの角度・模様固定方向・光沢に差、三角形内色勾配は未対応。 | progress-diamond |
| DiamondMembrane | 制約あり | 透明材質・書き順／追加オプションで薄膜色変化 | 標準は簡易透明材質。薄膜は -openbrushDiamondFilm が必要。Matcapの角度・模様固定方向・光沢に差、三角形内色勾配は未対応。 | progress-diamond |
| Digital | 制約あり | 格子模様・色・書き順 | 長いUVに対応した静的アトラス。遠距離の細線や画面微分AAは異なる。 | progress-digital-race／digital-length |
| Disco | 制約あり | 変形・きらめき・複数ストロークの書き順 | 描画と変形を同期。光沢とGamma照明差が残る。三角形内の色勾配は未対応。 | progress-disco-multi／disco-smoothness |
| DotMarker | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| Dots | 制約あり | 粒子の形状・色・出現・書き順 | 生データから有限メッシュ列を作成。粒子数×時間で容量増。 | progress-dots |
| DoubleFlat | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| DoubleTaperedFlat | 制約あり | 両端の先細り形状・色・書き順 | 元シェーダーの形状補正を焼き込む。Flat側の陰影は近似。 | progress-experimental-paint |
| DoubleTaperedMarker | 制約あり | 両端の先細り形状・色・書き順 | 元シェーダーの形状補正を焼き込む。Flat側の陰影は近似。 | progress-experimental-paint |
| Drafting | 制約あり | 表示状態の模様・色・書き順 | DraftingVisibility=1の状態を再現。任意の可視性操作・音声反応は未対応。 | progress-additive-extra |
| DryBrush | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| DuctTape | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| DuctTapeGeometry | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Electricity | 制約あり | 電気の変形・色・書き順 | 元の生チャンネルが必須。有限メッシュ列、HDRとBloomは近似。 | progress-electricity／mixed-bundle |
| Embers | 制約あり | 粒の動き・描画時刻に応じた出現 | ネイティブ表現による近似。元のシミュレーションの完全再現ではない。 | BrushTest描画・粒子検証 |
| FacetedTube | 制約あり | 面ごとの色・書き順 | 元の面表現を変換。レンダラーと環境差に共通制約。 | progress-solid-extra |
| Fairy | 制約あり | 星雲等の動く模様・5色・書き順 | 元HDRが強すぎるため出力を抑制。Spaceの色・粒状感、Fairyの微細模様に差。音声反応なし。 | progress-fairy-space |
| Feather | 制約あり | 柔らかいテクスチャ・色・書き順 | アトラス端・ミップを修正。Featherに薄い三角形境界が残る。暗色と背景依存の加算差あり。 | progress-soft-mip-isolation |
| Fire | 制約あり | 炎模様の揺らぎ・色・書き順 | 模様の時計は描画と独立。停止中も揺らぐ。HDRを抑制。三角形内の色勾配は未対応でエラー。 | progress-fire-color／sampling |
| Fire2 | 制約あり | 動く模様・色・書き順 | 描画時計に同期した有限テクスチャ列。HDR抑制・細部の解像度制約。音声反応未対応。SparksとRainは形状補正あり。 | progress-flow-hdr／chromatic／fire-sparks／weather |
| Flat | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| FlatDeprecated | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Gouache | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Guts | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Highlighter | 制約あり | 色・マスク・書き順 | 元のフラグメント表現を焼き込む。UVの範囲が大きい場合は最大512サンプル/辺に制限し、16Mピクセルを超える場合はさらに輪郭用画像の解像度を下げる（最小16サンプル/辺）。細い輪郭や繰り返し模様は差が出る場合がある。重なりと背景依存の合成差が残る。 | progress-highlighter／sampling |
| HyperGrid | 制約あり | グリッドの形状・出現・色・書き順 | パディングとミップを修正済み。明るさ・鋭さが異なる。静止専用経路は未整備。 | progress-hypergrid-sampling |
| Hypercolor | 制約あり | 色変化する模様・書き順 | 有限ベイクとネイティブ材質。光沢・環境・色勾配の全条件での一致は未保証。 | progress-hypercolor |
| Icing | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Ink | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| InkGeometry | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| KeijiroTube | 制約あり | 進行する収縮・5色・書き順 | 描画と変形を同期。60fps有限ベイク。暗色・陰影差、ループ時の位相リセットあり。生の半径・接線が必要。 | progress-keijiro-23944 |
| Lacewing | 制約あり | 色・高いアルファしきい値・法線・書き順 | 通常材質のSpecularColorは黒で、元の動的反射項も寄与ゼロ。任意の材質変更は未対応。 | progress-lacewing |
| LeakyPen | 制約あり | 色・カットアウト・法線・書き順 | 5色と両面で調整。Gamma照明と暗色に近似が残る。 | progress-leakypen |
| Leaves | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Leaves2 | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Light | 制約あり | 発光・色・書き順 | Bloomと露出による発光の広がりは近似。 | プロファイル／BrushTest |
| LightWire | 制約あり | 膨らみ・走る光・色・書き順 | 光の時計は描画と独立。光沢・Bloom差、極端に長いUVの細部制約。 | progress-lightwire-fullhdr |
| Lofted | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| Lofted (Hue Shift) | 制約あり | 元の色相変換・書き順 | 元の頂点色を基に色変換。Lofted側のGamma照明を経験的補正。全照明一致ではない。 | progress-hueshift-brushtest |
| Marbled Rainbow | 制約あり | マーブル色変換・法線・書き順 | 元の色計算を保持。照明・光沢はネイティブPBS近似。 | progress-marbled |
| Marker | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| MatteHull | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| MatteMembrane | 制約あり | 形状・色・材質・書き順 | 照明を補正した近似。Membraneは書き出しmanifestへの追加登録が必要な版あり。Hullの描画は完成面の表示近似。 | progress-solid-extra |
| Muscle | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| MylarTube | 制約あり | くびれ・角度依存の光・時間変化・書き順 | 標準経路に実装済み。Matcapは真の視線方向を近似し、白・紫の明るさや帯の色が異なる。生半径が必須。三角形内の色勾配は未対応。 | progress-mylar |
| NeonPulse | 制約あり | 走る光・書き順 | 標準Fresnelの角度応答に差。追加オプションのMatcapで改善するが視線・反射は近似。 | progress-neon／neon-matcap |
| OilPaint | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| Paper | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
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
| SquarePaper | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Stars | 制約あり | 粒の点滅・HDR色・書き順 | HDR頂点色を保持。元のBloom・色変換・フィルタ差は残る。音声反応未対応。 | progress-stars-hdr／particles |
| Streamers | 制約あり | 動く模様・色・書き順 | 描画時計に同期した有限テクスチャ列。HDR抑制・細部の解像度制約。音声反応未対応。SparksとRainは形状補正あり。 | progress-flow-hdr／chromatic／fire-sparks／weather |
| Taffy | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| TaperedFlat | 制約あり | 色・テクスチャ・書き順 | 照明差あり。CoarseBristlesの細い筆毛内部は色測定の十分な対象になっていない。 | プロファイル／塗料ブラシ比較 |
| TaperedHueShift | 制約あり | 元の色相変換・書き順 | 元の頂点色を基に色変換。Lofted側のGamma照明を経験的補正。全照明一致ではない。 | progress-hueshift-brushtest |
| TaperedMarker | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| TaperedMarker_Flat | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| TaperedWire | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| ThickGeometry | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| ThickPaint | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| Toon | 制約あり | 塗りと輪郭・筆圧・書き順 | 元のworld Yによる明るさを焼き込む。輪郭幅は基準距離での近似。移動・回転・距離変更で差が出る。 | progress-toon |
| TubeAdditive | 制約あり | 加算合成・色・書き順 | 元より暗い箇所と、前後の面・背景との重なり方の差が残る。 | progress-additive-extra／soft-mip-isolation |
| TubeToonInverted | 制約あり | 黒い本体と色付き外殻・書き順 | 元の変形を焼き込む。任意の姿勢・拡縮や全視点での一致は未保証。 | progress-inverted-toon |
| UnlitHull | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| VelvetInk | 制約あり | 柔らかいテクスチャ・色・書き順 | アトラス端・ミップを修正。Featherに薄い三角形境界が残る。暗色と背景依存の加算差あり。 | progress-soft-mip-isolation |
| Waveform | 制約あり | 音声なしの波形模様・色・書き順 | 60fps有限ベイク、発光上限を調整。音声入力・FFT連動は再現しない。 | progress-waveform |
| WetPaint | 制約あり | 色・テクスチャ・書き順 | 照明・粗さ・透過境界はネイティブ材質への近似。全環境での一致は未保証。 | プロファイル／BrushTest・塗料ブラシ比較 |
| WigglyGraphite | 制約あり | 揺らぐ模様・色・書き順 | 模様の時計は描画と独立。色を補正したが明るさに差が残る。 | progress-sampling／BrushTest |
| Wind | 制約あり | 動く模様・色・書き順 | 描画時計に同期した有限テクスチャ列。HDR抑制・細部の解像度制約。音声反応未対応。SparksとRainは形状補正あり。 | progress-flow-hdr／chromatic／fire-sparks／weather |
| Wire | 対応あり | 色・形状・書き順 | 基本変換に対応。共通制約は適用される。 | プロファイル・描画変換実装／複数ブラシ比較 |
| Wire (Lit) | 制約あり | 色・カットアウト・表面材質・書き順 | 法線・片面性・光沢を調整。GammaとLinearの照明差、暗色やハイライト差が残る。 | progress-experimental-paint／paint2／lit-standard／solid-extra |
| Wireframe | 制約あり | 輪郭模様・色・書き順 | 基準背景に対する加算近似。任意背景・三角形内色勾配・音声・スクリプトのclip/dissolveは未対応または未検証。 | progress-wireframe |
| HypercolorSingleSided | 制約あり | Hypercolor系の追加GUIDの変換 | 名前に反して元descriptorは裏面も生成する。カタログ外の追加GUID。独立した全方向の実写検証は未確認。 | プロファイル／progress-hypercolor |

## 未対応

未対応ブラシを含む場合は、元のスケッチの複製からそのブラシを除く・置き換えるか、対応追加後に再出力します。未対応部分を黙って落として成功扱いにはしません。ただしOpen Brushのエクスポーター自体が出力を省略する場合はSDKでは検出できないため、元スケッチと完成形の比較が必要です。

| ブラシ | 状態 | 対応内容 | 制約とメモ | 確認根拠 |
| --- | --- | --- | --- | --- |
| BlocksBasic | 未対応 | 変換対象外 | Blocksモデル用材質。通常の描画ブラシとは別枠。 | カタログとプロファイル照合 |
| BlocksGem | 未対応 | 変換対象外 | Blocksモデル用材質。 | カタログとプロファイル照合 |
| BlocksGlass | 未対応 | 変換対象外 | Blocksモデル用透明材質。 | カタログとプロファイル照合 |
| EnvironmentDiffuse | 未対応 | 変換対象外 | 環境モデル用材質。 | カタログとプロファイル照合 |
| EnvironmentDiffuseLightMap | 未対応 | 変換対象外 | 環境モデルとライトマップ用材質。 | カタログとプロファイル照合 |
| PassthroughHull | 未対応 | 変換対象外 | パススルー表現の変換未実装。 | カタログとプロファイル照合 |
| PbrTemplate | 未対応 | 変換対象外 | インポートモデル用テンプレート。 | カタログとプロファイル照合 |
| PbrTransparentTemplate | 未対応 | 変換対象外 | 透明PBRモデル用テンプレート。 | カタログとプロファイル照合 |
| SvgTemplate | 未対応 | 変換対象外 | SVG用テンプレート。 | カタログとプロファイル照合 |
| WaveformFFT | 未対応 | 変換対象外 | FFT音声連動を含め未実装。通常のWaveformとは別。 | カタログとプロファイル照合 |
| WaveformParticles | 未対応 | 変換対象外 | 粒子型Waveform未実装。 | カタログとプロファイル照合 |
| WaveformTube | 未対応 | 変換対象外 | チューブ型Waveform未実装。 | カタログとプロファイル照合 |

## 更新する際の確認先

プロファイルの有無は [OpenBrushProfiles.cs](../Assets/OpenBrushResonite/OpenBrushProfiles.cs)、自動選択される補助データは [prepare-openbrush-bundle.py](../Support/Python/prepare-openbrush-bundle.py)、SDKの適用順とオプションは [OpenBrushSendModel.cs](../Assets/OpenBrushResonite/Editor/OpenBrushSendModel.cs) が根拠です。対応プロファイルを追加しただけで「実機検証済み」へ変更しないでください。

### 完成形モード

`prepare-openbrush-bundle.py --completed` は書き順の再生とボタンを省き、完成した線を最初から表示する。対応ブラシと入力の制約は上表と同じ。ブラシ固有の動きは残す。有限時間で焼き込む表現は既定8秒の反復になり、非周期的な模様は境界がつながらない場合がある。`--effect-loop-seconds` で0.1〜60秒に変更できる。書き順用の分割が減っても、ブラシの画像列・メッシュ列の容量上限は残る。
