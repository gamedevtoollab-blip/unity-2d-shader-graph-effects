# 撮影ガイド

## 1. Game View とシーンを準備する

1. `Tools > Shader Capture > Set Game View 1920x1080` を実行します。
2. `Tools > Shader Capture > Open Capture Hub` を実行し、Play Mode に入ります。
3. Hub のボタン、または数字キー `1`～`9`、`0`、`-`、`=` で対象シーンを開きます。

## 2. 静止画を撮る

演出を一時停止する場合は `Space` を押します。`Left` / `Right` で演出量を調整し、必要なら `H` で HUD、`B` で Before を隠します。`R` は演出量と `_SC_DemoTime` をゼロへ戻し、Before／HUD／Shader Output位置を初期化して一時停止します。

Recorder では `Assets/ShaderCapture/Settings/Recorder/Still_1920x1080_PNG.asset` を使用します。簡易確認には `Tools > Shader Capture > Take Reference Screenshot` も使用できます。

## 3. 動画を撮る

- 30 fps: `Movie_1920x1080_30fps_H264.asset`
- 60 fps: `Movie_1920x1080_60fps_H264.asset`

Scene 01、03、06、08、09、10、11、12 は時間変化が重要なため、専用クリップを撮ります。Scene 09 では黄色いマーカーが左右で同じ軌道を移動し、平面法線と実法線の入力差を比較できます。

## 4. 自動レビュー素材を生成する

1. `Tools > Shader Capture > Generate Shader Graph Evidence` を実行し、12グラフの全体画像を生成します。
2. `Tools > Shader Capture > Generate Review Captures` を実行します。
3. プロジェクトルートで `python Tools/build_review_bundle.py` を実行します。

`ReviewBundles/round-NN/` に、36枚のフル解像度 PNG、12枚の Shader Graph 画像、48秒の全体動画、8本の専用動画、完全なソース ZIP、証拠 ZIP、SHA-256 マニフェストが生成されます。

自動動画は各シーン4秒、30 fpsです。最初の0.5秒を Effect 0、次の2秒を 0→1、0.5秒を Effect 1、最後の1秒を 1→0 とし、`_SC_DemoTime` をフレーム番号から直接設定します。

## 5. トラブルシュート

- Shader がマゼンタ: Console の Shader error を確認し、対象 Graph を Reimport します。
- Normal Map が見えない: Sprite Lit Graph、`_NormalMap` Secondary Texture、Light2D の `Use Normal Map` と Normal Map Distance を確認します。
- データテクスチャの境界がにじむ: sRGB OFF、Mip Map OFF、Clamp、必要なものは Point Filter を確認します。
- 頂点変形が硬い: Scene 05 の After が169頂点 Mesh を使用していることを確認します。
