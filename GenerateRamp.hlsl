#pragma once
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#pragma region Variable
CBUFFER_START(UnityPerMaterial)
float4 _AlbedoTint;
CBUFFER_END

float4 _ColorArray[80];
float _PointArray[80];
float  _GradientNums;

TEXTURE2D(_AlbedoTex);

SamplerState Smp_ClampU_ClampV_Linear;
SamplerState Smp_ClampU_RepeatV_Linear;
SamplerState Smp_RepeatU_RepeatV_Linear;
SamplerState Smp_RepeatU_ClampV_Linear;
SamplerState Smp_Clamp_Linear;
SamplerState Smp_Repeat_Linear;
SamplerState sampler_PointClamp;
SamplerState sampler_PointRepeat;

struct VSInput
{
    float3      posOS        : POSITION;

    float3      normalOS      : NORMAL;

    float2      uv           : TEXCOORD0;
};

struct PSInput
{
    float2      uv              : TEXCOORD0;

    float3      posWS           : TEXCOORD2;
    float4      posCS           : SV_POSITION;
};

struct PSOutput
{
    float4 color : SV_TARGET;
};
#pragma endregion

PSInput VS(VSInput i)
{
    PSInput o = (PSInput)0;

    VertexPositionInputs posData  = GetVertexPositionInputs(i.posOS);
    o.posCS                  = posData.positionCS;
    o.posWS                  = posData.positionWS;

    o.uv  = i.uv;
    o.uv.y = 1.f - o.uv.y;  // 混合时序列是由上到下
    
    return o;
}

#pragma region Tools
float RemapRange(float input, float inputLow, float inputHigh, float outputLow, float outputHigh)
{
    return (input - inputLow) / (inputHigh - inputLow) * (outputHigh - outputLow) + outputLow;
}
#pragma endregion

/// <summary>
/// 计算单个gradient的颜色值
/// 在单个gradient中存在多个time,需要从左到右依次计算time,并依次根据time进行lerp
/// </summary>
/// <param name="num"> gradient index </param>
/// <param name="u"> uv.u </param>
float4 GetSingleGradient(float num, float u)
{
    int i = 0;
    int l = 0, r = 7;

    UNITY_UNROLL
    for(i = 0; i < 8; ++i)
    {
        // 找到time,且该time为lerp的最右端
        if(_PointArray[num * 10 + i] >= u)
        {
            r = i;
            break;
        }
    }
    l = max(0, r - 1);

    float4 resultColor = lerp(_ColorArray[num * 10 + l], _ColorArray[num * 10 + r],
        RemapRange(u, _PointArray[num * 10 + l], _PointArray[num * 10 + r], 0, 1));

    #if defined (_GAMMA_MODE)
        return pow(resultColor, 2.2f);
    #else
        return resultColor;
    #endif
}

PSOutput PS(PSInput i)
{
    PSOutput o;

    int nums = _GradientNums;
    #if defined(_LERP_MODE)
        if(_GradientNums > 1)
        {
            --nums;
        }
    #endif

    float level = i.uv.y * nums;
    #if defined (_LERP_MODE)
        int next = ceil(level);
        float balance = frac(level);
    
        o.color = lerp(GetSingleGradient(floor(level), i.uv.x), GetSingleGradient(next, i.uv.x), saturate(balance));
    #else
        o.color = GetSingleGradient(floor(level), i.uv.x);
    #endif

    return o;
}