# Unity 6.5 compatibility record

## Confirmed baseline

- Unity Editor: `6000.5.9f1`
- Universal Render Pipeline: `17.5.0`
- Shader Graph: `17.5.0`
- 2D Animation: `15.1.0`
- Rendering backend during verification: DirectX 12
- Project template: Universal 2D
- Renderer: URP 2D Renderer

`Packages/manifest.json`, `Packages/packages-lock.json`, and the loaded package `package.json` files are required to resolve to the same URP / Shader Graph major-minor version before shader implementation begins.

## Difference from the reviewed article

The reviewed article targets Unity 6.4 / Shader Graph 17.4. This demo project intentionally targets the user-provided Unity 6.5 project and therefore uses the built-in Unity 6.5 package line, Shader Graph 17.5.

The implementation must re-check these article-facing contracts in 17.5:

- Sprite Unlit Shader Graph and Sprite Lit Shader Graph template names
- Sprite Target blocks: Base Color, Alpha, Sprite Mask, Normal (Tangent Space)
- Graph settings: Blending Mode, Depth Write, Alpha Clipping, Disable Color Tint
- Blackboard Property Scope values and Read Only / PerRendererData behavior
- Texture 2D `Set as Main Texture` and `Use TexelSize`
- Custom Function File mode precision suffixes (`_float`, `_half`)
- Sprite Skinning node prerequisites and ports
- Dither Screen Position input
- Secondary Texture names `_NormalMap` and `_MaskTex`
- 2D Light Normal Map and Blend Style settings
- Sprite Atlas Allow Rotation, Tight Packing, Alpha Dilation, and Padding

## Compatibility policy

- Do not silently copy screenshots or UI wording from Unity 6.4 when the Unity 6.5 Editor differs.
- Record every visible UI-name or behavior difference in this file and `SCENE_CATALOG.md`.
- Keep the demo behavior equivalent to the reviewed formulas even when the graph layout or UI label changes.
- Do not modify the reviewed article as part of this project unless the user separately requests article synchronization.
