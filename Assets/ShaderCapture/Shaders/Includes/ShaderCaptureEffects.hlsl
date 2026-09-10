#ifndef SHADER_CAPTURE_GLITCH_INCLUDED
#define SHADER_CAPTURE_GLITCH_INCLUDED

// Scene 06 is the article's deliberate Custom Function example.
// All other effects are generated from standard Shader Graph nodes.
float _SC_Effect;
float _SC_Seed;
float _SC_DemoTime;
float4 _SC_TexelSize;

float SC_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

void Effect06Fragment_float(float4 BaseRGBA, float2 UV, float3 WorldPos, float3 ScreenPos,
    out float3 OutColor, out float OutAlpha)
{
    float effect = saturate(_SC_Effect);
    float4 base = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, UV);
    float row = floor(UV.y * 18.0);
    float tick = floor(_SC_DemoTime * 9.0);
    float random = SC_Hash21(float2(row, tick) + _SC_Seed);
    float active = step(lerp(0.98, 0.58, effect), random);
    float offset = (random * 2.0 - 1.0) * _SC_TexelSize.x * 22.0 * active * effect;
    float chroma = _SC_TexelSize.x * 5.0 * effect;
    float4 center = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, UV + float2(offset, 0.0));
    float red = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, UV + float2(offset + chroma, 0.0)).r;
    float blue = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, UV + float2(offset - chroma, 0.0)).b;
    OutColor = lerp(base.rgb, float3(red, center.g, blue), effect);
    OutAlpha = lerp(base.a, center.a, effect);
}

void Effect06Fragment_half(half4 BaseRGBA, half2 UV, half3 WorldPos, half3 ScreenPos,
    out half3 OutColor, out half OutAlpha)
{
    float3 color;
    float alpha;
    Effect06Fragment_float((float4)BaseRGBA, (float2)UV, (float3)WorldPos, (float3)ScreenPos, color, alpha);
    OutColor = (half3)color;
    OutAlpha = (half)alpha;
}

#endif
