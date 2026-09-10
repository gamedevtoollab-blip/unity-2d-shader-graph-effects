using System;
using System.Linq;
using System.Reflection;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace ShaderCapture.Editor
{
    static class ShaderCaptureNativeGraphBuilder
    {
        public static bool TryBuildFragment(GraphData graph, int effectNumber, SlotReference baseRgba,
            BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            if (effectNumber is 1 or 2 or 3 or 4 or 5 or 7 or 8 or 9 or 10 or 11 or 12)
            {
                var spriteUv = Add<UVNode>(graph, "Sprite UV", -1250, -80);
                var spriteSample = SampleTexture(graph, "Sprite Texture", "_MainTex", true, Out(spriteUv, 0), -1010, -80);
                baseRgba = Out(spriteSample, 0);
            }
            return effectNumber switch
            {
                1 => BuildHitFlash(graph, baseRgba, baseColorBlock, alphaBlock),
                2 => BuildPaletteSwap(graph, baseRgba, baseColorBlock, alphaBlock),
                3 => BuildDissolve(graph, baseRgba, baseColorBlock, alphaBlock),
                4 => BuildOutline(graph, baseRgba, baseColorBlock, alphaBlock),
                5 => BuildWindFragment(graph, baseRgba, baseColorBlock, alphaBlock),
                7 => BuildPixelDither(graph, baseRgba, baseColorBlock, alphaBlock),
                8 => BuildHologram(graph, baseRgba, baseColorBlock, alphaBlock),
                9 => BuildNormalMapLighting(graph, baseRgba, baseColorBlock, alphaBlock),
                10 => BuildMaskLighting(graph, baseRgba, baseColorBlock, alphaBlock),
                11 => BuildWaterReflection(graph, baseRgba, baseColorBlock, alphaBlock),
                12 => BuildWorldScan(graph, baseRgba, baseColorBlock, alphaBlock),
                _ => false
            };
        }

        public static bool TryBuildVertex(GraphData graph, int effectNumber, BlockNode positionBlock)
        {
            if (effectNumber != 5) return false;
            var position = Add<PositionNode>(graph, "Position (Object)", -900, 260);
            SetCoordinateSpace(position, CoordinateSpace.Object);
            var uv = Add<UVNode>(graph, "UV0", -900, 430);
            var positionSplit = Add<SplitNode>(graph, "Position XYZ", -650, 260);
            Connect(graph, Out(position, 0), In(positionSplit, 0));
            var uvSplit = Add<SplitNode>(graph, "UV XY", -650, 430);
            Connect(graph, Out(uv, 0), In(uvSplit, 0));
            var effect = GlobalFloat(graph, "Effect", "_SC_Effect", 0f, -900, 600);
            var demoTime = GlobalFloat(graph, "Deterministic Demo Time", "_SC_DemoTime", 0f, -900, 740);
            var seed = GlobalFloat(graph, "Scene Seed", "_SC_Seed", .173f, -900, 880);
            var vertexWindAmount = PerRendererFloat(graph, "Vertex Wind Amount", "_SC_VertexWindAmount", 1f, -900, 1160);
            var squashAmount = PerRendererFloat(graph, "Squash Amount", "_SC_SquashAmount", 1f, -900, 1300);
            var spriteSize = GlobalVector4(graph, "Sprite Size", "_SC_SpriteSize", new Vector4(2.56f, 2.56f, 1f / 2.56f, 1f / 2.56f), -900, 1020);
            var spriteSizeSplit = Add<SplitNode>(graph, "Sprite Size XY", -650, 1020);
            Connect(graph, spriteSize, In(spriteSizeSplit, 0));

            var phase = AddOp(graph,
                AddOp(graph, Multiply(graph, Out(uvSplit, 1), Constant(graph, 8f, -410, 560), "UV.y x8", -180, 540),
                    Multiply(graph, demoTime, Constant(graph, 3.2f, -410, 700), "Time x3.2", -180, 680), "Wave Phase", 60, 600),
                Multiply(graph, seed, Constant(graph, 6.2831853f, -410, 840), "Seed x Tau", -180, 820), "Deterministic Phase", 300, 650);
            var wave = Multiply(graph,
                Multiply(graph, Sine(graph, phase, "Sine Wave", 520, 650), Constant(graph, .22f, 300, 820), "Wave Amplitude", 750, 690),
                Multiply(graph, Multiply(graph, effect, vertexWindAmount, "Effect x Vertex Wind", 300, 900), Out(uvSplit, 1), "Wind x Height", 520, 900), "Top Weighted Wave", 980, 760);
            var squash = Multiply(graph,
                Sine(graph, Multiply(graph, effect, Constant(graph, Mathf.PI, -180, 1000), "Effect x Pi", 60, 980), "Squash Curve", 300, 980),
                Multiply(graph, Constant(graph, .23f, 300, 1120), squashAmount, "Per Renderer Squash", 520, 1140), "Squash", 750, 1020);
            var bottomY = Subtract(graph, Out(positionSplit, 1), Multiply(graph, Out(uvSplit, 1), Out(spriteSizeSplit, 1), "UV Height", -180, 1160), "Bottom Pivot", 60, 1160);
            var newX = AddOp(graph, Multiply(graph, Out(positionSplit, 0), AddOp(graph, Constant(graph, 1f, 520, 1160), squash, "1 + Squash", 750, 1120), "Widen", 980, 1080), wave, "Wind Position X", 1210, 900);
            var newY = AddOp(graph, bottomY,
                Multiply(graph, Subtract(graph, Out(positionSplit, 1), bottomY, "Height from Bottom", 520, 1280),
                    OneMinus(graph, squash, "1 - Squash", 750, 1340), "Bottom Pivot Squash", 980, 1280), "Fixed Foot Y", 1210, 1220);
            var combined = Vector3(graph, newX, newY, Out(positionSplit, 2), "Vertex Position", 1450, 1050);
            Connect(graph, combined, positionBlock.GetSlotReference(0));
            return true;
        }

        static bool BuildPaletteSwap(GraphData graph, SlotReference baseRgba, BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            var uv = Add<UVNode>(graph, "UV0", -950, 100);
            var indexSample = SampleTexture(graph, "Index Map", "_SC_IndexTex", false, Out(uv, 0), -700, 40);
            var indexSplit = Add<SplitNode>(graph, "Index ID / Shade", -450, 40);
            Connect(graph, Out(indexSample, 0), In(indexSplit, 0));
            var id = Floor(graph, Multiply(graph, Saturate(graph, Out(indexSplit, 0), "Clamp ID", -220, -20), Constant(graph, 3.999f, -220, 100), "ID x4", 20, 20), "Palette Cell", 250, 20);
            var paletteX = Divide(graph, AddOp(graph, id, Constant(graph, .5f, 250, 160), "Cell Center X", 480, 80), Constant(graph, 4f, 480, 200), "Palette X", 710, 90);
            var row = GlobalFloat(graph, "Palette Row", "_SC_PaletteRow", 0f, -450, 300);
            var paletteY = Divide(graph, AddOp(graph, row, Constant(graph, .5f, -220, 360), "Row Center Y", 20, 320), Constant(graph, 4f, 20, 430), "Palette Y", 250, 350);
            var paletteUv = Vector2(graph, paletteX, paletteY, "Palette Cell UV", 920, 180);
            var palette = SampleTexture(graph, "Palette Texture", "_SC_PaletteTex", false, paletteUv, 1150, 160);
            var shade = Lerp(graph, Constant(graph, .58f, 480, 500), Constant(graph, 1.22f, 480, 580), Out(indexSplit, 1), "Preserve Index Shade", 720, 520);
            var color = Multiply(graph, Out(palette, 0), shade, "Palette x Shade", 1400, 280);
            Connect(graph, color, baseColorBlock.GetSlotReference(0));
            Connect(graph, Alpha(graph, baseRgba, -200, 650), alphaBlock.GetSlotReference(0));
            return true;
        }

        static bool BuildDissolve(GraphData graph, SlotReference baseRgba, BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            var uv = Add<UVNode>(graph, "UV0", -950, 100);
            var noiseUv = Multiply(graph, Out(uv, 0), Constant(graph, 1.3f, -950, 260), "Noise Tiling", -700, 150);
            var noise = SampleTexture(graph, "Dissolve Noise", "_SC_NoiseTex", false, noiseUv, -450, 100);
            var effect = GlobalFloat(graph, "Effect", "_SC_Effect", 0f, -700, 380);
            var softness = PerRendererFloat(graph, "Dissolve Softness", "_SC_DissolveSoftness", 0f, -700, 520);
            var invert = PerRendererFloat(graph, "Invert Dissolve / Reveal", "_SC_DissolveInvert", 0f, -700, 660);
            var threshold = Lerp(graph, Constant(graph, -.15f, -450, 420), Constant(graph, 1.15f, -450, 500), effect, "Dissolve Threshold", -180, 400);
            var hardVisible = Step(graph, threshold, Out(noise, 1), "Hard Step Visible", 70, 120);
            var softVisible = Smoothstep(graph, Out(noise, 1),
                Subtract(graph, threshold, softness, "Threshold - Softness", -180, 620),
                AddOp(graph, threshold, softness, "Threshold + Softness", -180, 720),
                "Soft Smoothstep Visible", 70, 260);
            var useSoftness = Step(graph, Constant(graph, .001f, -180, 820), softness, "Use Soft Boundary", 70, 780);
            var dissolveVisible = Lerp(graph, hardVisible, softVisible, useSoftness, "Hard / Soft Visible", 300, 180);
            var edgeWidth = Constant(graph, .075f, -180, 920);
            var dissolveBodyThreshold = AddOp(graph, threshold, edgeWidth, "Dissolve Body Threshold", 70, 500);
            var hardDissolveBody = Step(graph, dissolveBodyThreshold, Out(noise, 1), "Hard Dissolve Body", 300, 460);
            var softDissolveBody = Smoothstep(graph, Out(noise, 1),
                Subtract(graph, dissolveBodyThreshold, softness, "Dissolve Body - Softness", 70, 620),
                AddOp(graph, dissolveBodyThreshold, softness, "Dissolve Body + Softness", 70, 720),
                "Soft Dissolve Body", 300, 620);
            var dissolveBody = Lerp(graph, hardDissolveBody, softDissolveBody, useSoftness, "Hard / Soft Dissolve Body", 530, 520);
            var revealVisible = OneMinus(graph, dissolveVisible, "Reveal Visible", 300, 340);
            var revealBodyThreshold = Subtract(graph, threshold, edgeWidth, "Reveal Body Threshold", 300, 820);
            var hardRevealBody = OneMinus(graph, Step(graph, revealBodyThreshold, Out(noise, 1), "Hard Reveal Boundary", 530, 820), "Hard Reveal Body", 760, 800);
            var softRevealBoundary = Smoothstep(graph, Out(noise, 1),
                Subtract(graph, revealBodyThreshold, softness, "Reveal Body - Softness", 530, 960),
                AddOp(graph, revealBodyThreshold, softness, "Reveal Body + Softness", 530, 1060),
                "Soft Reveal Boundary", 760, 980);
            var revealBody = Lerp(graph, hardRevealBody, OneMinus(graph, softRevealBoundary, "Soft Reveal Body", 990, 980), useSoftness, "Hard / Soft Reveal Body", 990, 820);
            var visibleDirectional = Lerp(graph, dissolveVisible, revealVisible, invert, "Dissolve / Reveal Visible", 760, 220);
            var bodyDirectional = Lerp(graph, dissolveBody, revealBody, invert, "Dissolve / Reveal Body", 1220, 560);
            var atStart = OneMinus(graph, Smoothstep(graph, effect, 0f, .001f, "Effect Start Gate", 990, 160), "At Effect 0", 1220, 140);
            var atEnd = Smoothstep(graph, effect, .999f, 1f, "Effect End Gate", 990, 260);
            var startTarget = OneMinus(graph, invert, "Start Visible Target", 1220, 300);
            var visibleStartFixed = Lerp(graph, visibleDirectional, startTarget, atStart, "Exact Start Visible", 1450, 220);
            var bodyStartFixed = Lerp(graph, bodyDirectional, startTarget, atStart, "Exact Start Body", 1450, 520);
            var visible = Lerp(graph, visibleStartFixed, invert, atEnd, "Exact End Visible", 1680, 220);
            var body = Lerp(graph, bodyStartFixed, invert, atEnd, "Exact End Body", 1680, 520);
            var edge = Maximum(graph, Subtract(graph, visible, body, "Visible - Body", 520, 260), Constant(graph, 0f, 520, 380), "Edge", 750, 280);
            var baseSplit = Add<SplitNode>(graph, "Base RGBA", 70, 700);
            Connect(graph, baseRgba, In(baseSplit, 0));
            var bodyColor = Multiply(graph, baseRgba, body, "Body Color", 520, 600);
            var edgeColor = Multiply(graph, GlobalVector4(graph, "Edge Color", "_SC_ColorB", new Vector4(1f, .18f, .42f, 1f), 300, 820), Multiply(graph, edge, Constant(graph, 2.5f, 300, 940), "Edge HDR", 520, 850), "Emissive Edge", 760, 720);
            Connect(graph, AddOp(graph, bodyColor, edgeColor, "Body + Edge", 1000, 570), baseColorBlock.GetSlotReference(0));
            Connect(graph, Multiply(graph, Out(baseSplit, 3), visible, "Dissolve Alpha", 1000, 760), alphaBlock.GetSlotReference(0));
            return true;
        }

        static bool BuildOutline(GraphData graph, SlotReference baseRgba, BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            var uv = Add<UVNode>(graph, "UV0", -1200, 100);
            var uvSplit = Add<SplitNode>(graph, "UV XY", -970, 100);
            Connect(graph, Out(uv, 0), In(uvSplit, 0));
            var effect = GlobalFloat(graph, "Effect", "_SC_Effect", 0f, -1200, 280);
            var texelSize = GlobalVector4(graph, "Texel Size", "_SC_TexelSize", new Vector4(1f / 256f, 1f / 256f, 256f, 256f), -1200, 440);
            var texelSplit = Add<SplitNode>(graph, "Texel XY", -970, 440);
            Connect(graph, texelSize, In(texelSplit, 0));
            var thickness = Lerp(graph, Constant(graph, 1f, -970, 600), Constant(graph, 4f, -970, 680), effect, "Outline Thickness", -740, 620);
            var dx = Multiply(graph, Out(texelSplit, 0), thickness, "Texel X", -510, 500);
            var dy = Multiply(graph, Out(texelSplit, 1), thickness, "Texel Y", -510, 620);
            SlotReference SampleAlpha(SlotReference xOffset, SlotReference yOffset, string label, float y)
            {
                var shifted = Vector2(graph, AddOp(graph, Out(uvSplit, 0), xOffset, label + " X", -280, y), AddOp(graph, Out(uvSplit, 1), yOffset, label + " Y", -280, y + 60), label + " UV", -50, y + 20);
                var sample = SampleTexture(graph, label, "_MainTex", true, shifted, 180, y + 20);
                return sample.GetSlotReference(SampleTexture2DNode.OutputSlotAId);
            }
            var zero = Constant(graph, 0f, -510, 760);
            var negDx = Subtract(graph, zero, dx, "-Texel X", -280, 760);
            var negDy = Subtract(graph, zero, dy, "-Texel Y", -280, 860);
            var left = SampleAlpha(negDx, zero, "Left Alpha", 100);
            var right = SampleAlpha(dx, zero, "Right Alpha", 250);
            var up = SampleAlpha(zero, dy, "Up Alpha", 400);
            var down = SampleAlpha(zero, negDy, "Down Alpha", 550);
            var upLeft = SampleAlpha(negDx, dy, "Up Left Alpha", 700);
            var upRight = SampleAlpha(dx, dy, "Up Right Alpha", 850);
            var downLeft = SampleAlpha(negDx, negDy, "Down Left Alpha", 1000);
            var downRight = SampleAlpha(dx, negDy, "Down Right Alpha", 1150);
            var max4 = Maximum(graph, Maximum(graph, left, right, "Max Left Right", 430, 220), Maximum(graph, up, down, "Max Up Down", 430, 500), "4-Way Max", 660, 340);
            var min4 = Minimum(graph, Minimum(graph, left, right, "Min Left Right", 430, 320), Minimum(graph, up, down, "Min Up Down", 430, 600), "4-Way Min", 660, 500);
            var maxDiag = Maximum(graph, Maximum(graph, upLeft, upRight, "Max Upper Diagonal", 430, 820), Maximum(graph, downLeft, downRight, "Max Lower Diagonal", 430, 1120), "Diagonal Max", 660, 930);
            var minDiag = Minimum(graph, Minimum(graph, upLeft, upRight, "Min Upper Diagonal", 430, 900), Minimum(graph, downLeft, downRight, "Min Lower Diagonal", 430, 1200), "Diagonal Min", 660, 1040);
            var directions = PerRendererFloat(graph, "Outline Directions (4 / 8)", "_SC_OutlineDirections", 8f, 660, 1250);
            var useDiagonal = Step(graph, Constant(graph, 6f, 890, 1260), directions, "Use 8 Directions", 1120, 1200);
            var neighborMax = Lerp(graph, max4, Maximum(graph, max4, maxDiag, "8-Way Max", 890, 760), useDiagonal, "4 / 8 Max", 1350, 680);
            var neighborMin = Lerp(graph, min4, Minimum(graph, min4, minDiag, "8-Way Min", 890, 960), useDiagonal, "4 / 8 Min", 1350, 860);
            var baseAlpha = Alpha(graph, baseRgba, 1120, 1040);
            var outline = Saturate(graph, Subtract(graph, neighborMax, baseAlpha, "Outer Difference", 1580, 640), "Outer Outline", 1810, 640);
            var inner = Multiply(graph, baseAlpha, OneMinus(graph, neighborMin, "Inner Neighbor", 1580, 920), "Inner Rim", 1810, 900);
            var colorA = GlobalVector4(graph, "Outline Color", "_SC_ColorA", new Vector4(.12f, .88f, 1f, 1f), 1580, 1080);
            var colorB = GlobalVector4(graph, "Inner Rim Color", "_SC_ColorB", new Vector4(1f, .18f, .42f, 1f), 1580, 1220);
            var outerColor = Multiply(graph, colorA, Multiply(graph, outline, effect, "Outer Amount", 2040, 650), "Outer Color", 2270, 680);
            var baseOutline = AddOp(graph, baseRgba, outerColor, "Sprite + Outline", 2500, 720);
            var innerColor = Multiply(graph, colorB, Multiply(graph, inner, effect, "Inner Amount", 2040, 980), "Inner Emission", 2270, 940);
            Connect(graph, AddOp(graph, baseOutline, innerColor, "Outline Result", 2730, 760), baseColorBlock.GetSlotReference(0));
            Connect(graph, Maximum(graph, baseAlpha, Multiply(graph, outline, effect, "Outline Alpha", 2040, 580), "Final Alpha", 2270, 580), alphaBlock.GetSlotReference(0));
            return true;
        }

        static bool BuildWindFragment(GraphData graph, SlotReference baseRgba, BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            var effect = GlobalFloat(graph, "Effect", "_SC_Effect", 0f, -600, 200);
            var time = GlobalFloat(graph, "Deterministic Demo Time", "_SC_DemoTime", 0f, -600, 40);
            var uvWiggleAmount = PerRendererFloat(graph, "UV Wiggle Amount", "_SC_UVWiggleAmount", 1f, -600, 560);
            var uv = Add<UVNode>(graph, "Wind Fragment UV", -850, -120);
            var uvSplit = Add<SplitNode>(graph, "Wind Fragment UV XY", -600, -120);
            Connect(graph, Out(uv, 0), In(uvSplit, 0));
            var uvPhase = AddOp(graph, Multiply(graph, Out(uvSplit, 1), Constant(graph, 7f, -350, -140), "UV.y x7", -120, -140), Multiply(graph, time, Constant(graph, 2.4f, -350, -20), "Time x2.4", -120, -20), "UV Wiggle Phase", 110, -80);
            var uvOffset = Multiply(graph, Sine(graph, uvPhase, "UV Wiggle", 340, -80), Multiply(graph, Multiply(graph, effect, uvWiggleAmount, "Effect x UV-only Control", 110, 60), Constant(graph, .014f, 340, 120), "UV Wiggle Scale", 570, 80), "Fragment UV Offset", 800, -40);
            var windUv = Vector2(graph, AddOp(graph, Out(uvSplit, 0), uvOffset, "Wiggled UV.x", 1030, -80), Out(uvSplit, 1), "Wiggled Sprite UV", 1260, -40);
            var windSample = SampleTexture(graph, "UV Wiggled Sprite", "_MainTex", true, windUv, 1490, -40);
            var fragmentSource = Lerp(graph, baseRgba, Out(windSample, 0), Multiply(graph, effect, uvWiggleAmount, "UV-only Blend", 1490, 140), "Effect 0 UV Identity", 1720, 20);
            var tint = Vector3(graph, Constant(graph, .72f, -600, 360), Constant(graph, 1.12f, -600, 440), Constant(graph, .88f, -600, 520), "Wind Tint", -350, 410);
            var tinted = Multiply(graph, fragmentSource, tint, "Tinted Sprite", -100, 300);
            var amount = Multiply(graph, effect, Constant(graph, .65f, -100, 470), "Tint Amount", 140, 430);
            Connect(graph, Lerp(graph, fragmentSource, tinted, amount, "Wind Fragment + UV", 400, 300), baseColorBlock.GetSlotReference(0));
            Connect(graph, Alpha(graph, fragmentSource, 400, 520), alphaBlock.GetSlotReference(0));
            return true;
        }

        static bool BuildPixelDither(GraphData graph, SlotReference baseRgba, BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            var uv = Add<UVNode>(graph, "Local UV", -1100, 80);
            var uvSplit = Add<SplitNode>(graph, "UV XY", -870, 80);
            Connect(graph, Out(uv, 0), In(uvSplit, 0));
            var effect = GlobalFloat(graph, "Effect", "_SC_Effect", 0f, -1100, 260);
            var texelSize = GlobalVector4(graph, "Sprite Texel Size", "_SC_TexelSize", new Vector4(1f / 256f, 1f / 256f, 256f, 256f), -1100, 420);
            var texelSplit = Add<SplitNode>(graph, "Texture Width / Height", -870, 420);
            Connect(graph, texelSize, In(texelSplit, 0));
            var textureSize = Vector2(graph, Out(texelSplit, 2), Out(texelSplit, 3), "Texture Size", -620, 420);
            var blockPixels = Lerp(graph, Constant(graph, 1f, -620, 560), Constant(graph, 14f, -620, 640), effect, "Pixel Block Size", -380, 580);
            var pixel = Multiply(graph, Out(uv, 0), textureSize, "UV to Pixel", -380, 80);
            var cell = Floor(graph, Divide(graph, pixel, blockPixels, "Pixel / Block", -130, 80), "Pixel Cell", 100, 80);
            var samplePixel = AddOp(graph, Multiply(graph, cell, blockPixels, "Cell Origin", 330, 80), Multiply(graph, blockPixels, Constant(graph, .5f, 100, 230), "Cell Center", 330, 210), "Centered Pixel", 560, 100);
            var sampleUv = Divide(graph, samplePixel, textureSize, "Pixelated UV", 790, 100);
            var sampled = SampleTexture(graph, "Pixelated Sprite", "_MainTex", true, sampleUv, 1010, 80);
            var sampledSplit = Add<SplitNode>(graph, "Pixel Sample RGBA", 1240, 80);
            Connect(graph, Out(sampled, 0), In(sampledSplit, 0));

            var levels = Maximum(graph, Constant(graph, 2f, 100, 380), Floor(graph, Lerp(graph, Constant(graph, 12f, -380, 340), Constant(graph, 4f, -380, 420), effect, "Posterize Levels", -130, 360), "Integer Levels", 100, 340), "Safe Levels", 330, 350);
            var levelsMinusOne = Subtract(graph, levels, Constant(graph, 1f, 330, 470), "Levels - 1", 560, 410);
            var posterized = Divide(graph, Round(graph, Multiply(graph, Out(sampled, 0), levelsMinusOne, "Scale Colors", 790, 330), "Round Colors", 1010, 330), levelsMinusOne, "Posterized", 1240, 330);

            var localPixel = Floor(graph, pixel, "Local Pixel Coordinates", -130, 580);
            var screenPosition = Add<ScreenPositionNode>(graph, "Screen Position", -620, 760);
            var screen = Add<ScreenNode>(graph, "Screen Size", -620, 900);
            var screenSize = Vector2(graph, Out(screen, 0), Out(screen, 1), "Screen Width / Height", -380, 900);
            var screenPixel = Floor(graph, Multiply(graph, Out(screenPosition, 0), screenSize, "Screen Pixel", -130, 780), "Fixed Screen Coordinates", 100, 760);
            var ditherSpace = PerRendererFloat(graph, "Dither Space (0 Local / 1 Screen)", "_SC_DitherSpace", 0f, 100, 940);
            var ditherCoord = Lerp(graph, localPixel, screenPixel, ditherSpace, "Local / Screen Coordinates", 330, 700);
            var dither = Fraction(graph, Dot(graph, ditherCoord, Vector2(graph, Constant(graph, .75487766f, 330, 900), Constant(graph, .56984029f, 330, 980), "Dither Hash", 560, 930), "Dither Dot", 790, 760), "Dither Pattern", 1010, 720);
            var ditherOffset = Divide(graph, Subtract(graph, dither, Constant(graph, .5f, 1010, 860), "Centered Dither", 1240, 760), levels, "Dither / Levels", 1470, 700);
            var processed = Saturate(graph, AddOp(graph, posterized, ditherOffset, "Posterize + Dither", 1470, 430), "Clamp Output", 1700, 430);
            Connect(graph, Lerp(graph, baseRgba, processed, effect, "Effect 0 Identity", 1940, 300), baseColorBlock.GetSlotReference(0));
            Connect(graph, Out(sampledSplit, 3), alphaBlock.GetSlotReference(0));
            return true;
        }

        static bool BuildHologram(GraphData graph, SlotReference baseRgba, BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            var uv = Add<UVNode>(graph, "UV0", -1000, 100);
            var uvSplit = Add<SplitNode>(graph, "UV XY", -760, 100);
            Connect(graph, Out(uv, 0), In(uvSplit, 0));
            var effect = GlobalFloat(graph, "Effect", "_SC_Effect", 0f, -1000, 280);
            var time = GlobalFloat(graph, "Deterministic Demo Time", "_SC_DemoTime", 0f, -1000, 430);
            var seed = GlobalFloat(graph, "Scene Seed", "_SC_Seed", .173f, -1000, 580);
            var texel = GlobalVector4(graph, "Texel Size", "_SC_TexelSize", new Vector4(1f / 256f, 1f / 256f, 256f, 256f), -1000, 730);
            var texelSplit = Add<SplitNode>(graph, "Texel XY", -760, 730);
            Connect(graph, texel, In(texelSplit, 0));
            var glitchPhase = Fraction(graph, AddOp(graph, Multiply(graph, time, Constant(graph, .8f, -760, 500), "Slow Time", -520, 480), seed, "Seeded Glitch", -280, 480), "Low Frequency Phase", -40, 480);
            var glitchActive = Step(graph, Constant(graph, .86f, -280, 620), glitchPhase, "Rare Glitch", 190, 500);
            var glitchOffset = Multiply(graph, Multiply(graph, Sine(graph, Multiply(graph, glitchPhase, Constant(graph, 6.2831853f, -40, 650), "Phase x Tau", 190, 650), "Glitch Sign", 420, 620), Out(texelSplit, 0), "Texel Offset", 650, 600), Multiply(graph, glitchActive, Multiply(graph, effect, Constant(graph, 12f, 420, 800), "Effect Offset", 650, 780), "Active Offset", 880, 690), "Glitch UV Offset", 1110, 620);
            var sampleUv = Vector2(graph, AddOp(graph, Out(uvSplit, 0), glitchOffset, "Offset UV.x", 1340, 520), Out(uvSplit, 1), "Hologram UV", 1570, 540);
            var sampled = SampleTexture(graph, "Hologram Sprite", "_MainTex", true, sampleUv, 1800, 520);
            var sampledSplit = Add<SplitNode>(graph, "Hologram RGBA", 2030, 520);
            Connect(graph, Out(sampled, 0), In(sampledSplit, 0));
            var linePhase = Multiply(graph, AddOp(graph, Multiply(graph, Out(uvSplit, 1), Constant(graph, 22f, -520, 980), "UV.y x22", -280, 960), Multiply(graph, time, Constant(graph, 1.8f, -520, 1100), "Time x1.8", -280, 1080), "Scan Phase", -40, 1010), Constant(graph, 6.2831853f, -40, 1150), "Scan Tau", 190, 1040);
            var scanLine = AddOp(graph, Constant(graph, .5f, 190, 1180), Multiply(graph, Constant(graph, .5f, 190, 1260), Sine(graph, linePhase, "Scan Sine", 420, 1040), "Half Sine", 650, 1080), "Scan Lines", 880, 1080);
            var tintAmount = Multiply(graph, effect, Constant(graph, .52f, 1110, 1140), "Tint Amount", 1340, 1120);
            var colorA = GlobalVector4(graph, "Hologram Color", "_SC_ColorA", new Vector4(.12f, .88f, 1f, 1f), 1110, 1260);
            var tinted = Multiply(graph, Out(sampled, 0), Lerp(graph, ConstantVector3(graph, UnityEngine.Vector3.one, 1340, 1320), colorA, tintAmount, "Effect Gated Tint", 1570, 1240), "Tinted Hologram", 1800, 1120);
            var emission = Multiply(graph, Multiply(graph, colorA, scanLine, "Line Color", 1110, 1400), Multiply(graph, effect, Out(sampledSplit, 3), "Visible Effect", 1340, 1450), "Scan Emission", 1570, 1400);
            var diagonal = Dot(graph, Out(uv, 0), ConstantVector2(graph, new Vector2(.7071068f, .7071068f), 880, 1540), "Diagonal UV Projection", 1110, 1540);
            var movingShine = Fraction(graph, AddOp(graph, diagonal, Multiply(graph, time, Constant(graph, .38f, 880, 1720), "Shine Speed", 1110, 1700), "Moving Diagonal Coordinate", 1340, 1580), "Looping Shine", 1570, 1540);
            var shineIn = Smoothstep(graph, movingShine, .28f, .40f, "Shine Soft Leading Edge", 1800, 1460);
            var shineOut = OneMinus(graph, Smoothstep(graph, movingShine, .58f, .70f, "Shine Soft Trailing Edge", 1800, 1660), "Invert Shine Trail", 2030, 1640);
            var shineBand = Multiply(graph, shineIn, shineOut, "Diagonal Shine Band", 2260, 1540);
            var shineVisible = Multiply(graph, Multiply(graph, shineBand, Out(sampledSplit, 3), "Shine Alpha Mask", 2490, 1500), effect, "Effect Gated Shine", 2720, 1500);
            var shineEmission = Multiply(graph, GlobalVector4(graph, "Shine Color", "_SC_ColorB", new Vector4(1f, .18f, .42f, 1f), 2260, 1760), Multiply(graph, shineVisible, Constant(graph, 3.2f, 2490, 1840), "Shine HDR", 2720, 1780), "Diagonal Shine Emission", 2950, 1640);
            var hologramAndScan = AddOp(graph, tinted, emission, "Hologram + Horizontal Scan", 2030, 1180);
            Connect(graph, AddOp(graph, hologramAndScan, shineEmission, "Hologram + Diagonal Shine", 3180, 1380), baseColorBlock.GetSlotReference(0));
            var lineAlpha = Lerp(graph, Constant(graph, .38f, 1570, 1560), Constant(graph, 1f, 1570, 1640), scanLine, "Scan Alpha", 1800, 1580);
            Connect(graph, Multiply(graph, Out(sampledSplit, 3), Lerp(graph, Constant(graph, 1f, 1800, 1720), lineAlpha, effect, "Effect 0 Alpha Identity", 2030, 1620), "Hologram Alpha", 2260, 1560), alphaBlock.GetSlotReference(0));
            return true;
        }

        static bool BuildMaskLighting(GraphData graph, SlotReference baseRgba, BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            var uv = Add<UVNode>(graph, "UV0", -900, 100);
            var mask = SampleTexture(graph, "Secondary Mask Texture", "_MaskTex", true, Out(uv, 0), -650, 100, perRenderer: true);
            var maskSplit = Add<SplitNode>(graph, "Mask R / G / B", -400, 100);
            Connect(graph, Out(mask, 0), In(maskSplit, 0));
            var effect = GlobalFloat(graph, "Effect", "_SC_Effect", 0f, -900, 300);
            var time = GlobalFloat(graph, "Deterministic Demo Time", "_SC_DemoTime", 0f, -900, 440);
            var channel = PerRendererFloat(graph, "Mask Channel (0 R / 1 G / 2 B)", "_SC_MaskChannel", 0f, -900, 580);
            var selectR = OneMinus(graph, Saturate(graph, Absolute(graph, channel, "Distance from R", -650, 580), "R Selector Distance", -400, 560), "Select R", -150, 540);
            var selectG = OneMinus(graph, Saturate(graph, Absolute(graph, Subtract(graph, channel, Constant(graph, 1f, -650, 760), "Distance from G", -400, 720), "Abs G", -150, 700), "G Selector Distance", 80, 680), "Select G", 310, 660);
            var selectB = OneMinus(graph, Saturate(graph, Absolute(graph, Subtract(graph, channel, Constant(graph, 2f, -650, 920), "Distance from B", -400, 880), "Abs B", -150, 860), "B Selector Distance", 80, 840), "Select B", 310, 820);
            var wet = Multiply(graph, Multiply(graph, Out(maskSplit, 0), selectR, "Wetness Mask R", 80, 100), ConstantVector3(graph, new Vector3(.08f, .58f, .72f), 80, 220), "Static Wet Sheen", 310, 140);
            var metal = Multiply(graph, Multiply(graph, Out(maskSplit, 1), selectG, "Metal Mask G (URP Sprite Mask only)", 310, 300), Constant(graph, 0f, 540, 380), "No Manual G BaseColor Light", 770, 340);
            var pulse = AddOp(graph, Constant(graph, .5f, 540, 820), Multiply(graph, Constant(graph, .5f, 540, 900), Sine(graph, Multiply(graph, time, Constant(graph, 6f, 310, 980), "Time x6", 540, 980), "Blue Pulse", 770, 900), "Half Pulse", 1000, 880), "Pulse 0..1", 1230, 860);
            var weak = Multiply(graph, Multiply(graph, Multiply(graph, Out(maskSplit, 2), selectB, "Weak Point Mask B", 540, 520), pulse, "Weak Point Pulse", 770, 540), ConstantVector3(graph, new Vector3(1.15f, .18f, .48f), 770, 660), "Weak Point Emission", 1000, 580);
            var channels = AddOp(graph, AddOp(graph, wet, metal, "Wet + Zero Manual Metal", 770, 200), weak, "Wet + URP Metal + Weak", 1230, 300);
            var lighting = Multiply(graph, channels, Multiply(graph, effect, Constant(graph, 1.6f, 1000, 760), "Effect Intensity", 1230, 720), "Masked Lighting", 1460, 380);
            Connect(graph, AddOp(graph, baseRgba, lighting, "Base + Mask Channels", 1690, 300), baseColorBlock.GetSlotReference(0));
            Connect(graph, Alpha(graph, baseRgba, 1460, 580), alphaBlock.GetSlotReference(0));
            return true;
        }

        static bool BuildWaterReflection(GraphData graph, SlotReference baseRgba, BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            var uv = Add<UVNode>(graph, "Reflection UV", -900, 100);
            var uvSplit = Add<SplitNode>(graph, "UV XY", -650, 100);
            Connect(graph, Out(uv, 0), In(uvSplit, 0));
            var effect = GlobalFloat(graph, "Effect", "_SC_Effect", 0f, -900, 300);
            var time = GlobalFloat(graph, "Deterministic Demo Time", "_SC_DemoTime", 0f, -900, 440);
            var texel = GlobalVector4(graph, "Texel Size", "_SC_TexelSize", new Vector4(1f / 256f, 1f / 256f, 256f, 256f), -900, 580);
            var texelSplit = Add<SplitNode>(graph, "Texel XY", -650, 580);
            Connect(graph, texel, In(texelSplit, 0));
            var wavePhase = Multiply(graph, AddOp(graph, Multiply(graph, Out(uvSplit, 1), Constant(graph, 9f, -650, 720), "UV.y x9", -420, 700), Multiply(graph, time, Constant(graph, 1.8f, -650, 840), "Time x1.8", -420, 820), "Wave Phase", -190, 760), Constant(graph, 6.2831853f, -190, 900), "Wave Tau", 40, 800);
            var wave = Multiply(graph, Multiply(graph, Sine(graph, wavePhase, "Water Sine", 270, 800), Out(texelSplit, 0), "Texel Wave", 500, 760), Multiply(graph, effect, Constant(graph, 7f, 270, 940), "Effect Wave", 500, 920), "Horizontal Wave", 730, 820);
            var sampleUv = Vector2(graph, AddOp(graph, Out(uvSplit, 0), wave, "Wave UV.x", 960, 720), Out(uvSplit, 1), "Reflection Sample UV", 1190, 740);
            var reflection = SampleTexture(graph, "Reflection Only", "_MainTex", true, sampleUv, 1420, 720);
            var reflectionSplit = Add<SplitNode>(graph, "Reflection RGBA", 1650, 720);
            Connect(graph, Out(reflection, 0), In(reflectionSplit, 0));
            var tint = Lerp(graph, ConstantVector3(graph, UnityEngine.Vector3.one, 960, 1000), ConstantVector3(graph, new UnityEngine.Vector3(.38f, .72f, .95f), 960, 1120), effect, "Water Tint", 1190, 1030);
            Connect(graph, Multiply(graph, Out(reflection, 0), tint, "Tinted Reflection", 1880, 820), baseColorBlock.GetSlotReference(0));
            var fade = Smoothstep(graph, OneMinus(graph, Out(uvSplit, 1), "Depth", 730, 1120), 0f, .82f, "Depth Fade", 960, 1180);
            var effectFade = Lerp(graph, Constant(graph, 1f, 1190, 1280), fade, effect, "Effect 0 Identity Fade", 1420, 1200);
            var alphaScale = Lerp(graph, Constant(graph, 1f, 1420, 1370), Constant(graph, .72f, 1420, 1450), effect, "Reflection Alpha", 1650, 1380);
            Connect(graph, Multiply(graph, Out(reflectionSplit, 3), Multiply(graph, effectFade, alphaScale, "Fade x Alpha", 1880, 1260), "Final Reflection Alpha", 2110, 1180), alphaBlock.GetSlotReference(0));
            return true;
        }

        static bool BuildWorldScan(GraphData graph, SlotReference baseRgba, BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            var world = Add<PositionNode>(graph, "World Position", -900, 100);
            SetCoordinateSpace(world, CoordinateSpace.World);
            var center = GlobalVector4(graph, "Global Scan Center", "_SC_ScanCenter", Vector4.zero, -900, 280);
            var radius = GlobalFloat(graph, "Global Scan Radius", "_SC_ScanRadius", .2f, -900, 440);
            var effect = GlobalFloat(graph, "Effect", "_SC_Effect", 0f, -900, 580);
            var distance = Distance(graph, Out(world, 0), center, "World Distance", -650, 200);
            var ringDistance = Absolute(graph, Subtract(graph, distance, radius, "Distance - Radius", -420, 200), "Ring Distance", -190, 200);
            var ring = OneMinus(graph, Smoothstep(graph, ringDistance, .08f, .24f, "Ring Width", 40, 220), "Scan Ring", 270, 220);
            var inside = OneMinus(graph, Smoothstep(graph, distance, Subtract(graph, radius, Constant(graph, .35f, -420, 500), "Radius - Feather", -190, 480), radius, "Reveal Feather", 40, 440), "Inside Reveal", 270, 440);
            var baseSplit = Add<SplitNode>(graph, "Base RGBA", 40, 650);
            Connect(graph, baseRgba, In(baseSplit, 0));
            var scanColor = GlobalVector4(graph, "Scan Color", "_SC_ColorA", new Vector4(.12f, .88f, 1f, 1f), 270, 650);
            var emission = Multiply(graph, Multiply(graph, scanColor, ring, "Ring Color", 500, 300), Multiply(graph, Out(baseSplit, 3), Constant(graph, 2.2f, 500, 650), "Visible HDR", 730, 580), "Scan Emission", 960, 380);
            Connect(graph, AddOp(graph, baseRgba, emission, "Base + Global Scan", 1190, 300), baseColorBlock.GetSlotReference(0));
            var revealAmount = Multiply(graph, effect, Constant(graph, .72f, 730, 760), "Reveal Amount", 960, 720);
            Connect(graph, Multiply(graph, Out(baseSplit, 3), Lerp(graph, Constant(graph, 1f, 960, 840), inside, revealAmount, "Reveal Alpha", 1190, 700), "Final Scan Alpha", 1420, 620), alphaBlock.GetSlotReference(0));
            return true;
        }

        static bool BuildHitFlash(GraphData graph, SlotReference baseRgba, BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            var split = Add<SplitNode>(graph, "Base RGBA", -900, 0);
            Connect(graph, baseRgba, In(split, 0));
            var effect = GlobalFloat(graph, "Effect", "_SC_Effect", 0f, -900, -260);
            var demoTime = GlobalFloat(graph, "Deterministic Demo Time", "_SC_DemoTime", 0f, -900, -420);
            var seed = GlobalFloat(graph, "Scene Seed", "_SC_Seed", .173f, -900, -560);
            var seedOffset = PerRendererFloat(graph, "Per Renderer Seed Offset", "_SC_SeedOffset", 0f, -900, -700);
            var flashColor = GlobalVector4(graph, "HDR Flash Color", "_SC_ColorB", new Vector4(1f, .18f, .42f, 1f), -900, -840);

            var flashIn = Smoothstep(graph, effect, .02f, .18f, "Flash In", -550, -250);
            var flashOut = Smoothstep(graph, effect, .36f, .58f, "Flash Out", -550, -130);
            var flashMask = Multiply(graph, flashIn, OneMinus(graph, flashOut, "Invert Flash Out", -320, -130), "Flash Window", -90, -220);

            var hdrTint = Multiply(graph, flashColor, Constant(graph, 3.5f, -540, 30), "HDR Tint x3.5", -300, 20);
            var hdrBase = AddOp(graph, baseRgba, hdrTint, "Add HDR Tint", -70, 20);
            var hdrFlash = AddOp(graph, hdrBase, Constant(graph, 1.5f, -300, 135), "Add White HDR", 160, 50);
            var color = Lerp(graph, baseRgba, hdrFlash, flashMask, "HDR Hit Flash", 410, -80);

            var phase = AddOp(graph,
                AddOp(graph, Multiply(graph, demoTime, Constant(graph, 7f, -540, -430), "Time x7", -300, -430), seed, "Time + Seed", -60, -430),
                seedOffset, "Per Renderer Phase", 170, -430);
            var blink = Step(graph, Constant(graph, .5f, -60, -560), Fraction(graph, phase, "Loop Phase", 390, -430), "Invincible Blink", 620, -430);
            var invincible = Smoothstep(graph, effect, .55f, .72f, "Invincible Amount", 160, -260);
            var alphaFactor = Lerp(graph, Constant(graph, 1f, 400, -280), blink, invincible, "Blink Enable", 650, -260);
            var alpha = Multiply(graph, Out(split, 3), alphaFactor, "Final Alpha", 880, -260);

            Connect(graph, color, baseColorBlock.GetSlotReference(0));
            Connect(graph, alpha, alphaBlock.GetSlotReference(0));
            return true;
        }

        static bool BuildNormalMapLighting(GraphData graph, SlotReference baseRgba, BlockNode baseColorBlock, BlockNode alphaBlock)
        {
            var split = Add<SplitNode>(graph, "Sprite RGBA", -420, 100);
            Connect(graph, baseRgba, In(split, 0));
            var uv = Add<UVNode>(graph, "Normal UV", -900, 260);
            var normalSample = SampleTexture(graph, "Secondary Normal Map", "_NormalMap", true, Out(uv, 0), -650, 260, true);
            normalSample.textureType = TextureType.Normal;
            normalSample.UpdateNodeAfterDeserialization();
            var time = GlobalFloat(graph, "Deterministic Demo Time", "_SC_DemoTime", 0f, -900, 480);
            var effect = GlobalFloat(graph, "Effect", "_SC_Effect", 0f, -900, 620);
            var phase = Multiply(graph, time, Constant(graph, 1.35f, -650, 500), "Light Orbit Phase", -420, 480);
            var lightDirection = Vector3(graph,
                Cosine(graph, phase, "Light X", -190, 420),
                Sine(graph, phase, "Light Y", -190, 520),
                Constant(graph, .55f, -190, 620),
                "Moving Light Direction", 40, 500);
            var normalizedLight = Normalize(graph, lightDirection, "Normalize Light", 270, 500);
            var normalDotLight = Saturate(graph, Dot(graph, Out(normalSample, 0), normalizedLight, "Normal Dot Moving Light", 40, 260), "Visible Normal Response", 270, 260);
            var mappedLight = AddOp(graph, Constant(graph, .18f, 270, 120), Multiply(graph, normalDotLight, Constant(graph, 1.35f, 500, 380), "Normal Contrast", 730, 300), "Normal Lighting", 960, 260);
            var lighting = Lerp(graph, Constant(graph, 1f, 730, 500), mappedLight, effect, "Effect 0 Identity / Normal Lighting", 1190, 360);
            Connect(graph, Multiply(graph, baseRgba, lighting, "Lit Sprite Color", 1420, 260), baseColorBlock.GetSlotReference(0));
            Connect(graph, Out(split, 3), alphaBlock.GetSlotReference(0));
            return true;
        }

        static T Add<T>(GraphData graph, string name, float x, float y) where T : AbstractMaterialNode, new()
        {
            var node = new T { name = name };
            var drawState = node.drawState;
            drawState.position = new Rect(x, y, 210f, 120f);
            node.drawState = drawState;
            graph.AddNode(node);
            return node;
        }

        static SlotReference Constant(GraphData graph, float value, float x, float y)
        {
            var node = Add<Vector1Node>(graph, value.ToString("0.###"), x, y);
            node.FindInputSlot<Vector1MaterialSlot>(Vector1Node.InputSlotXId).value = value;
            return node.GetSlotReference(Vector1Node.OutputSlotId);
        }

        static SlotReference GlobalFloat(GraphData graph, string displayName, string referenceName, float value, float x, float y)
            => FloatProperty(graph, displayName, referenceName, value, false, x, y);

        static SlotReference PerRendererFloat(GraphData graph, string displayName, string referenceName, float value, float x, float y)
            => FloatProperty(graph, displayName, referenceName, value, true, x, y);

        static SlotReference FloatProperty(GraphData graph, string displayName, string referenceName, float value, bool perRenderer, float x, float y)
        {
            var property = graph.properties.OfType<Vector1ShaderProperty>().FirstOrDefault(item => item.referenceName == referenceName);
            if (property == null)
            {
                property = new Vector1ShaderProperty
                {
                    displayName = displayName,
                    value = value,
                    generatePropertyBlock = perRenderer,
                    overrideReferenceName = referenceName
                };
                property.overrideHLSLDeclaration = true;
                property.hlslDeclarationOverride = perRenderer ? HLSLDeclaration.UnityPerMaterial : HLSLDeclaration.Global;
                property.PerRendererData = perRenderer;
                graph.AddGraphInput(property);
            }
            var node = Add<PropertyNode>(graph, displayName, x, y);
            node.property = property;
            node.UpdateNodeAfterDeserialization();
            return Out(node, 0);
        }

        static SlotReference GlobalVector4(GraphData graph, string displayName, string referenceName, Vector4 value, float x, float y)
        {
            var property = graph.properties.OfType<Vector4ShaderProperty>().FirstOrDefault(item => item.referenceName == referenceName);
            if (property == null)
            {
                property = new Vector4ShaderProperty
                {
                    displayName = displayName,
                    value = value,
                    generatePropertyBlock = false,
                    overrideReferenceName = referenceName
                };
                property.overrideHLSLDeclaration = true;
                property.hlslDeclarationOverride = HLSLDeclaration.Global;
                graph.AddGraphInput(property);
            }
            var node = Add<PropertyNode>(graph, displayName, x, y);
            node.property = property;
            node.UpdateNodeAfterDeserialization();
            return Out(node, 0);
        }

        static SampleTexture2DNode SampleTexture(GraphData graph, string displayName, string referenceName, bool materialProperty,
            SlotReference uv, float x, float y, bool perRenderer = false)
        {
            var property = graph.properties.OfType<Texture2DShaderProperty>().FirstOrDefault(item => item.referenceName == referenceName);
            if (property == null)
            {
                property = new Texture2DShaderProperty
                {
                    displayName = displayName,
                    overrideReferenceName = referenceName,
                    generatePropertyBlock = materialProperty || perRenderer,
                    useTilingAndOffset = false,
                    useTexelSize = false
                };
                property.overrideHLSLDeclaration = true;
                property.hlslDeclarationOverride = materialProperty || perRenderer ? HLSLDeclaration.UnityPerMaterial : HLSLDeclaration.Global;
                property.PerRendererData = perRenderer;
                graph.AddGraphInput(property);
            }
            var propertyNode = Add<PropertyNode>(graph, displayName + " Property", x - 240, y - 40);
            propertyNode.property = property;
            propertyNode.UpdateNodeAfterDeserialization();
            var sample = Add<SampleTexture2DNode>(graph, displayName, x, y);
            Connect(graph, Out(propertyNode, 0), sample.GetSlotReference(SampleTexture2DNode.TextureInputId));
            Connect(graph, uv, sample.GetSlotReference(SampleTexture2DNode.UVInput));
            return sample;
        }

        static SlotReference Alpha(GraphData graph, SlotReference rgba, float x, float y)
        {
            var split = Add<SplitNode>(graph, "Alpha", x, y);
            Connect(graph, rgba, In(split, 0));
            return Out(split, 3);
        }

        static SlotReference Vector2(GraphData graph, SlotReference xValue, SlotReference yValue, string name, float x, float y)
        {
            var node = Add<Vector2Node>(graph, name, x, y);
            Connect(graph, xValue, node.GetSlotReference(Vector2Node.InputSlotXId));
            Connect(graph, yValue, node.GetSlotReference(Vector2Node.InputSlotYId));
            return node.GetSlotReference(Vector2Node.OutputSlotId);
        }

        static SlotReference Vector3(GraphData graph, SlotReference xValue, SlotReference yValue, SlotReference zValue, string name, float x, float y)
        {
            var node = Add<Vector3Node>(graph, name, x, y);
            Connect(graph, xValue, node.GetSlotReference(Vector3Node.InputSlotXId));
            Connect(graph, yValue, node.GetSlotReference(Vector3Node.InputSlotYId));
            Connect(graph, zValue, node.GetSlotReference(Vector3Node.InputSlotZId));
            return node.GetSlotReference(Vector3Node.OutputSlotId);
        }

        static SlotReference ConstantVector3(GraphData graph, Vector3 value, float x, float y)
        {
            var node = Add<Vector3Node>(graph, "Vector " + value, x, y);
            node.FindInputSlot<Vector1MaterialSlot>(Vector3Node.InputSlotXId).value = value.x;
            node.FindInputSlot<Vector1MaterialSlot>(Vector3Node.InputSlotYId).value = value.y;
            node.FindInputSlot<Vector1MaterialSlot>(Vector3Node.InputSlotZId).value = value.z;
            return node.GetSlotReference(Vector3Node.OutputSlotId);
        }

        static SlotReference ConstantVector2(GraphData graph, Vector2 value, float x, float y)
        {
            var node = Add<Vector2Node>(graph, "Vector " + value, x, y);
            node.FindInputSlot<Vector1MaterialSlot>(Vector2Node.InputSlotXId).value = value.x;
            node.FindInputSlot<Vector1MaterialSlot>(Vector2Node.InputSlotYId).value = value.y;
            return node.GetSlotReference(Vector2Node.OutputSlotId);
        }

        static SlotReference AddOp(GraphData graph, SlotReference a, SlotReference b, string name, float x, float y)
            => Binary<AddNode>(graph, a, b, name, x, y);
        static SlotReference Multiply(GraphData graph, SlotReference a, SlotReference b, string name, float x, float y)
            => Binary<MultiplyNode>(graph, a, b, name, x, y);
        static SlotReference Subtract(GraphData graph, SlotReference a, SlotReference b, string name, float x, float y)
            => Binary<SubtractNode>(graph, a, b, name, x, y);
        static SlotReference Divide(GraphData graph, SlotReference a, SlotReference b, string name, float x, float y)
            => Binary<DivideNode>(graph, a, b, name, x, y);
        static SlotReference Maximum(GraphData graph, SlotReference a, SlotReference b, string name, float x, float y)
            => Binary<MaximumNode>(graph, a, b, name, x, y);
        static SlotReference Minimum(GraphData graph, SlotReference a, SlotReference b, string name, float x, float y)
            => Binary<MinimumNode>(graph, a, b, name, x, y);
        static SlotReference Distance(GraphData graph, SlotReference a, SlotReference b, string name, float x, float y)
            => Binary<DistanceNode>(graph, a, b, name, x, y);
        static SlotReference Dot(GraphData graph, SlotReference a, SlotReference b, string name, float x, float y)
            => Binary<DotProductNode>(graph, a, b, name, x, y);

        static SlotReference Binary<T>(GraphData graph, SlotReference a, SlotReference b, string name, float x, float y)
            where T : AbstractMaterialNode, new()
        {
            var node = Add<T>(graph, name, x, y);
            Connect(graph, a, In(node, 0));
            Connect(graph, b, In(node, 1));
            return Out(node, 0);
        }

        static SlotReference Smoothstep(GraphData graph, SlotReference input, float edge1, float edge2, string name, float x, float y)
        {
            var node = Add<SmoothstepNode>(graph, name, x, y);
            Connect(graph, Constant(graph, edge1, x - 220, y - 35), In(node, 0));
            Connect(graph, Constant(graph, edge2, x - 220, y + 35), In(node, 1));
            Connect(graph, input, In(node, 2));
            return Out(node, 0);
        }

        static SlotReference Smoothstep(GraphData graph, SlotReference input, SlotReference edge1, SlotReference edge2, string name, float x, float y)
        {
            var node = Add<SmoothstepNode>(graph, name, x, y);
            Connect(graph, edge1, In(node, 0));
            Connect(graph, edge2, In(node, 1));
            Connect(graph, input, In(node, 2));
            return Out(node, 0);
        }

        static SlotReference OneMinus(GraphData graph, SlotReference input, string name, float x, float y)
        {
            var node = Add<OneMinusNode>(graph, name, x, y);
            Connect(graph, input, In(node, 0));
            return Out(node, 0);
        }

        static SlotReference Fraction(GraphData graph, SlotReference input, string name, float x, float y)
        {
            var node = Add<FractionNode>(graph, name, x, y);
            Connect(graph, input, In(node, 0));
            return Out(node, 0);
        }

        static SlotReference Floor(GraphData graph, SlotReference input, string name, float x, float y)
            => Unary<FloorNode>(graph, input, name, x, y);
        static SlotReference Round(GraphData graph, SlotReference input, string name, float x, float y)
            => Unary<RoundNode>(graph, input, name, x, y);
        static SlotReference Saturate(GraphData graph, SlotReference input, string name, float x, float y)
            => Unary<SaturateNode>(graph, input, name, x, y);
        static SlotReference Absolute(GraphData graph, SlotReference input, string name, float x, float y)
            => Unary<AbsoluteNode>(graph, input, name, x, y);
        static SlotReference Sine(GraphData graph, SlotReference input, string name, float x, float y)
            => Unary<SineNode>(graph, input, name, x, y);
        static SlotReference Cosine(GraphData graph, SlotReference input, string name, float x, float y)
            => Unary<CosineNode>(graph, input, name, x, y);
        static SlotReference Normalize(GraphData graph, SlotReference input, string name, float x, float y)
            => Unary<NormalizeNode>(graph, input, name, x, y);

        static SlotReference Unary<T>(GraphData graph, SlotReference input, string name, float x, float y)
            where T : AbstractMaterialNode, new()
        {
            var node = Add<T>(graph, name, x, y);
            Connect(graph, input, In(node, 0));
            return Out(node, 0);
        }

        static SlotReference Step(GraphData graph, SlotReference edge, SlotReference input, string name, float x, float y)
        {
            var node = Add<StepNode>(graph, name, x, y);
            Connect(graph, edge, In(node, 0));
            Connect(graph, input, In(node, 1));
            return Out(node, 0);
        }

        static SlotReference Lerp(GraphData graph, SlotReference a, SlotReference b, SlotReference t, string name, float x, float y)
        {
            var node = Add<LerpNode>(graph, name, x, y);
            Connect(graph, a, In(node, 0));
            Connect(graph, b, In(node, 1));
            Connect(graph, t, In(node, 2));
            return Out(node, 0);
        }

        static SlotReference In(AbstractMaterialNode node, int index)
            => node.GetSlotReference(node.GetInputSlots<MaterialSlot>().ElementAt(index).id);
        static SlotReference Out(AbstractMaterialNode node, int index)
            => node.GetSlotReference(node.GetOutputSlots<MaterialSlot>().ElementAt(index).id);
        static void Connect(GraphData graph, SlotReference output, SlotReference input) => graph.Connect(output, input);

        static void SetCoordinateSpace(PositionNode node, CoordinateSpace coordinateSpace)
        {
            var field = typeof(GeometryNode).GetField("m_Space", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(typeof(GeometryNode).FullName, "m_Space");
            field.SetValue(node, coordinateSpace);
            node.UpdateNodeAfterDeserialization();
        }
    }
}
