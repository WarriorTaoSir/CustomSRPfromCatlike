#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface {
	float3 position;		// 平面世界位置
	float3 normal;			// 法线
	float3 viewDirection;	// 视线方向
	float depth;			// 平面深度
	float3 color;			// 本身颜色
	float alpha;			// 不透明度值
    float metallic;			// 金属度
    float smoothness;		// 粗糙度
	float dither;           // 阴影抖动值
};

#endif