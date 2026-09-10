# Unity 2D Shader Graph Effects

Unity 6.5 と Universal Render Pipeline（URP）の 2D Renderer を使った、12種類の実践的な Shader Graph 演出サンプルです。

各演出は独立したシーンとして収録しており、同じ素材の Before / After を比較しながら確認できます。ゲーム向けのヒットフラッシュ、パレット交換、ディゾルブ、アウトライン、グリッチ、2Dライト、反射、ワールド座標を使った走査演出などを扱います。

## 動作環境

- Unity `6000.5.9f1`
- Universal RP / Shader Graph `17.5.0`
- Universal 2D Renderer
- Unity Recorder `5.1.7`（撮影する場合のみ）

Unity Hub から、このリポジトリのルートフォルダをプロジェクトとして開いてください。初回起動時は Package Manager による依存パッケージの解決とアセットのインポートに時間がかかる場合があります。

## 実行方法

1. Unity メニューの `Tools > Shader Capture > Open Capture Hub` を選択します。
2. Play Mode に入ります。
3. Hub のボタン、またはキーボードで演出シーンを開きます。

Hub では数字キー `1`～`9`、`0`、`-`、`=` が Scene 01～12 に対応します。

各演出シーンでは次の操作を利用できます。

| キー | 動作 |
|---|---|
| `Space` | 自動再生／一時停止 |
| `R` | 時刻、演出量、表示位置、HUDを初期状態へ戻して停止 |
| `Left` / `Right` | 演出量を手動調整 |
| `B` | Shader Output を中央に移動して単独表示 |
| `H` | HUD の表示／非表示 |
| `PageUp` / `PageDown` | 前後の演出シーンへ移動 |

## 収録している演出

| # | シーン | 内容 |
|---:|---|---|
| 01 | Hit Flash / Invincible | HDRカラーによるヒットフラッシュと無敵点滅 |
| 02 | Palette Swap | インデックスマップを使ったパレット交換 |
| 03 | Dissolve | ノイズによる消滅・出現と境界発光 |
| 04 | Outline / Inner Rim | 近傍サンプリングによる外周・内周表現 |
| 05 | Wind / Vertex Squash | 頂点変形による風揺れとつぶれ表現 |
| 06 | Glitch / RGB Split | RGB分離とライン単位のグリッチ |
| 07 | Pixel / Posterize / Dither | ピクセル化、減色、ディザリング |
| 08 | Hologram / Shine | 走査線、グリッチ、斜めの光沢 |
| 09 | Normal Map 2D Light | 法線マップと Light2D の組み合わせ |
| 10 | Mask Map Lighting | RGBマスクによる領域別ライティング |
| 11 | Water Reflection | 上下反転とUV変形による水面反射 |
| 12 | World Scan Reveal | ワールド座標を横断する円形スキャン |

## プロジェクト構成

- `Assets/ShaderCapture/Scenes/` — Capture Hub と12の演出シーン
- `Assets/ShaderCapture/Shaders/Graphs/` — 各演出の Shader Graph
- `Assets/ShaderCapture/Art/Generated/` — サンプル用の生成素材
- `Assets/ShaderCapture/Settings/Recorder/` — 1920×1080の静止画・動画設定
- `Documentation/SCENE_CATALOG.md` — 各シーンの詳しい構成
- `Documentation/CAPTURE_GUIDE.md` — スクリーンショット・動画の撮影手順

Scene 06 の Custom Function を除き、各演出は Shader Graph の標準ノードで構成されています。

## 撮影

Game View を 1920×1080 に設定するには、`Tools > Shader Capture > Set Game View 1920x1080` を使用します。静止画・動画の詳しい手順は [Documentation/CAPTURE_GUIDE.md](Documentation/CAPTURE_GUIDE.md) を参照してください。

## 補足

- サンプル素材は、このプロジェクト用に生成した仮素材です。
- Unityが生成する `Library`、`Temp`、`Logs`、録画・レビュー成果物はリポジトリに含めていません。
- Shader Graph のプロパティやノード構成は、各シーンと `Assets/ShaderCapture/Shaders/Graphs/` から確認できます。
