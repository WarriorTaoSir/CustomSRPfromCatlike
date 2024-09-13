#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4

// ����TEXTURE2D_SHADOW����ר�Ų�����Ӱ��ͼ
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
// ����SAMPLER_CMP�����������״̬��һ�����Թ��˲��������������
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);


CBUFFER_START(_CustomShadows)
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

#endif