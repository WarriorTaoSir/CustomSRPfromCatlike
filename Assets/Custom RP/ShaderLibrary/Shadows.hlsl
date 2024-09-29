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

#if defined(_OTHER_PCF3)
	#define OTHER_FILTER_SAMPLES 4
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
	#define OTHER_FILTER_SAMPLES 9
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
	#define OTHER_FILTER_SAMPLES 16
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4

// 利用TEXTURE2D_SHADOW宏来专门采样阴影贴图
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
// 利用SAMPLER_CMP宏来定义采样状态，一般线性过滤不适用于深度数据
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _ShadowAtlasSize;
	float4 _ShadowDistanceFade;
CBUFFER_END

struct DirectionalShadowData {
	float strength;
	int tileIndex;
	float normalBias;
	int shadowMaskChannel; 
};

struct OtherShadowData {
	float strength;
	int tileIndex;
	bool isPoint;
	int shadowMaskChannel;
	// 用来计算normal bias
	float3 lightPositionWS; 
	float3 lightDirectionWS;
	float3 spotDirectionWS;
};

struct ShadowMask{
	bool always;
    bool distance;
	float4 shadows;
};

struct ShadowData {
	int cascadeIndex;
	float cascadeBlend; // 级联边界混合
	float strength;
	ShadowMask shadowMask;
};



float FadedShadowStrength (float distance, float scale, float fade) {
	return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData (Surface surfaceWS) {
	ShadowData data;
	// 默认不使用shadow mask
	data.shadowMask.always = false;
	data.shadowMask.distance = false;
	data.shadowMask.shadows = 1.0;
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

    if( i == _CascadeCount && _CascadeCount > 0){
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

// 采样方向光阴影atlas
float SampleDirectionalShadowAtlas (float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

// 采样其它光阴影atlas
float SampleOtherShadowAtlas (float3 positionSTS, float3 bounds) {
	positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
	return SAMPLE_TEXTURE2D_SHADOW(
		_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

// 软化方向光阴影
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

// 软化其它光阴影
float FilterOtherShadow (float3 positionSTS, float3 bounds) {
	#if defined(OTHER_FILTER_SETUP)
		real weights[OTHER_FILTER_SAMPLES];
		real2 positions[OTHER_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.wwzz;
		OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < OTHER_FILTER_SAMPLES; i++) {
			shadow += weights[i] * SampleOtherShadowAtlas(
				float3(positions[i].xy, positionSTS.z), bounds
			);
		}
		return shadow;
	#else
		return SampleOtherShadowAtlas(positionSTS, bounds);
	#endif
}

float GetCascadedShadow(DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{
	float3 normalBias = surfaceWS.interpolatedNormal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[directional.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	).xyz;
	float shadow = FilterDirectionalShadow(positionSTS);
	if (global.cascadeBlend < 1.0) {
		normalBias = surfaceWS.interpolatedNormal *
			(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(
			_DirectionalShadowMatrices[directional.tileIndex + 1],
			float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		shadow = lerp(
			FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend
		);
	}
	return shadow;
}

// 获取烘焙阴影
float GetBakedShadow (ShadowMask mask, int channel) {
	float shadow = 1.0;
	if ((mask.always || mask.distance) && channel >= 0) {
		shadow = mask.shadows[channel];
	}
	return shadow;
}
// 带有强度的参数，然后对烘焙阴影进行插值
float GetBakedShadow (ShadowMask mask, int channel, float strength) {
	if (mask.always || mask.distance) {
		return lerp(1.0, GetBakedShadow(mask, channel), strength);
	}
	return 1.0;
}

// 将阴影强度应用上，除非有距离阴影遮罩，如果有，将实时阴影换成烘焙阴影。
float MixBakedAndRealtimeShadows(
	ShadowData global, float shadow, int shadowMaskChannel, float strength
) {
	float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
	// 如果开启的是always，那么
	if (global.shadowMask.always) {
		shadow = lerp(1.0, shadow, global.strength);
		// 烘焙阴影和实时阴影通过取最小值进行组合，
		shadow = min(baked, shadow);
		// 将灯光的阴影强度应用于结合的阴影
		return lerp(1.0, shadow, strength);
	}
	if (global.shadowMask.distance) {
		// 在烘焙阴影与实时阴影之间插值
		shadow = lerp(baked, shadow, global.strength);
		return lerp(1.0, shadow, strength);
	}
	return lerp(1.0, shadow, strength * global.strength);
}

// 计算阴影衰减值，返回值[0,1]，0表示阴影衰减最大，片元完全在阴影中，1表示阴影衰减最少，片元完全被光照射
float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData global, Surface surfaceWS) {
	// 如果不接收阴影，直接返回1
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif

	float shadow;
	// 忽略不开启阴影和阴影强度为0的光源，并且如果实时阴影强度为0，获取烘焙阴影。
	if (directional.strength * global.strength <= 0.0) {
		shadow = GetBakedShadow(global.shadowMask, directional.shadowMaskChannel, abs(directional.strength));
	}else{
		// 获取级联阴影，获取到的阴影是实时阴影
		shadow = GetCascadedShadow(directional, global, surfaceWS); 
		// 获取混合阴影，是烘焙阴影与实时阴影之间的插值
		shadow = MixBakedAndRealtimeShadows(global, shadow, directional.shadowMaskChannel, directional.strength);
	}
	return shadow;
}

static const float3 pointShadowPlanes[6] = {
	float3(-1.0, 0.0, 0.0),
	float3(1.0, 0.0, 0.0),
	float3(0.0, -1.0, 0.0),
	float3(0.0, 1.0, 0.0),
	float3(0.0, 0.0, -1.0),
	float3(0.0, 0.0, 1.0)
};

// 获得其它阴影
float GetOtherShadow(OtherShadowData other, ShadowData global, Surface surfaceWS){
	float tileIndex = other.tileIndex;
	float3 lightPlane = other.spotDirectionWS;
	// 如果是点光源，需要根据cube的face来获取tileindex
	if (other.isPoint) {
		float faceOffset = CubeMapFaceID(-other.lightDirectionWS);
		tileIndex += faceOffset;
		lightPlane = pointShadowPlanes[faceOffset];
	}
	float4 tileData = _OtherShadowTiles[tileIndex];
	float3 surfaceToLight = other.lightPositionWS - surfaceWS.position;
	float distanceToLightPlane = dot(surfaceToLight, lightPlane);
	float3 normalBias = surfaceWS.interpolatedNormal * (distanceToLightPlane * tileData.w);
	float4 positionSTS = mul(
		_OtherShadowMatrices[tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	);
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}

// 计算阴影衰减值，1是没阴影，0是完全在阴影中
float GetOtherShadowAttenuation(OtherShadowData other, ShadowData global, Surface surfaceWS) {
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif
	
	float shadow;
	//全局强度用于确定我们是否可以跳过实时阴影采样，要么是因为我们超出了阴影距离，要么是在最大的级联球体之外。
	if (other.strength * global.strength <= 0.0) { // 实时阴影强度为0，则获取烘焙阴影
		shadow = GetBakedShadow(global.shadowMask, other.shadowMaskChannel, abs(other.strength));
	}
	else { // 获取级联阴影
		shadow = GetOtherShadow(other, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(global, shadow, other.shadowMaskChannel, other.strength);
	}
	return shadow;
}

#endif