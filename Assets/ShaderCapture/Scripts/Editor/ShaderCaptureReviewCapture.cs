using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShaderCapture.Editor
{
    public static class ShaderCaptureReviewCapture
    {
        const string SceneRoot = "Assets/ShaderCapture/Scenes";
        const int Width = 1920;
        const int Height = 1080;
        const int FramesPerScene = 120;

        static readonly string[] SceneNames =
        {
            "01_HitFlashInvincible", "02_PaletteSwap", "03_Dissolve", "04_OutlineInnerRim",
            "05_WindVertexSquash", "06_GlitchRgbSplit", "07_PixelPosterizeDither",
            "08_HologramShine", "09_NormalMap2DLight", "10_MaskMapLighting",
            "11_WaterReflection", "12_WorldScanReveal"
        };

        static readonly float[] ReviewEffectValues =
        {
            .22f, .42f, .48f, .95f, .72f, .92f,
            .88f, .78f, .92f, .82f, .68f, .45f
        };

        [MenuItem("Tools/Shader Capture/Open Capture Hub")]
        static void OpenCaptureHub()
        {
            EditorSceneManager.OpenScene(SceneRoot + "/00_CaptureHub.unity");
        }

        [MenuItem("Tools/Shader Capture/Set Game View 1920x1080")]
        static void SetGameView1920x1080()
        {
            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
            var sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
            var gameViewType = editorAssembly.GetType("UnityEditor.GameView");
            if (sizesType == null || sizeType == null || gameViewType == null)
                throw new InvalidOperationException("Unity Game View size API was not found.");
            var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var getGroup = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.Instance);
            var group = getGroup?.Invoke(instance, new object[] { 0 });
            if (group == null) throw new InvalidOperationException("Standalone Game View size group was not found.");
            var countMethod = group.GetType().GetMethod("GetTotalCount", BindingFlags.Public | BindingFlags.Instance);
            var getSizeMethod = group.GetType().GetMethod("GetGameViewSize", BindingFlags.Public | BindingFlags.Instance);
            var addMethod = group.GetType().GetMethod("AddCustomSize", BindingFlags.Public | BindingFlags.Instance);
            var index = -1;
            var count = (int)(countMethod?.Invoke(group, null) ?? 0);
            for (var i = 0; i < count; i++)
            {
                var size = getSizeMethod?.Invoke(group, new object[] { i });
                var width = (int)(sizeType.GetProperty("width")?.GetValue(size) ?? 0);
                var height = (int)(sizeType.GetProperty("height")?.GetValue(size) ?? 0);
                if (width == 1920 && height == 1080) { index = i; break; }
            }
            if (index < 0)
            {
                var size = Activator.CreateInstance(sizeType, 1, 1920, 1080, "Shader Capture 1920x1080");
                addMethod?.Invoke(group, new[] { size });
                index = (int)(countMethod?.Invoke(group, null) ?? 1) - 1;
            }
            var gameView = EditorWindow.GetWindow(gameViewType);
            gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(gameView, index);
            gameView.Repaint();
            Debug.Log("[ShaderCapture] Game View set to 1920x1080.");
        }

        [MenuItem("Tools/Shader Capture/Take Reference Screenshot")]
        static void TakeReferenceScreenshot()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var output = Path.Combine(projectRoot, "Captures", EditorSceneManager.GetActiveScene().name + "_reference.png");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            SetGlobals(.72f, 1.25f);
            Capture(Camera.main, output);
            Debug.Log("[ShaderCapture] Reference screenshot: " + output);
        }

        [MenuItem("Tools/Shader Capture/Validate All Scenes")]
        static void ValidateAllScenes()
        {
            var errors = 0;
            foreach (var sceneName in SceneNames)
            {
                var scene = EditorSceneManager.OpenScene(SceneRoot + "/" + sceneName + ".unity", OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                if (roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Count() != 1) errors++;
                if (roots.SelectMany(root => root.GetComponentsInChildren<ShaderCapture.EffectDemoController>(true)).Count() != 1) errors++;
                errors += roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).Count(renderer => renderer.sharedMaterial == null);
            }
            if (errors == 0) Debug.Log("[ShaderCapture] Validation passed: 12 scenes, 0 missing cameras/controllers/materials.");
            else Debug.LogError("[ShaderCapture] Validation failed with " + errors + " structural error(s).");
        }

        [MenuItem("Tools/Shader Capture/Generate Validation Stills")]
        static void GenerateValidationStills()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputRoot = Path.Combine(projectRoot, "Captures", "Round2Validation");
            Directory.CreateDirectory(outputRoot);
            for (var index = 0; index < SceneNames.Length; index++)
            {
                EditorSceneManager.OpenScene(SceneRoot + "/" + SceneNames[index] + ".unity", OpenSceneMode.Single);
                SetGlobals(ReviewEffectValues[index], ReviewEffectValues[index] * 2f);
                Capture(Camera.main, Path.Combine(outputRoot, SceneNames[index] + "_comparison.png"));
            }
            EditorSceneManager.OpenScene(SceneRoot + "/00_CaptureHub.unity", OpenSceneMode.Single);
            Debug.Log("[ShaderCapture] Generated 12 Round 2 validation stills: " + outputRoot);
        }

        [MenuItem("Tools/Shader Capture/Generate Review Captures")]
        static void GenerateReviewCaptures()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var reviewRoot = Path.Combine(projectRoot, "ReviewBundles");
            Directory.CreateDirectory(reviewRoot);
            var round = 1;
            while (Directory.Exists(Path.Combine(reviewRoot, "round-" + round.ToString("00")))) round++;
            var roundRoot = Path.Combine(reviewRoot, "round-" + round.ToString("00"));
            var screenshotRoot = Path.Combine(roundRoot, "screenshots");
            var frameRoot = Path.Combine(projectRoot, "Temp", "ShaderCaptureReviewFrames", "round-" + round.ToString("00"));
            Directory.CreateDirectory(screenshotRoot);
            Directory.CreateDirectory(frameRoot);

            var globalFrame = 0;
            for (var index = 0; index < SceneNames.Length; index++)
            {
                var sceneName = SceneNames[index];
                EditorSceneManager.OpenScene(SceneRoot + "/" + sceneName + ".unity", OpenSceneMode.Single);
                var camera = Camera.main;
                if (camera == null) throw new InvalidOperationException("Main Camera missing: " + sceneName);
                SetGlobals(ReviewEffectValues[index], ReviewEffectValues[index] * 2f);
                Capture(camera, Path.Combine(screenshotRoot, sceneName + "_comparison.png"));
                CaptureSolo(camera, "Before", Path.Combine(screenshotRoot, sceneName + "_before.png"));
                CaptureSolo(camera, "After", Path.Combine(screenshotRoot, sceneName + "_after.png"));

                for (var frame = 0; frame < FramesPerScene; frame++)
                {
                    var effect = frame < 15 ? 0f
                        : frame < 75 ? (frame - 15) / 59f
                        : frame < 90 ? 1f
                        : 1f - (frame - 90) / 29f;
                    SetGlobals(effect, frame / 30f);
                    Capture(camera, Path.Combine(frameRoot, "frame_" + globalFrame.ToString("0000") + ".png"));
                    globalFrame++;
                }
            }

            var state = "round=" + round.ToString("00") + Environment.NewLine +
                        "roundRoot=" + roundRoot + Environment.NewLine +
                        "frameRoot=" + frameRoot + Environment.NewLine +
                        "frameCount=" + globalFrame + Environment.NewLine +
                        "framesPerScene=" + FramesPerScene + Environment.NewLine +
                        "width=" + Width + Environment.NewLine +
                        "height=" + Height + Environment.NewLine;
            File.WriteAllText(Path.Combine(reviewRoot, ".capture-state.txt"), state);
            EditorSceneManager.OpenScene(SceneRoot + "/00_CaptureHub.unity", OpenSceneMode.Single);
            Debug.Log("[ShaderCapture] Review captures generated for round-" + round.ToString("00") + ": " + globalFrame + " video frames and " + (SceneNames.Length * 3) + " screenshots.");
        }

        static void CaptureSolo(Camera camera, string targetName, string output)
        {
            var before = GameObject.Find("Before");
            var after = GameObject.Find("After");
            if (before == null || after == null)
            {
                Capture(camera, output);
                return;
            }

            var beforeActive = before.activeSelf;
            var afterActive = after.activeSelf;
            var beforePosition = before.transform.position;
            var afterPosition = after.transform.position;
            var stageObjects = new[]
            {
                GameObject.Find("Before Panel"), GameObject.Find("After Panel"),
                GameObject.Find("Before Label"), GameObject.Find("After Label")
            };
            var stageActive = stageObjects.Select(item => item != null && item.activeSelf).ToArray();
            var stagePositions = stageObjects.Select(item => item != null ? item.transform.position : Vector3.zero).ToArray();
            try
            {
                var showBefore = targetName == "Before";
                before.SetActive(showBefore);
                after.SetActive(!showBefore);
                var target = showBefore ? before : after;
                var targetPosition = showBefore ? beforePosition : afterPosition;
                target.transform.position = new Vector3(0f, targetPosition.y, targetPosition.z);
                for (var index = 0; index < stageObjects.Length; index++)
                {
                    var item = stageObjects[index];
                    if (item == null) continue;
                    var selectedSide = index % 2 == (showBefore ? 0 : 1);
                    item.SetActive(selectedSide);
                    if (selectedSide)
                    {
                        var position = item.transform.position;
                        item.transform.position = new Vector3(0f, position.y, position.z);
                    }
                }
                Capture(camera, output);
            }
            finally
            {
                before.transform.position = beforePosition;
                after.transform.position = afterPosition;
                before.SetActive(beforeActive);
                after.SetActive(afterActive);
                for (var index = 0; index < stageObjects.Length; index++)
                    if (stageObjects[index] != null)
                    {
                        stageObjects[index].transform.position = stagePositions[index];
                        stageObjects[index].SetActive(stageActive[index]);
                    }
            }
        }

        static void SetGlobals(float effect, float demoTime)
        {
            Shader.SetGlobalFloat("_SC_Effect", Mathf.Clamp01(effect));
            Shader.SetGlobalFloat("_SC_DemoTime", Mathf.Max(0f, demoTime));
            Shader.SetGlobalFloat("_SC_Seed", .173f);
            Shader.SetGlobalFloat("_SC_PaletteRow", Mathf.Min(3f, Mathf.Floor(effect * 4f)));
            Shader.SetGlobalColor("_SC_ColorA", new Color(.12f, .88f, 1f, 1f));
            Shader.SetGlobalColor("_SC_ColorB", new Color(1f, .18f, .42f, 1f));
            Shader.SetGlobalVector("_SC_ScanCenter", new Vector4(Mathf.Lerp(-4.6f, 4.6f, effect), 0f, 0f, 0f));
            Shader.SetGlobalFloat("_SC_ScanRadius", Mathf.Lerp(.2f, 6.5f, effect));
            Shader.SetGlobalVector("_SC_TexelSize", new Vector4(1f / 256f, 1f / 256f, 256f, 256f));
            Shader.SetGlobalVector("_SC_SpriteSize", new Vector4(2.56f, 2.56f, 1f / 2.56f, 1f / 2.56f));
            SetTexture("_SC_NoiseTex", "Assets/ShaderCapture/Art/Generated/Noise.png");
            SetTexture("_SC_IndexTex", "Assets/ShaderCapture/Art/Generated/DemoCharacter_Index.png");
            SetTexture("_SC_PaletteTex", "Assets/ShaderCapture/Art/Generated/Palette.png");
            var lightRig = UnityEngine.Object.FindAnyObjectByType<ShaderCapture.DeterministicLightRig>();
            if (lightRig != null) lightRig.ApplyTime(demoTime);
        }

        public static void SetDeterministicGlobals(float effect, float demoTime) => SetGlobals(effect, demoTime);

        static void SetTexture(string property, string assetPath)
        {
            Shader.SetGlobalTexture(property, AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
        }

        static void Capture(Camera camera, string output)
        {
            var png = CapturePngBytes(camera);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllBytes(output, png);
        }

        public static byte[] CapturePngBytes(Camera camera)
        {
            var renderTexture = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply(false, false);
            var png = image.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(image);
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
            return png;
        }
    }
}
