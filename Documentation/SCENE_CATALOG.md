# Shader Capture シーンカタログ

対象環境は Unity `6000.5.9f1`、URP / Shader Graph `17.5.0`、Universal 2D Renderer です。各演出は `Assets/ShaderCapture/Scenes/` に独立したシーンとして保存されています。

| Build index | シーン | Graph | 撮影で確認する内容 |
|---:|---|---|---|
| 0 | `00_CaptureHub` | - | 12シーンのランチャー、数字キー操作 |
| 1 | `01_HitFlashInvincible` | Sprite Unlit | HDR フラッシュ、Bloom、3つの seed offset |
| 2 | `02_PaletteSwap` | Sprite Unlit | Point Filter の Index Map と4行 Palette |
| 3 | `03_Dissolve` | Sprite Unlit | Hard／Soft境界とDissolve／Revealの4分割比較 |
| 4 | `04_OutlineInnerRim` | Sprite Unlit | 4方向／8方向、Tight／Full Rect の比較 |
| 5 | `05_WindVertexSquash` | Sprite Unlit | 4頂点と169頂点の変形差、足元固定、Fragment UV揺れ |
| 6 | `06_GlitchRgbSplit` | Sprite Unlit | 唯一の Custom Function、RGB 分離、float／half wrapper |
| 7 | `07_PixelPosterizeDither` | Sprite Unlit | Local／Screen dither、Effect 0 の恒等性 |
| 8 | `08_HologramShine` | Sprite Unlit | 低周波glitch、水平走査線、独立した斜めShine、Effect 0の恒等性 |
| 9 | `09_NormalMap2DLight` | Sprite Lit | 平面法線と `_NormalMap` Secondary Texture、同期移動する Light2D |
| 10 | `10_MaskMapLighting` | Sprite Lit | `_MaskTex`のR=Wet、G=Blend Style Light2D Metal、B=Weak Pulse比較 |
| 11 | `11_WaterReflection` | Sprite Unlit | 上側主体を固定し、下側反射だけを歪ませる構成 |
| 12 | `12_WorldScanReveal` | Sprite Unlit | World Position の中心／半径による複数 Sprite 横断走査 |

## 共通操作

| キー | 動作 |
|---|---|
| `Space` | 自動再生／完全停止。停止中は `_SC_DemoTime` も進まない |
| `R` | 演出量／時刻を0、Before／HUD／位置を初期状態へ戻し、一時停止 |
| `Left` / `Right` | 演出量を手動変更 |
| `B` | Shader Output を中央へ移して単独表示 |
| `H` | HUD 表示切り替え |
| `PageUp` / `PageDown` | 前後の演出シーンへ移動 |

## 生成素材

- `DemoCharacter.png`: 通常 Sprite。`_NormalMap` と `_MaskTex` を Secondary Texture として登録
- `DemoCharacter_Flat.png`: Scene 09 の平面法線比較用 Sprite
- `DemoCharacter_Tight.png`: Tight Mesh 比較用 Sprite
- `DemoCharacter_Index.png`: Palette Swap 用。Point／sRGB OFF／Mip Map OFF
- `DemoCharacter_Normal.png`, `FlatNormal.png`: 2D Normal Map
- `DemoCharacter_Mask.png`: RGB チャンネル別 Mask Map
- `Noise.png`, `Palette.png`: Dissolve／Glitch／Palette 用データテクスチャ
- `DemoCharacter_QuadMesh.asset`: 4頂点
- `DemoCharacter_SubdividedMesh.asset`: 169頂点
- `ShaderCapture.spriteatlas`: Rotation OFF、Tight Packing OFF、Padding 8
