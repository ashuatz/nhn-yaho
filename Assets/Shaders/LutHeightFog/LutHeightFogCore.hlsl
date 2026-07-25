#ifndef SCAVENGER_LUTHEIGHTFOG_CORE_INCLUDED
#define SCAVENGER_LUTHEIGHTFOG_CORE_INCLUDED

// 2D LUT 포그의 공통 수식.
// 스크린스페이스 패스(LutHeightFog.hlsl)와 포워드 머티리얼(ScavengerSimpleLit)이 함께 쓴다.
// 두 경로가 같은 결과를 내려면 정의가 한 곳에만 있어야 한다.
//
// 2D 포그 LUT 규약:
//   U = 카메라 선형 시야 거리 (0 = 거리 시작, 1 = 거리 끝)
//   V = 월드 Y 높이 (0 = 높이 시작, 1 = 높이 끝)
//   RGB = 포그 색, A = 포그 농도

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

TEXTURE2D(_FogLut);

// x: 거리 시작, y: 1 / (거리 끝 - 거리 시작)
// z: 높이 시작, w: 1 / (높이 끝 - 높이 시작)
float4 _FogRangeParams;

// x: 농도 배율, y: 하늘 농도 배율
// 글로벌이 세팅되지 않은 상태(전부 0)에서는 농도 0이므로 포그가 적용되지 않는다.
float4 _FogBlendParams;

#define FOG_DISTANCE_START      _FogRangeParams.x
#define FOG_DISTANCE_INVSPAN    _FogRangeParams.y
#define FOG_HEIGHT_START        _FogRangeParams.z
#define FOG_HEIGHT_INVSPAN      _FogRangeParams.w
#define FOG_DENSITY_SCALE       _FogBlendParams.x
#define FOG_SKY_DENSITY_SCALE   _FogBlendParams.y

// 원본 구현은 near/far 계수를 CPU에서 미리 곱해 두었지만,
// 여기서는 (시작, 역구간) 형태로 넘겨 같은 결과를 역보간 한 번으로 얻는다.
float ComputeFogLutCoord(float value, float start, float invSpan)
{
    return saturate((value - start) * invSpan);
}

// 투영 방식(퍼스펙티브/오소)에 상관없이 선형 시야 깊이를 얻는다.
// 뷰 공간 z는 카메라 정면이 음수이므로 부호를 뒤집는다.
// LinearEyeDepth는 오소 카메라에서 틀린 값을 준다.
float ComputeLinearEyeDepthFromWorld(float3 positionWS)
{
    float viewZ = mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).z;
    return -viewZ;
}

half4 SampleFogLut(float distanceCoord, float heightCoord)
{
    return SAMPLE_TEXTURE2D(_FogLut, sampler_LinearClamp, float2(distanceCoord, heightCoord));
}

// 월드 좌표 하나로 포그를 합성한다. 포워드 머티리얼 경로가 쓴다.
half3 ApplyLutHeightFog(half3 color, float3 positionWS)
{
    float eyeDepth = ComputeLinearEyeDepthFromWorld(positionWS);

    float distanceCoord = ComputeFogLutCoord(eyeDepth, FOG_DISTANCE_START, FOG_DISTANCE_INVSPAN);
    float heightCoord = ComputeFogLutCoord(positionWS.y, FOG_HEIGHT_START, FOG_HEIGHT_INVSPAN);

    half4 fog = SampleFogLut(distanceCoord, heightCoord);
    half density = saturate(fog.a * FOG_DENSITY_SCALE);

    return lerp(color, fog.rgb, density);
}

#endif // SCAVENGER_LUTHEIGHTFOG_CORE_INCLUDED
