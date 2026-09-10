using UnityEngine;

namespace ShaderCapture
{
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public sealed class ShaderCapturePropertyBlock : MonoBehaviour
    {
        static readonly int DitherSpaceId = Shader.PropertyToID("_SC_DitherSpace");
        static readonly int NormalStrengthId = Shader.PropertyToID("_SC_NormalStrength");
        static readonly int MaskChannelId = Shader.PropertyToID("_SC_MaskChannel");
        static readonly int SeedOffsetId = Shader.PropertyToID("_SC_SeedOffset");
        static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
        static readonly int OutlineDirectionsId = Shader.PropertyToID("_SC_OutlineDirections");
        static readonly int DissolveSoftnessId = Shader.PropertyToID("_SC_DissolveSoftness");
        static readonly int DissolveInvertId = Shader.PropertyToID("_SC_DissolveInvert");
        static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        static readonly int UvWiggleAmountId = Shader.PropertyToID("_SC_UVWiggleAmount");
        static readonly int VertexWindAmountId = Shader.PropertyToID("_SC_VertexWindAmount");
        static readonly int SquashAmountId = Shader.PropertyToID("_SC_SquashAmount");

        [SerializeField, Range(0f, 1f)] float ditherSpace;
        [SerializeField, Range(0f, 1f)] float normalStrength = 1f;
        [SerializeField, Range(-1f, 3f)] float maskChannel = -1f;
        [SerializeField] float seedOffset;
        [SerializeField] Texture2D normalMapOverride;
        [SerializeField, Range(4f, 8f)] float outlineDirections = 8f;
        [SerializeField, Range(0f, .2f)] float dissolveSoftness;
        [SerializeField, Range(0f, 1f)] float dissolveInvert;
        [SerializeField] Texture2D mainTextureOverride;
        [SerializeField, Range(0f, 1f)] float uvWiggleAmount = 1f;
        [SerializeField, Range(0f, 1f)] float vertexWindAmount = 1f;
        [SerializeField, Range(0f, 1f)] float squashAmount = 1f;

        Renderer cachedRenderer;
        MaterialPropertyBlock properties;

        public float DitherSpace { get => ditherSpace; set { ditherSpace = Mathf.Clamp01(value); Apply(); } }
        public float NormalStrength { get => normalStrength; set { normalStrength = Mathf.Clamp01(value); Apply(); } }
        public float MaskChannel { get => maskChannel; set { maskChannel = Mathf.Clamp(value, -1f, 3f); Apply(); } }
        public float SeedOffset { get => seedOffset; set { seedOffset = value; Apply(); } }
        public Texture2D NormalMapOverride { get => normalMapOverride; set { normalMapOverride = value; Apply(); } }
        public float OutlineDirections { get => outlineDirections; set { outlineDirections = value < 6f ? 4f : 8f; Apply(); } }
        public float DissolveSoftness { get => dissolveSoftness; set { dissolveSoftness = Mathf.Clamp(value, 0f, .2f); Apply(); } }
        public float DissolveInvert { get => dissolveInvert; set { dissolveInvert = Mathf.Clamp01(value); Apply(); } }
        public Texture2D MainTextureOverride { get => mainTextureOverride; set { mainTextureOverride = value; Apply(); } }
        public float UvWiggleAmount { get => uvWiggleAmount; set { uvWiggleAmount = Mathf.Clamp01(value); Apply(); } }
        public float VertexWindAmount { get => vertexWindAmount; set { vertexWindAmount = Mathf.Clamp01(value); Apply(); } }
        public float SquashAmount { get => squashAmount; set { squashAmount = Mathf.Clamp01(value); Apply(); } }

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            cachedRenderer ??= GetComponent<Renderer>();
            if (cachedRenderer == null) return;
            properties ??= new MaterialPropertyBlock();
            cachedRenderer.GetPropertyBlock(properties);
            properties.SetFloat(DitherSpaceId, ditherSpace);
            properties.SetFloat(NormalStrengthId, normalStrength);
            properties.SetFloat(MaskChannelId, maskChannel);
            properties.SetFloat(SeedOffsetId, seedOffset);
            if (normalMapOverride != null) properties.SetTexture(NormalMapId, normalMapOverride);
            properties.SetFloat(OutlineDirectionsId, outlineDirections);
            properties.SetFloat(DissolveSoftnessId, dissolveSoftness);
            properties.SetFloat(DissolveInvertId, dissolveInvert);
            if (mainTextureOverride != null) properties.SetTexture(MainTextureId, mainTextureOverride);
            properties.SetFloat(UvWiggleAmountId, uvWiggleAmount);
            properties.SetFloat(VertexWindAmountId, vertexWindAmount);
            properties.SetFloat(SquashAmountId, squashAmount);
            cachedRenderer.SetPropertyBlock(properties);
        }
    }
}
