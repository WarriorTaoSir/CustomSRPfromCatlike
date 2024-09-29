#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
	
	int _OtherLightCount;
	float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];    // 其他光源颜色数组
	float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT]; // 其他类型光源位置，w分量是1/r方
	float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];// 其他类型方向
	float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];// 其他类型角度
	float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];// 其他光源的阴影数据
CBUFFER_END

struct Light {
	float3 color;
	float3 direction;
	float attenuation;
};

int GetDirectionalLightCount() {
	return _DirectionalLightCount;
}

int GetOtherLightCount () {
	return _OtherLightCount;
}

// 构造一个光源的ShadowData
DirectionalShadowData GetDirectionalShadowData (int lightIndex, ShadowData shadowData) {
	DirectionalShadowData data;
	// 阴影强度
	data.strength = _DirectionalLightShadowData[lightIndex].x;
	// Tile索引
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
	data.normalBias = _DirectionalLightShadowData[lightIndex].z;
	data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w; // 第四个分量时该光源对应的ShadowMask通道
	return data;
}

// 构造一个其他类型光源的shadowData
OtherShadowData GetOtherShadowData (int lightIndex) {
	OtherShadowData data;
	data.strength = _OtherLightShadowData[lightIndex].x;
	data.tileIndex = _OtherLightShadowData[lightIndex].y;
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	data.isPoint = _OtherLightShadowData[lightIndex].z == 1.0;
	// 初始化位置信息
	data.lightPositionWS = 0.0;
	data.lightDirectionWS = 0.0;
	data.spotDirectionWS = 0.0;
	return data;
}

// 对于每个片元，构造一个方向光源并返回，其颜色与方向取自常量缓冲区的数组中index下标处
Light GetDirectionalLight(int index, Surface surfaceWS, ShadowData shadowData) {
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;
	// 构造光源阴影信息
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(index, shadowData);
	// 根据片元的强度
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData,surfaceWS);
	return light;
}

// 对每个片元，构造一个其他光源类型并返回
Light GetOtherLight(int index, Surface surfaceWS, ShadowData shadowData) {
	Light light;
	light.color = _OtherLightColors[index].rgb;
	float3 position = _OtherLightPositions[index].xyz;
	float3 ray = position - surfaceWS.position;
	light.direction = normalize(ray);
	float distanceSqr = max(dot(ray,ray), 0.00001); // 光线的长度与0.00001取最大值
	// 范围内衰减
	float rangeAttenuation = Square(
		saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w))
	);
	float4 spotAngles = _OtherLightSpotAngles[index];
	// 聚光灯方向
	float3 spotDirection = _OtherLightDirections[index].xyz;
	// 聚光灯衰减
	float spotAttenuation = Square(saturate(dot(_OtherLightDirections[index].xyz, light.direction) * spotAngles.x + spotAngles.y));
	OtherShadowData otherShadowData = GetOtherShadowData(index);
	otherShadowData.lightPositionWS = position;
	otherShadowData.lightDirectionWS = light.direction;
	otherShadowData.spotDirectionWS = spotDirection;
	// 获取光源的阴影衰减值
	light.attenuation = GetOtherShadowAttenuation(otherShadowData, shadowData, surfaceWS) *
							spotAttenuation * rangeAttenuation / distanceSqr;
	return light;
}

#endif