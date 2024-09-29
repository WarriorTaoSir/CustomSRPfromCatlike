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
	float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];    // ������Դ��ɫ����
	float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT]; // �������͹�Դλ�ã�w������1/r��
	float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];// �������ͷ���
	float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];// �������ͽǶ�
	float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];// ������Դ����Ӱ����
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

// ����һ���������͹�Դ��shadowData
OtherShadowData GetOtherShadowData (int lightIndex) {
	OtherShadowData data;
	data.strength = _OtherLightShadowData[lightIndex].x;
	data.tileIndex = _OtherLightShadowData[lightIndex].y;
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	data.isPoint = _OtherLightShadowData[lightIndex].z == 1.0;
	// ��ʼ��λ����Ϣ
	data.lightPositionWS = 0.0;
	data.lightDirectionWS = 0.0;
	data.spotDirectionWS = 0.0;
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

// ��ÿ��ƬԪ������һ��������Դ���Ͳ�����
Light GetOtherLight(int index, Surface surfaceWS, ShadowData shadowData) {
	Light light;
	light.color = _OtherLightColors[index].rgb;
	float3 position = _OtherLightPositions[index].xyz;
	float3 ray = position - surfaceWS.position;
	light.direction = normalize(ray);
	float distanceSqr = max(dot(ray,ray), 0.00001); // ���ߵĳ�����0.00001ȡ���ֵ
	// ��Χ��˥��
	float rangeAttenuation = Square(
		saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w))
	);
	float4 spotAngles = _OtherLightSpotAngles[index];
	// �۹�Ʒ���
	float3 spotDirection = _OtherLightDirections[index].xyz;
	// �۹��˥��
	float spotAttenuation = Square(saturate(dot(_OtherLightDirections[index].xyz, light.direction) * spotAngles.x + spotAngles.y));
	OtherShadowData otherShadowData = GetOtherShadowData(index);
	otherShadowData.lightPositionWS = position;
	otherShadowData.lightDirectionWS = light.direction;
	otherShadowData.spotDirectionWS = spotDirection;
	// ��ȡ��Դ����Ӱ˥��ֵ
	light.attenuation = GetOtherShadowAttenuation(otherShadowData, shadowData, surfaceWS) *
							spotAttenuation * rangeAttenuation / distanceSqr;
	return light;
}

#endif