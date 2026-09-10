using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShaderCapture.Tests
{
    public sealed class ShaderCaptureProjectTests
    {
        const string Root = "Assets/ShaderCapture";

        static readonly string[] SceneNames =
        {
            "00_CaptureHub", "01_HitFlashInvincible", "02_PaletteSwap", "03_Dissolve",
            "04_OutlineInnerRim", "05_WindVertexSquash", "06_GlitchRgbSplit",
            "07_PixelPosterizeDither", "08_HologramShine", "09_NormalMap2DLight",
            "10_MaskMapLighting", "11_WaterReflection", "12_WorldScanReveal"
        };

        [Test]
        public void RequiredScenesGraphsAndMaterialsExist()
        {
            Assert.That(SceneNames.All(name => File.Exists(Root + "/Scenes/" + name + ".unity")), Is.True);
            Assert.That(Directory.GetFiles(Root + "/Shaders/Graphs", "*.shadergraph").Length, Is.EqualTo(12));
            Assert.That(Directory.GetFiles(Root + "/Materials", "Effect*.mat").Length, Is.EqualTo(12));
            Assert.That(File.Exists(Root + "/Prefabs/CaptureStage.prefab"), Is.True);
        }

        [Test]
        public void AllEffectShadersImportAndAreSupported()
        {
            for (var index = 1; index <= 12; index++)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Effect" + index.ToString("00") + ".mat");
                Assert.That(material, Is.Not.Null, "Effect material " + index);
                Assert.That(material.shader, Is.Not.Null, "Effect shader " + index);
                Assert.That(material.shader.isSupported, Is.True, material.shader.name);
            }
        }

        [Test]
        public void BuildSettingsContainHubThenTwelveEffects()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            Assert.That(scenes.Length, Is.EqualTo(13));
            for (var index = 0; index < SceneNames.Length; index++)
                Assert.That(scenes[index].path, Does.EndWith(SceneNames[index] + ".unity"));
        }

        [Test]
        public void SecondaryTexturesContainNormalAndMask()
        {
            var importer = AssetImporter.GetAtPath(Root + "/Art/Generated/DemoCharacter.png") as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var secondary = provider.GetDataProvider<ISecondaryTextureDataProvider>();
            Assert.That(secondary, Is.Not.Null);
            Assert.That(secondary.textures.Select(texture => texture.name), Is.EquivalentTo(new[] { "_NormalMap", "_MaskTex" }));
            Assert.That(secondary.textures.All(texture => texture.texture != null), Is.True);
        }

        [Test]
        public void DataTextureImportSettingsAreDeterministic()
        {
            foreach (var name in new[] { "DemoCharacter_Index.png", "DemoCharacter_Mask.png", "Noise.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/Art/Generated/" + name);
                Assert.That(importer.sRGBTexture, Is.False, name);
                Assert.That(importer.mipmapEnabled, Is.False, name);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), name);
            }
            var palette = (TextureImporter)AssetImporter.GetAtPath(Root + "/Art/Generated/Palette.png");
            Assert.That(palette.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(palette.mipmapEnabled, Is.False);
            var indexMap = (TextureImporter)AssetImporter.GetAtPath(Root + "/Art/Generated/DemoCharacter_Index.png");
            Assert.That(indexMap.filterMode, Is.EqualTo(FilterMode.Point));
        }

        [Test]
        public void EveryEffectSceneHasCameraControllerAndBoundAssets()
        {
            for (var index = 1; index <= 12; index++)
            {
                var scene = EditorSceneManager.OpenScene(Root + "/Scenes/" + SceneNames[index] + ".unity", OpenSceneMode.Single);
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Count(), Is.EqualTo(1), SceneNames[index]);
                var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EffectDemoController>(true)).SingleOrDefault();
                Assert.That(controller, Is.Not.Null, SceneNames[index]);
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).Any(renderer => renderer.sharedMaterial == null), Is.False, SceneNames[index]);
            }
        }

        [Test]
        public void SpecializedScenesContainRequiredGeometryAndLights()
        {
            var meshScene = EditorSceneManager.OpenScene(Root + "/Scenes/05_WindVertexSquash.unity", OpenSceneMode.Single);
            var meshes = new[]
            {
                AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/Art/Generated/DemoCharacter_QuadMesh.asset"),
                AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/Art/Generated/DemoCharacter_SubdividedMesh.asset")
            };
            Assert.That(meshes.Select(mesh => mesh.vertexCount), Is.EquivalentTo(new[] { 4, 169 }));
            var sceneSprites = meshScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
                .Where(renderer => renderer.name is "4V VERTEX ONLY" or "169V VERTEX ONLY")
                .Select(renderer => renderer.sprite.vertices.Length).ToArray();
            Assert.That(sceneSprites, Is.EquivalentTo(new[] { 4, 169 }));
            var paths = meshScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ShaderCapturePropertyBlock>(true)).ToArray();
            Assert.That(paths.Single(item => item.name == "UV ONLY").UvWiggleAmount, Is.EqualTo(1f));
            Assert.That(paths.Single(item => item.name == "UV ONLY").VertexWindAmount, Is.EqualTo(0f));
            Assert.That(paths.Single(item => item.name == "UV ONLY").SquashAmount, Is.EqualTo(0f));
            Assert.That(paths.Single(item => item.name == "SQUASH ONLY").UvWiggleAmount, Is.EqualTo(0f));
            Assert.That(paths.Single(item => item.name == "SQUASH ONLY").VertexWindAmount, Is.EqualTo(0f));
            Assert.That(paths.Single(item => item.name == "SQUASH ONLY").SquashAmount, Is.EqualTo(1f));
            foreach (var mesh in meshes)
            foreach (var pair in mesh.vertices.Zip(mesh.uv, (vertex, uv) => (vertex, uv)).Where(pair => pair.uv.y <= .000001f))
            {
                var squash = .23f;
                var bottomY = pair.vertex.y - pair.uv.y * 2.56f;
                var deformedY = bottomY + (pair.vertex.y - bottomY) * (1f - squash);
                Assert.That(deformedY, Is.EqualTo(pair.vertex.y).Within(1e-4f), mesh.name);
            }

            foreach (var sceneName in new[] { "09_NormalMap2DLight", "10_MaskMapLighting" })
            {
                var scene = EditorSceneManager.OpenScene(Root + "/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
                var lights = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light2D>(true)).ToArray();
                Assert.That(lights.Any(light => light.lightType == Light2D.LightType.Global), Is.True, sceneName);
                Assert.That(lights.Any(light => light.lightType == Light2D.LightType.Point), Is.True, sceneName);
            }
        }

        [Test]
        public void RecorderControllerPresetsExist()
        {
            Assert.That(File.Exists(Root + "/Settings/Recorder/Still_1920x1080_PNG.asset"), Is.True);
            Assert.That(File.Exists(Root + "/Settings/Recorder/Movie_1920x1080_30fps_H264.asset"), Is.True);
            Assert.That(File.Exists(Root + "/Settings/Recorder/Movie_1920x1080_60fps_H264.asset"), Is.True);
        }

        [Test]
        public void OnlyGlitchGraphUsesCustomFunction()
        {
            foreach (var path in Directory.GetFiles(Root + "/Shaders/Graphs", "*.shadergraph"))
            {
                var customFunctions = File.ReadAllText(path).Split("CustomFunctionNode").Length - 1;
                if (Path.GetFileName(path).StartsWith("Effect06_")) Assert.That(customFunctions, Is.EqualTo(1), path);
                else Assert.That(customFunctions, Is.EqualTo(0), path);
            }
            var include = File.ReadAllText(Root + "/Shaders/Includes/ShaderCaptureEffects.hlsl");
            Assert.That(include, Does.Not.Contain("SC_Fragment"));
            Assert.That(include, Does.Contain("Effect06Fragment_float"));
            Assert.That(include, Does.Contain("Effect06Fragment_half"));
        }

        [Test]
        public void MaskSceneUsesSecondaryTextureAndThreeChannels()
        {
            var graph = File.ReadAllText(Root + "/Shaders/Graphs/Effect10_MaskMapLighting.shadergraph");
            Assert.That(graph, Does.Contain("_MaskTex"));
            Assert.That(graph, Does.Not.Contain("_SC_MaskTex"));
            var scene = EditorSceneManager.OpenScene(Root + "/Scenes/10_MaskMapLighting.unity", OpenSceneMode.Single);
            var channels = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ShaderCapturePropertyBlock>(true))
                .Select(properties => Mathf.RoundToInt(properties.MaskChannel)).OrderBy(value => value).ToArray();
            Assert.That(channels, Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void NormalSceneAndReflectionSceneHaveIsolatedComparisonInputs()
        {
            var normalScene = EditorSceneManager.OpenScene(Root + "/Scenes/09_NormalMap2DLight.unity", OpenSceneMode.Single);
            var flat = GameObject.Find("Before").GetComponentInChildren<SpriteRenderer>();
            var mapped = GameObject.Find("After").GetComponentInChildren<SpriteRenderer>();
            Assert.That(AssetDatabase.GetAssetPath(flat.sprite), Does.EndWith("DemoCharacter_Flat.png"));
            Assert.That(AssetDatabase.GetAssetPath(mapped.sprite), Does.EndWith("DemoCharacter.png"));
            Assert.That(flat.sharedMaterial, Is.SameAs(mapped.sharedMaterial));
            var normalGraph = File.ReadAllText(Root + "/Shaders/Graphs/Effect09_NormalMap2DLight.shadergraph");
            Assert.That(normalGraph, Does.Contain("Normal Strength (2.5x)"));
            Assert.That(normalGraph, Does.Contain("\"m_PerRendererData\": true"));
            var pointLights = normalScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light2D>(true))
                .Where(light => light.lightType == Light2D.LightType.Point).ToArray();
            Assert.That(pointLights, Has.Length.EqualTo(2));
            Assert.That(pointLights.All(light => new SerializedObject(light).FindProperty("m_UseNormalMap").boolValue), Is.True);
            Assert.That(normalScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
                .Count(renderer => renderer.name == "Visible Moving Light Marker"), Is.EqualTo(2));

            var reflectionScene = EditorSceneManager.OpenScene(Root + "/Scenes/11_WaterReflection.unity", OpenSceneMode.Single);
            var stable = GameObject.Find("Stable Subject").GetComponent<Renderer>().sharedMaterial;
            var reflection = GameObject.Find("Reflection").GetComponent<Renderer>().sharedMaterial;
            Assert.That(stable.name, Is.EqualTo("BaseSprite"));
            Assert.That(reflection.name, Is.EqualTo("Effect11"));
        }

        [Test]
        public void SourceBundleInputsContainCompleteProjectConfiguration()
        {
            Assert.That(File.Exists("Tools/build_review_bundle.py"), Is.True);
            Assert.That(File.Exists("ProjectSettings/GraphicsSettings.asset"), Is.True);
            Assert.That(File.Exists("ProjectSettings/QualitySettings.asset"), Is.True);
            Assert.That(File.Exists("ProjectSettings/EditorBuildSettings.asset"), Is.True);
            Assert.That(Directory.Exists("Assets/Settings"), Is.True);
        }

        [Test]
        public void DeterministicCaptureProducesIdenticalPngBytes()
        {
            var scene = EditorSceneManager.OpenScene(Root + "/Scenes/06_GlitchRgbSplit.unity", OpenSceneMode.Single);
            var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single();
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(.73f, 1.25f);
            ShaderCapture.Editor.ShaderCaptureReviewCapture.CapturePngBytes(camera); // warm shader and render targets after a clean import
            var first = ShaderCapture.Editor.ShaderCaptureReviewCapture.CapturePngBytes(camera);
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(.73f, 1.25f);
            var second = ShaderCapture.Editor.ShaderCaptureReviewCapture.CapturePngBytes(camera);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void EffectZeroOutlineAndGlitchKeepSpriteAlphaWithoutFullRect()
        {
            var outlineScene = EditorSceneManager.OpenScene(Root + "/Scenes/04_OutlineInnerRim.unity", OpenSceneMode.Single);
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(0f, 0f);
            foreach (var renderer in GameObject.Find("After").GetComponentsInChildren<SpriteRenderer>(true))
            {
                var pixels = RenderIsolated(renderer);
                var visible = pixels.Count(pixel => pixel.a > 8);
                Assert.That(visible, Is.GreaterThan(1500), renderer.name + " disappeared at Effect 0");
                Assert.That(visible, Is.LessThan(pixels.Length * .7f), renderer.name + " became a full rectangle");
            }

            var glitchScene = EditorSceneManager.OpenScene(Root + "/Scenes/06_GlitchRgbSplit.unity", OpenSceneMode.Single);
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(0f, 0f);
            var glitchPixels = RenderIsolated(GameObject.Find("After").GetComponent<SpriteRenderer>());
            var glitchVisible = glitchPixels.Count(pixel => pixel.a > 8);
            Assert.That(glitchVisible, Is.GreaterThan(1500));
            Assert.That(glitchVisible, Is.LessThan(glitchPixels.Length * .7f));
            Assert.That(glitchPixels[0].a, Is.EqualTo(0));
            Assert.That(glitchPixels[^1].a, Is.EqualTo(0));
        }

        [Test]
        public void HologramContainsIndependentDiagonalShinePath()
        {
            var graph = File.ReadAllText(Root + "/Shaders/Graphs/Effect08_HologramShine.shadergraph");
            foreach (var nodeName in new[] { "Diagonal UV Projection", "Looping Shine", "Shine Soft Leading Edge", "Shine Soft Trailing Edge", "Shine Alpha Mask", "Diagonal Shine Emission" })
                Assert.That(graph, Does.Contain(nodeName));
            EditorSceneManager.OpenScene(Root + "/Scenes/08_HologramShine.unity", OpenSceneMode.Single);
            var renderer = GameObject.Find("After").GetComponent<SpriteRenderer>();
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(1f, 0f);
            var first = RenderIsolated(renderer);
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(1f, .7f);
            var second = RenderIsolated(renderer);
            Assert.That(first.Zip(second, PixelDelta).Count(delta => delta > 18), Is.GreaterThan(100), "Hologram shine produced no time-varying pixels");
        }

        [Test]
        public void WindUvOnlyPathChangesPixelsWithoutVertexOrSquash()
        {
            EditorSceneManager.OpenScene(Root + "/Scenes/05_WindVertexSquash.unity", OpenSceneMode.Single);
            var properties = GameObject.Find("UV ONLY").GetComponent<ShaderCapturePropertyBlock>();
            Assert.That(properties.VertexWindAmount, Is.EqualTo(0f));
            Assert.That(properties.SquashAmount, Is.EqualTo(0f));
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(1f, 0f);
            var first = RenderIsolated(properties.GetComponent<SpriteRenderer>());
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(1f, .4f);
            var second = RenderIsolated(properties.GetComponent<SpriteRenderer>());
            Assert.That(first.Zip(second, PixelDelta).Count(delta => delta > 18), Is.GreaterThan(100), "UV-only subject produced no fragment-motion pixels");
        }

        [Test]
        public void DissolveSceneContainsHardSoftAndRevealVariants()
        {
            var scene = EditorSceneManager.OpenScene(Root + "/Scenes/03_Dissolve.unity", OpenSceneMode.Single);
            var variants = GameObject.Find("After").GetComponentsInChildren<ShaderCapturePropertyBlock>(true)
                .Select(item => (soft: item.DissolveSoftness > .01f, reveal: item.DissolveInvert > .5f)).ToArray();
            Assert.That(variants, Is.EquivalentTo(new[] { (false, false), (true, false), (false, true), (true, true) }));
            foreach (var properties in GameObject.Find("After").GetComponentsInChildren<ShaderCapturePropertyBlock>(true))
            {
                ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(0f, 0f);
                var start = RenderIsolated(properties.GetComponent<SpriteRenderer>());
                var startVisible = start.Count(pixel => pixel.a > 8);
                if (properties.DissolveInvert < .5f) Assert.That(startVisible, Is.GreaterThan(800), properties.name + " dissolve start");
                else Assert.That(startVisible, Is.LessThan(50), properties.name + " reveal start");

                ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(1f, 2f);
                var end = RenderIsolated(properties.GetComponent<SpriteRenderer>());
                var endVisible = end.Count(pixel => pixel.a > 8);
                if (properties.DissolveInvert < .5f) Assert.That(endVisible, Is.LessThan(50), properties.name + " dissolve end");
                else
                {
                    Assert.That(endVisible, Is.GreaterThan(800), properties.name + " reveal end");
                    var edgeDominant = end.Count(pixel => pixel.a > 8 && pixel.r > pixel.b + 40);
                    Assert.That(edgeDominant, Is.LessThan(endVisible * .25f), properties.name + " retained edge color at full reveal");
                }
            }
            var graph = File.ReadAllText(Root + "/Shaders/Graphs/Effect03_Dissolve.shadergraph");
            Assert.That(graph, Does.Contain("Soft Smoothstep Visible"));
            Assert.That(graph, Does.Contain("Dissolve / Reveal Body"));
            Assert.That(graph, Does.Contain("Exact End Body"));
        }

        [Test]
        public void AfterSpecificLabelsAndScanTargetsBelongToAfterGroup()
        {
            foreach (var sceneName in new[] { "01_HitFlashInvincible", "03_Dissolve", "04_OutlineInnerRim", "07_PixelPosterizeDither", "10_MaskMapLighting", "11_WaterReflection", "12_WorldScanReveal" })
            {
                var scene = EditorSceneManager.OpenScene(Root + "/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
                var after = GameObject.Find("After");
                Assert.That(after, Is.Not.Null, sceneName);
                if (sceneName == "12_WorldScanReveal")
                    Assert.That(after.GetComponentsInChildren<SpriteRenderer>(true).Count(renderer => renderer.name.StartsWith("Scan Target")), Is.EqualTo(5));
                if (sceneName == "11_WaterReflection")
                    Assert.That(after.GetComponentsInChildren<TextMesh>(true).Any(text => text.name == "Waterline"), Is.True);
                if (sceneName is "01_HitFlashInvincible" or "03_Dissolve" or "04_OutlineInnerRim" or "07_PixelPosterizeDither" or "10_MaskMapLighting")
                    Assert.That(after.GetComponentsInChildren<TextMesh>(true).Length, Is.GreaterThan(0), sceneName);
            }
        }

        [Test]
        public void NormalAndMaskScenesHaveVisuallyDistinctSemanticPaths()
        {
            var normalScene = EditorSceneManager.OpenScene(Root + "/Scenes/09_NormalMap2DLight.unity", OpenSceneMode.Single);
            var flatOverride = GameObject.Find("Before").GetComponentInChildren<ShaderCapturePropertyBlock>().NormalMapOverride;
            var mappedOverride = GameObject.Find("After").GetComponentInChildren<ShaderCapturePropertyBlock>().NormalMapOverride;
            Assert.That(flatOverride, Is.Not.SameAs(mappedOverride));
            Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(flatOverride)), Is.Not.EqualTo(File.ReadAllBytes(AssetDatabase.GetAssetPath(mappedOverride))));
            var normalGraph = File.ReadAllText(Root + "/Shaders/Graphs/Effect09_NormalMap2DLight.shadergraph");
            Assert.That(normalGraph, Does.Contain("Normal Dot Moving Light"));
            Assert.That(normalGraph, Does.Contain("Visible Normal Response"));
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(1f, .65f);
            var normalCamera = normalScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single();
            var comparison = CaptureCameraPixels(normalCamera);
            var asymmetricSubjectPixels = 0;
            for (var y = 250; y < 850; y++)
            for (var x = 400; x < 820; x++)
                if (PixelDelta(comparison[y * 1920 + x], comparison[y * 1920 + (1919 - x)]) > 18)
                    asymmetricSubjectPixels++;
            Assert.That(asymmetricSubjectPixels, Is.GreaterThan(500), "Flat and mapped normal comparison contained no visible pixel difference");

            var maskGraph = File.ReadAllText(Root + "/Shaders/Graphs/Effect10_MaskMapLighting.shadergraph");
            foreach (var path in new[] { "Static Wet Sheen", "Metal Mask G (URP Sprite Mask only)", "No Manual G BaseColor Light", "Weak Point Pulse", "Weak Point Emission", "Secondary Sprite Mask (_MaskTex)" })
                Assert.That(maskGraph, Does.Contain(path));
            Assert.That(maskGraph, Does.Not.Contain("\"m_OverrideReferenceName\": \"_MaskMap\""));
            Assert.That(maskGraph, Does.Contain("\"m_OverrideReferenceName\": \"_MaskTex\""));
            Assert.That(maskGraph, Does.Not.Contain("_SC_MetalLightPosition"));
            var maskScene = EditorSceneManager.OpenScene(Root + "/Scenes/10_MaskMapLighting.unity", OpenSceneMode.Single);
            var metalLight = maskScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light2D>(true))
                .Single(light => light.name == "G Metal Blend Style Light 2D");
            Assert.That(new SerializedObject(metalLight).FindProperty("m_BlendStyleIndex").intValue, Is.EqualTo(3));
            Assert.That(metalLight.targetSortingLayers, Is.EqualTo(new[] { SortingLayer.NameToID("MaskMetal") }));
            Assert.That(GameObject.Find("After").GetComponentsInChildren<Light2D>(true), Does.Contain(metalLight));
            var renderer2D = File.ReadAllText("Assets/Settings/Renderer2D.asset");
            Assert.That(renderer2D.Split("maskTextureChannel: 2").Length - 1, Is.GreaterThanOrEqualTo(2));

            var camera = maskScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single();
            var rWet = GameObject.Find("R WET Channel").GetComponent<SpriteRenderer>();
            var gMetal = GameObject.Find("G METAL Channel").GetComponent<SpriteRenderer>();
            var bWeak = GameObject.Find("B WEAK Channel").GetComponent<SpriteRenderer>();
            Assert.That(gMetal.sortingLayerName, Is.EqualTo("MaskMetal"));
            Assert.That(rWet.sortingLayerName, Is.EqualTo("Default"));
            Assert.That(bWeak.sortingLayerName, Is.EqualTo("Default"));

            var bStart = CaptureOnlyRenderer(camera, bWeak, metalLight, false, 0f);
            var bEnd = CaptureOnlyRenderer(camera, bWeak, metalLight, false, .4f);
            Assert.That(bStart.Zip(bEnd, PixelDelta).Count(delta => delta > 18), Is.GreaterThan(100), "B Weak pulse did not remain independently time-driven");
            foreach (var renderer in new[] { rWet, gMetal })
            {
                var start = CaptureOnlyRenderer(camera, renderer, metalLight, false, 0f);
                var end = CaptureOnlyRenderer(camera, renderer, metalLight, false, .4f);
                Assert.That(start.Zip(end, PixelDelta).Count(delta => delta > 18), Is.LessThan(20), renderer.name + " changed from B-only demo time");
            }
        }

        [Test]
        public void ReflectionAndWorldScanPixelRegressionsHold()
        {
            EditorSceneManager.OpenScene(Root + "/Scenes/11_WaterReflection.unity", OpenSceneMode.Single);
            var stable = GameObject.Find("Stable Subject").GetComponent<SpriteRenderer>();
            var reflection = GameObject.Find("Reflection").GetComponent<SpriteRenderer>();
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(1f, 0f);
            var stableStart = RenderIsolated(stable);
            var reflectionStart = RenderIsolated(reflection);
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(1f, .65f);
            var stableEnd = RenderIsolated(stable);
            var reflectionEnd = RenderIsolated(reflection);
            Assert.That(stableEnd, Is.EqualTo(stableStart), "Stable subject changed with reflection time");
            Assert.That(reflectionStart.Zip(reflectionEnd, PixelDelta).Count(delta => delta > 18), Is.GreaterThan(100), "Reflection produced no time-varying pixels");

            EditorSceneManager.OpenScene(Root + "/Scenes/12_WorldScanReveal.unity", OpenSceneMode.Single);
            var scanTarget = GameObject.Find("After Subject").GetComponent<SpriteRenderer>();
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(.1f, 0f);
            var scanStart = RenderIsolated(scanTarget);
            ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(.85f, 1f);
            var scanEnd = RenderIsolated(scanTarget);
            Assert.That(scanStart.Zip(scanEnd, PixelDelta).Count(delta => delta > 18), Is.GreaterThan(100), "World scan produced no reveal pixels");
        }

        static int PixelDelta(Color32 left, Color32 right)
            => Mathf.Abs(left.r - right.r) + Mathf.Abs(left.g - right.g) + Mathf.Abs(left.b - right.b) + Mathf.Abs(left.a - right.a);

        static Color32[] CaptureCameraPixels(Camera camera)
        {
            var png = ShaderCapture.Editor.ShaderCaptureReviewCapture.CapturePngBytes(camera);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            texture.LoadImage(png, false);
            var pixels = texture.GetPixels32();
            Object.DestroyImmediate(texture);
            return pixels;
        }

        static Color32[] CaptureOnlyRenderer(Camera camera, SpriteRenderer selected, Light2D light, bool lightEnabled, float demoTime)
        {
            var renderers = camera.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
            var enabled = renderers.Select(renderer => renderer.enabled).ToArray();
            var previousLight = light.enabled;
            try
            {
                foreach (var renderer in renderers) renderer.enabled = renderer == selected;
                selected.enabled = true;
                light.enabled = lightEnabled;
                ShaderCapture.Editor.ShaderCaptureReviewCapture.SetDeterministicGlobals(1f, demoTime);
                return CaptureCameraPixels(camera);
            }
            finally
            {
                for (var index = 0; index < renderers.Length; index++) renderers[index].enabled = enabled[index];
                light.enabled = previousLight;
            }
        }

        static Color32[] RenderIsolated(Renderer source)
        {
            var clone = Object.Instantiate(source.gameObject);
            clone.name = "Isolated Test Subject";
            clone.layer = 31;
            clone.transform.position = Vector3.zero;
            clone.transform.rotation = Quaternion.identity;
            clone.transform.localScale = source.transform.lossyScale;
            var cloneRenderer = clone.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            source.GetPropertyBlock(block);
            cloneRenderer.SetPropertyBlock(block);

            var cameraObject = new GameObject("Isolated Test Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 3.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 31;
            var target = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false, false);
            image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            image.Apply(false, false);
            var pixels = image.GetPixels32();
            Object.DestroyImmediate(image);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(clone);
            return pixels;
        }
    }
}
