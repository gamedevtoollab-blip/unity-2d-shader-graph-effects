using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShaderCapture
{
    public sealed class CaptureHubController : MonoBehaviour
    {
        static readonly string[] Titles =
        {
            "Hit Flash / Invincible",
            "Palette Swap",
            "Dissolve",
            "Outline / Inner Rim",
            "Wind / Vertex / Squash",
            "Glitch / RGB Split",
            "Pixel / Posterize / Dither",
            "Hologram / Shine",
            "Normal Map / 2D Light",
            "Mask Map Lighting",
            "Water Reflection",
            "World Scan / Reveal"
        };

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) Load(1);
            else if (keyboard.digit2Key.wasPressedThisFrame) Load(2);
            else if (keyboard.digit3Key.wasPressedThisFrame) Load(3);
            else if (keyboard.digit4Key.wasPressedThisFrame) Load(4);
            else if (keyboard.digit5Key.wasPressedThisFrame) Load(5);
            else if (keyboard.digit6Key.wasPressedThisFrame) Load(6);
            else if (keyboard.digit7Key.wasPressedThisFrame) Load(7);
            else if (keyboard.digit8Key.wasPressedThisFrame) Load(8);
            else if (keyboard.digit9Key.wasPressedThisFrame) Load(9);
            else if (keyboard.digit0Key.wasPressedThisFrame) Load(10);
            else if (keyboard.minusKey.wasPressedThisFrame) Load(11);
            else if (keyboard.equalsKey.wasPressedThisFrame) Load(12);
        }

        static void Load(int sceneIndex)
        {
            if (sceneIndex > 0 && sceneIndex < SceneManager.sceneCountInBuildSettings)
                SceneManager.LoadScene(sceneIndex);
        }

        void OnGUI()
        {
            var width = Mathf.Min(760f, Screen.width - 80f);
            var left = (Screen.width - width) * 0.5f;
            GUI.Box(new Rect(left, 28f, width, Screen.height - 56f), string.Empty);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(left + 20f, 48f, width - 40f, 52f), "Unity Shader Capture", titleStyle);

            var columns = 2;
            var buttonWidth = (width - 70f) / columns;
            for (var i = 0; i < Titles.Length; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var rect = new Rect(left + 24f + column * (buttonWidth + 18f), 118f + row * 66f, buttonWidth, 50f);
                if (GUI.Button(rect, $"{i + 1:00}  {Titles[i]}")) Load(i + 1);
            }
        }
    }
}
