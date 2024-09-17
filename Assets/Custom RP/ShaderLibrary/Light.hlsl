#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct Light {
	float3 color;
	float3 direction;
	float attenuation;
};

int GetDirectionalLightCount() {
	return _DirectionalLightCount;
}

// 构造一个光源的ShadowData
DirectionalShadowData GetDirectionalShadowData (int lightIndex) {
	DirectionalShadowData data;
	// 阴影强度
	data.strength = _DirectionalLightShadowData[lightIndex].x;
	// Tile索引
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y;
	return data;
}

// 对于每个片元，构造一个方向光源并返回，其颜色与方向取自常量缓冲区的数组中index下标处
Light GetDirectionalLight(int index, Surface surfaceWS) {
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;
	// 构造光源阴影信息
	DirectionalShadowData shadowData = GetDirectionalShadowData(index);
	// 根据片元的强度
	light.attenuation = GetDirectionalShadowAttenuation(shadowData, surfaceWS);
	return light;
}



#endif