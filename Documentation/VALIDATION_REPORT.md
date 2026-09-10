# Unity Shader Capture 検証報告

検証日: 2026-08-25  
対象: 本リポジトリのUnityプロジェクト

## 環境

| 項目 | 値 |
|---|---|
| Unity | 6000.5.9f1 |
| Render Pipeline | Universal RP 17.5.0 / Universal 2D Renderer |
| Shader Graph | 17.5.0 |
| Recorder | 5.1.7 |
| Graphics API | DirectX 12 |

## 自動テスト

| Suite | 結果 | 主な確認内容 |
|---|---:|---|
| EditMode | **20 / 20 passed** | 12 Graph／Material、Scene構造、4／169頂点、UV-only／Squash-only、Effect 0 Alpha、斜めShine、Hard／Soft／Reveal、Normal／Mask、Scene 08／09／11／12ピクセル回帰、決定論的PNG |
| PlayMode | **4 / 4 passed** | 停止中の時刻固定、Reset、Before 非表示時の中央配置、実Light2D ON/OFFでG Metalだけが変化するピクセル因果テスト |

## Unity Editor 確認

- C# compile error: 0
- Shader compile error: 0
- Console error: 0
- Console warning: 0
- 12 Shader Graph の生成／Import: 成功
- Scene 01～05、07～12: Shader Graph ネイティブノードのみ
- Scene 06: Custom Function 1個のみ。`Effect06Fragment_float`／`Effect06Fragment_half` を実装
- `_Time` 依存: なし。全演出を `_SC_DemoTime` で制御
- `_NormalMap`／`_MaskTex`: PerRendererData と Secondary Sprite Texture を使用
- Game View 1920x1080 設定コマンド: 実装済み
- Source ZIP を Library のない別フォルダへ展開し、Unity 6000.5.9f1／DirectX 12 の batchmode で再 Import: 成功
- Round 3 Source ZIPをLibraryなしの別フォルダへ展開し、Unity 6000.5.9f1／DirectX 12で再Import: 成功
- 展開済みRound 3 Source ZIPに対するEditMode再実行: **18 / 18 passed**
- Round 4 Source ZIPをLibraryなしの別フォルダへ展開し、Unity 6000.5.9f1／DirectX 12で再Import: 成功
- 展開済みRound 4 Source ZIPに対するEditMode再実行: **18 / 18 passed**

## Round 4レビュー（95/100）への対応

- Scene 10: `_SC_MetalLightPosition` と手動Base Color Gハイライトを完全に削除。G Metalだけを専用`MaskMetal` Sorting Layerへ配置し、Blend Style 3 Light2Dの`targetSortingLayers`を同Layerだけに限定。
- Scene 10: PlayModeの実フレーム上でLight2DをON/OFFし、G Metalだけにピクセル差が発生し、R Wet／B Weakには差がないことを検証。B Weakの時間PulseはLight2D無効時にも独立して変化することをEditModeで検証。
- Scene 05: `_SC_UVWiggleAmount`／`_SC_VertexWindAmount`／`_SC_SquashAmount`をPerRendererData化し、4V／169V Vertex-only、UV-only、Squash-only、All Pathsを同一Sceneで分離表示。UV-onlyの時間ピクセル差も検証。
- Solo Capture: 非対象側のPanel／Headingを無効化し、対象側のPanel／Heading／被写体だけを中央へ移動して撮影後に復元。
- Scene 08／09／11／12: Hologram時間差、Flat／Normal比較、Stable Subject不変＋Reflection時間差、World Scan Reveal差を実描画ピクセルテスト化。
- Scene 09: Normal差を読み取りやすい専用visual cropをEvidence ZIPへ追加。
- Round 5最終検証: EditMode **20 / 20 passed**、PlayMode **4 / 4 passed**、Console error／warning **0 / 0**。
- Round 5 Source ZIPを`Library`なしの別フォルダへ展開し、Unity 6000.5.9f1／DirectX 12で初回Import: **成功**。同じclean展開物のEditMode再実行: **20 / 20 passed**。

## Round 1 Major 指摘への対応

1. 共通 HLSL ラッパーを廃止し、Scene 06 以外をネイティブノード化。
2. Pause／Reset／自動撮影を `_SC_DemoTime` へ統一。
3. Scene 05 を4頂点対169頂点にし、UV と Sprite Size による足元固定変形を実装。
4. Scene 07 を Local／Screen dither の同時比較にし、Effect 0 を恒等化。
5. Scene 09 を同一 Sprite Lit Graph／Material、平面法線と実法線の Secondary Texture、同期移動する法線対応 Light2D と可視マーカーに変更。
6. Scene 10 を各 Sprite の `_MaskTex` と R／G／B チャンネル比較に変更。
7. Scene 11 の上側主体を BaseSprite で固定し、下側 Reflection のみ Effect11 を適用。
8. Source ZIP に `Assets/Settings`、`ProjectSettings`、`Packages`、`Tools`、`Documentation` を含めるよう変更。
9. 36枚のフル解像度 PNG、12 Graph 画像、48秒の全体動画、8本の専用動画を Round 2 証拠へ含めるよう変更。

## Round 2レビュー（83/100）への対応

- Scene 04: `_MainTex`を明示再サンプルし、Effect 0で本体Alphaを維持。4-Way／8-Wayそれぞれの非透明画素数を回帰テスト化。
- Scene 06: Custom Function内で基準RGBAを直接サンプルし、Effect 0の黒いFull-Rectを解消。背景Alphaと非透明面積を回帰テスト化。
- Scene 08: Diagonal UV、時間移動、Fraction、両端Smoothstep、Alpha Mask、HDR Emissionからなる独立した斜めShineを追加。
- Scene 03: Hard／SoftとDissolve／Revealの4分割比較を追加。
- Scene 05: TextureImporterのSprite Mesh Dataを使い、SpriteRenderer上で4頂点／169頂点を確実に比較。
- Scene 09: Global Fill Lightを上げ、Flat／Normal差を原寸画像で判別しやすくした。
- Scene 10: R=静的Wet、G=移動Light方向に依存するMetal、B=時間PulseするWeak Pointへ意味を分離。
- Solo Capture: After専用ラベル、Waterline、Scene 12 Scan TargetをAfter配下へ統合。
- Reset: Before、HUD、After位置、Effect、DemoTimeを同時に初期化し、一時停止する定義へ統一。
- Bundle: Manifest全項目へ`modifiedUtc`を追加し、Review Requestへ前回スコアを記載。主要Graphの拡大画像も同梱する。

## Round 3レビュー（90/100）への対応

- Scene 03: Dissolve／RevealそれぞれでVisibleとBody境界を同方向へ計算し、Effect 0／1でBodyとEdgeを明示的に固定。4種類の端点を実描画テスト化。
- Scene 10: Sprite LitテンプレートのSprite Mask Propertyを`_MaskTex`へ統合し、SurfaceDescription.SpriteMaskへSecondary Textureを接続。Renderer2D Blend Style 2／3をGチャンネルに設定。
- Scene 10 Metal: Blend Style 3のLight2DをDeterministicLightRigで移動し、実Light Transformから`_SC_MetalLightPosition`を設定。DemoTime単独ではG Metalが移動しない実描画テストを追加。
- Scene 05: SpriteRenderer化した4／169頂点比較へFragment UV再サンプルを追加し、Effect 0では元UVへ一致。
- Solo Capture: Scene 09はBefore／Afterごとに対応LightとMarkerを同じGroupへ格納。Scene 10のMetal LightもAfter配下へ格納。
- Scene 04: 4／8方向 × Tight／Full Rectの2×2比較へ変更し、条件を独立化。
- Bundle: Test Runner XMLとConsole Receiptを実測解析し、失敗・古い証拠・Console Error／Warningで生成を停止。`__pycache__`／`.pyc`をSource ZIPから除外。

## 再現性

- `ShaderCaptureProjectGenerator` が仮素材、Material、Scene、Atlas、Recorder 設定を決定論的に生成します。
- 自動撮影は30 fpsのフレーム番号から演出量と `_SC_DemoTime` を直接計算します。
- 同一 Scene／Effect／DemoTime を2回 RenderTexture へ描画した PNG バイト列の一致を EditMode テストで確認しています。
- Review Bundle は全ファイルの byte size と SHA-256 を `MANIFEST.json`／`SHA256SUMS.txt` に記録します。

最終記事用のカット選定と本番撮影は利用者が行います。本報告の自動撮影物は実装検証と GPT Pro 添削用です。
