#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface {
	float3 position;		// ƽ������λ��
	float3 normal;			// ����
	float3 viewDirection;	// ���߷���
	float depth;			// ƽ�����
	float3 color;			// ������ɫ
	float alpha;			// ��͸����ֵ
    float metallic;			// ������
    float smoothness;		// �ֲڶ�
	float dither;           // ��Ӱ����ֵ
};

#endif