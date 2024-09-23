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

// ����һ����Դ��ShadowData
DirectionalShadowData GetDirectionalShadowData (int lightIndex, ShadowData shadowData) {
	DirectionalShadowData data;
	// ��Ӱǿ��
	data.strength = _DirectionalLightShadowData[lightIndex].x;
	// Tile����
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
	data.normalBias = _DirectionalLightShadowData[lightIndex].z;
	data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w; // ���ĸ�����ʱ�ù�Դ��Ӧ��ShadowMaskͨ��
	return data;
}

// ����ÿ��ƬԪ������һ�������Դ�����أ�����ɫ�뷽��ȡ�Գ�����������������index�±괦
Light GetDirectionalLight(int index, Surface surfaceWS, ShadowData shadowData) {
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;
	// �����Դ��Ӱ��Ϣ
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(index, shadowData);
	// ����ƬԪ��ǿ��
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData,surfaceWS);
	return light;
}



#endif