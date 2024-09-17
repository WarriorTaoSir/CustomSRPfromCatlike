#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4

// 利用TEXTURE2D_SHADOW宏来专门采样阴影贴图
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
// 利用SAMPLER_CMP宏来定义采样状态，一般线性过滤不适用于深度数据
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct DirectionalShadowData {
	float strength;
	int tileIndex;
};

float SampleDirectionalShadowAtlas (float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}
// 计算阴影衰减值，返回值[0,1]，0表示阴影衰减最大，片元完全在阴影中，1表示阴影衰减最少，片元完全被光照射
float GetDirectionalShadowAttenuation(DirectionalShadowData data, Surface surfaceWS) {
	// 忽略不开启阴影和阴影强度为0的光源
	if (data.strength <= 0.0) {
		return 1.0;
	}

	float3 positionSTS = mul(
		_DirectionalShadowMatrices[data.tileIndex],
		float4(surfaceWS.position, 1.0)
	).xyz;
	float shadow = SampleDirectionalShadowAtlas(positionSTS);
	return lerp(1.0, shadow, data.strength);
}

#endif