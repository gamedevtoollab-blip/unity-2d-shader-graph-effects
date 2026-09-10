using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShaderCapture
{
    public sealed class EffectDemoController : MonoBehaviour
    {
        static readonly int EffectId = Shader.PropertyToID("_SC_Effect");
        static readonly int SeedId = Shader.PropertyToID("_SC_Seed");
        static readonly int PaletteRowId = Shader.PropertyToID("_SC_PaletteRow");
        static readonly int ColorAId = Shader.PropertyToID("_SC_ColorA");
        static readonly int ColorBId = Shader.PropertyToID("_SC_ColorB");
        static readonly int ScanCenterId = Shader.PropertyToID("_SC_ScanCenter");
        static readonly int ScanRadiusId = Shader.PropertyToID("_SC_ScanRadius");
        static readonly int NoiseTexId = Shader.PropertyToID("_SC_NoiseTex");
        static readonly int IndexTexId = Shader.PropertyToID("_SC_IndexTex");
        static readonly int PaletteTexId = Shader.PropertyToID("_SC_PaletteTex");
        static readonly int TexelSizeId = Shader.PropertyToID("_SC_TexelSize");
        static readonly int DemoTimeId = Shader.PropertyToID("_SC_DemoTime");
        static readonly int SpriteSizeId = Shader.PropertyToID("_SC_SpriteSize");

        [SerializeField, Range(1, 12)] int sceneIndex = 1;
        [SerializeField] string effectTitle = "Shader Capture";
        [SerializeField] float playbackSpeed = 0.24f;
        [SerializeField] float seed = 0.173f;
        [SerializeField] bool autoPlay = true;
        [SerializeField] bool showHud = true;
        [SerializeField] bool showBefore = true;
        [SerializeField] GameObject beforeRoot;
        [SerializeField] GameObject afterRoot;
        [SerializeField] Texture2D noiseTexture;
        [SerializeField] Texture2D indexTexture;
        [SerializeField] Texture2D paletteTexture;

        float effect;
        float demoTime;
        Vector3 afterOriginalPosition;

        public int SceneIndex => sceneIndex;
        public float Effect => effect;
        public float DemoTime => demoTime;
        public bool IsAutoPlaying => autoPlay;
        public bool IsHudVisible => showHud;
        public bool IsBeforeVisible => showBefore;

        void OnEnable()
        {
            demoTime = 0f;
            if (afterRoot != null) afterOriginalPosition = afterRoot.transform.position;
            PushStaticGlobals();
            ApplyEffect(0f);
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(EffectId, 0f);
            Shader.SetGlobalFloat(DemoTimeId, 0f);
        }

        void Update()
        {
            HandleKeyboard();
            if (autoPlay)
            {
                demoTime += Time.unscaledDeltaTime;
                ApplyEffect(Mathf.PingPong(demoTime * playbackSpeed, 1f));
                PushDemoTime();
            }
        }

        void HandleKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                autoPlay = !autoPlay;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                ResetDemo();
            }

            if (keyboard.hKey.wasPressedThisFrame) showHud = !showHud;
            if (keyboard.bKey.wasPressedThisFrame)
            {
                SetBeforeVisible(!showBefore);
            }

            if (keyboard.leftArrowKey.isPressed)
            {
                SetManualEffect(effect - Time.unscaledDeltaTime * 0.35f);
            }

            if (keyboard.rightArrowKey.isPressed)
            {
                SetManualEffect(effect + Time.unscaledDeltaTime * 0.35f);
            }

            if (keyboard.pageUpKey.wasPressedThisFrame) LoadRelative(-1);
            if (keyboard.pageDownKey.wasPressedThisFrame) LoadRelative(1);
        }

        void LoadRelative(int delta)
        {
            var next = Mathf.Clamp(sceneIndex + delta, 1, 12);
            if (next == sceneIndex) return;
            SceneManager.LoadScene(next);
        }

        void PushStaticGlobals()
        {
            Shader.SetGlobalFloat(SeedId, seed);
            Shader.SetGlobalColor(ColorAId, new Color(0.12f, 0.88f, 1.0f, 1f));
            Shader.SetGlobalColor(ColorBId, new Color(1.0f, 0.18f, 0.42f, 1f));
            if (noiseTexture != null) Shader.SetGlobalTexture(NoiseTexId, noiseTexture);
            if (indexTexture != null) Shader.SetGlobalTexture(IndexTexId, indexTexture);
            if (paletteTexture != null) Shader.SetGlobalTexture(PaletteTexId, paletteTexture);
            if (indexTexture != null)
                Shader.SetGlobalVector(TexelSizeId, new Vector4(1f / indexTexture.width, 1f / indexTexture.height, indexTexture.width, indexTexture.height));
            Shader.SetGlobalVector(SpriteSizeId, new Vector4(2.56f, 2.56f, 1f / 2.56f, 1f / 2.56f));
            if (beforeRoot != null) beforeRoot.SetActive(showBefore);
            if (afterRoot != null)
            {
                afterRoot.SetActive(true);
                afterRoot.transform.position = showBefore ? afterOriginalPosition : new Vector3(0f, afterOriginalPosition.y, afterOriginalPosition.z);
            }
            PushDemoTime();
        }

        public void SetAutoPlay(bool value)
        {
            autoPlay = value;
        }

        public void SetBeforeVisible(bool value)
        {
            showBefore = value;
            if (beforeRoot != null) beforeRoot.SetActive(showBefore);
            if (afterRoot != null)
                afterRoot.transform.position = showBefore ? afterOriginalPosition : new Vector3(0f, afterOriginalPosition.y, afterOriginalPosition.z);
        }

        public void SetHudVisible(bool value) => showHud = value;

        public void ResetDemo()
        {
            autoPlay = false;
            demoTime = 0f;
            showHud = true;
            SetBeforeVisible(true);
            if (afterRoot != null) afterRoot.transform.position = afterOriginalPosition;
            PushStaticGlobals();
            ApplyEffect(0f);
            PushDemoTime();
        }

        public void SetManualEffect(float value)
        {
            autoPlay = false;
            ApplyEffect(value);
            demoTime = effect / Mathf.Max(playbackSpeed, 0.001f);
            PushDemoTime();
        }

        void PushDemoTime()
        {
            Shader.SetGlobalFloat(DemoTimeId, demoTime);
        }


        void ApplyEffect(float value)
        {
            effect = Mathf.Clamp01(value);
            Shader.SetGlobalFloat(EffectId, effect);
            Shader.SetGlobalFloat(PaletteRowId, Mathf.Min(3f, Mathf.Floor(effect * 4f)));

            var scanX = Mathf.Lerp(-4.6f, 4.6f, effect);
            Shader.SetGlobalVector(ScanCenterId, new Vector4(scanX, 0f, 0f, 0f));
            Shader.SetGlobalFloat(ScanRadiusId, Mathf.Lerp(0.2f, 6.5f, effect));
        }

        void OnGUI()
        {
            if (!showHud) return;

            var scale = Mathf.Max(1f, Screen.height / 900f);
            var box = new Rect(24f * scale, 22f * scale, 580f * scale, 128f * scale);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            box = new Rect(24f, 22f, 580f, 128f);
            GUI.Box(box, string.Empty);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = new Color(0.82f, 0.9f, 1f) }
            };

            GUI.Label(new Rect(42f, 30f, 520f, 34f), $"{sceneIndex:00}  {effectTitle}", titleStyle);
            GUI.Label(new Rect(42f, 66f, 520f, 24f), $"Effect {effect:0.000}   Time {demoTime:0.000}s   {(autoPlay ? "AUTO" : "MANUAL")}", bodyStyle);
            GUI.Label(new Rect(42f, 94f, 520f, 24f), "Space: Play/Pause   R: Full Reset + Pause   H: HUD   B: Before   Left/Right: Scrub", bodyStyle);
            GUI.matrix = previousMatrix;
        }
    }
}
