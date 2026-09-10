using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShaderCapture.Editor
{
    static class ShaderGraphEvidenceCapture
    {
        static readonly string[] GraphNames =
        {
            "Effect01_HitFlashInvincible", "Effect02_PaletteSwap", "Effect03_Dissolve",
            "Effect04_OutlineInnerRim", "Effect05_WindVertexSquash", "Effect06_GlitchRgbSplit",
            "Effect07_PixelPosterizeDither", "Effect08_HologramShine", "Effect09_NormalMap2DLight",
            "Effect10_MaskMapLighting", "Effect11_WaterReflection", "Effect12_WorldScanReveal"
        };

        static int currentIndex;
        static int waitFrames;
        static bool capturePending;
        static int captureStage;
        static string outputRoot;
        static EditorWindow currentGraphWindow;

        [MenuItem("Tools/Shader Capture/Generate Shader Graph Evidence")]
        static void StartCapture()
        {
            if (capturePending) return;
            outputRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "ShaderGraphEvidence"));
            Directory.CreateDirectory(outputRoot);
            currentIndex = 0;
            capturePending = true;
            captureStage = 0;
            EditorApplication.update += Tick;
            EditorApplication.delayCall += OpenCurrentGraph;
            Debug.Log("[ShaderCapture] Starting Shader Graph evidence capture: " + outputRoot);
        }

        static void OpenCurrentGraph()
        {
            var path = "Assets/ShaderCapture/Shaders/Graphs/" + GraphNames[currentIndex] + ".shadergraph";
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null || !AssetDatabase.OpenAsset(asset))
                throw new InvalidOperationException("Could not open Shader Graph: " + path);
            currentGraphWindow = EditorWindow.focusedWindow;
            waitFrames = 24;
            captureStage = 1;
        }

        static void Tick()
        {
            if (!capturePending) return;
            if (waitFrames-- > 0) return;
            if (captureStage == 2)
            {
                CaptureWindow(currentGraphWindow);
                return;
            }

            var window = currentGraphWindow;
            if (window == null)
            {
                waitFrames = 8;
                return;
            }

            var graphView = window.rootVisualElement.Query<GraphView>().First();
            graphView?.FrameAll();
            window.maximized = true;
            window.Focus();
            window.Repaint();
            captureStage = 2;
            waitFrames = 10;
        }

        static void CaptureWindow(EditorWindow window)
        {
            var rect = window.position;
            var width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            var pixels = InternalEditorUtility.ReadScreenPixel(rect.position, width, height);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(Path.Combine(outputRoot, GraphNames[currentIndex] + ".png"), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("[ShaderCapture] Graph evidence " + (currentIndex + 1) + "/" + GraphNames.Length + ": " + GraphNames[currentIndex]);
            window.Close();

            currentIndex++;
            if (currentIndex >= GraphNames.Length)
            {
                EditorApplication.update -= Tick;
                EditorSceneManager.OpenScene("Assets/ShaderCapture/Scenes/00_CaptureHub.unity", OpenSceneMode.Single);
                Debug.Log("[ShaderCapture] Generated 12 Shader Graph evidence screenshots: " + outputRoot);
                return;
            }

            captureStage = 0;
            EditorApplication.delayCall += OpenCurrentGraph;
        }
    }
}
