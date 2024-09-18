#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
	#define DIRECTIONAL_FILTER_SAMPLES 4
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
	#define DIRECTIONAL_FILTER_SAMPLES 9
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
	#define DIRECTIONAL_FILTER_SAMPLES 16
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

// 利用TEXTURE2D_SHADOW宏来专门采样阴影贴图
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
// 利用SAMPLER_CMP宏来定义采样状态，一般线性过滤不适用于深度数据
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4 _ShadowAtlasSize;
	float4 _ShadowDistanceFade;
CBUFFER_END

struct DirectionalShadowData {
	float strength;
	int tileIndex;
	float normalBias;
};

struct ShadowData {
	int cascadeIndex;
	float cascadeBlend; // 级联边界混合
	float strength;
};

float FadedShadowStrength (float distance, float scale, float fade) {
	return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData (Surface surfaceWS) {
	ShadowData data;
	data.cascadeBlend = 1.0;
	data.strength = FadedShadowStrength(
		surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y
	);
	int i;
	for (i = 0; i < _CascadeCount; i++) {
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
		if (distanceSqr < sphere.w) {
			float fade = FadedShadowStrength(
				distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z
			);
			if(i == _CascadeCount - 1){
				data.strength *= fade;
			}else{
				data.cascadeBlend = fade;
			}
			break;
		}
	}

    if( i == _CascadeCount ){
		data.strength = 0.0;
	} 
	// 若使用抖动，如果不在最后一级联，跳转到下一级联如果混合值小于抖动值
	#if defined(_CASCADE_BLEND_DITHER)
		else if (data.cascadeBlend < surfaceWS.dither) {
			i += 1;
		}
	#endif

	// 不使用软混合则将cascadeBlend清零
	#if !defined(_CASCADE_BLEND_SOFT)
		data.cascadeBlend = 1.0;
	#endif

	data.cascadeIndex = i;
	return data;
}

float SampleDirectionalShadowAtlas (float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}


float FilterDirectionalShadow (float3 positionSTS) {
	#if defined(DIRECTIONAL_FILTER_SETUP) // 软阴影
		float weights[DIRECTIONAL_FILTER_SAMPLES];
		float2 positions[DIRECTIONAL_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.yyxx;
		DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) {
			shadow += weights[i] * SampleDirectionalShadowAtlas(
				float3(positions[i].xy, positionSTS.z)
			);
		}
		return shadow;
	#else  // 硬阴影
		return SampleDirectionalShadowAtlas(positionSTS);
	#endif
}
// 计算阴影衰减值，返回值[0,1]，0表示阴影衰减最大，片元完全在阴影中，1表示阴影衰减最少，片元完全被光照射
float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData global, Surface surfaceWS) {
	// 如果不接收阴影，直接返回1
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif
	// 忽略不开启阴影和阴影强度为0的光源
	if (directional.strength <= 0.0) {
		return 1.0;
	}
	float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[directional.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	).xyz;
	float shadow = FilterDirectionalShadow(positionSTS);
	if (global.cascadeBlend < 1.0) {
		normalBias = surfaceWS.normal *
			(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(
			_DirectionalShadowMatrices[directional.tileIndex + 1],
			float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		shadow = lerp(
			FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend
		);
	}
	return lerp(1.0, shadow, directional.strength);
}

#endif