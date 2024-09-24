#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface {
	float3 position;		// ƽ������λ��
	float3 normal;			// ����
	float3 interpolatedNormal; // ��ֵ��ķ���
	float3 viewDirection;	// ���߷���
	float depth;			// ƽ�����
	float3 color;			// ������ɫ
	float alpha;			// ��͸����ֵ
    float metallic;			// ������
	float occlusion;        // ���ڵ�
    float smoothness;		// �ֲڶ�
	float fresnelStrength;  // ������ǿ��
	float dither;           // ��Ӱ����ֵ
};

#endif