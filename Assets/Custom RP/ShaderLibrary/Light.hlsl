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
DirectionalShadowData GetDirectionalShadowData (int lightIndex) {
	DirectionalShadowData data;
	// ��Ӱǿ��
	data.strength = _DirectionalLightShadowData[lightIndex].x;
	// Tile����
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y;
	return data;
}

// ����ÿ��ƬԪ������һ�������Դ�����أ�����ɫ�뷽��ȡ�Գ�����������������index�±괦
Light GetDirectionalLight(int index, Surface surfaceWS) {
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;
	// �����Դ��Ӱ��Ϣ
	DirectionalShadowData shadowData = GetDirectionalShadowData(index);
	// ����ƬԪ��ǿ��
	light.attenuation = GetDirectionalShadowAttenuation(shadowData, surfaceWS);
	return light;
}



#endif