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

#endif