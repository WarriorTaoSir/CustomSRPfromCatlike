#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 IncomingLight(Surface surface, Light light) {
	return saturate(dot(surface.normal, light.direction) * light.attenuation) * light.color;
}

float3 GetLighting(Surface surface, BRDF brdf, Light light) {
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Surface surfaceWS, BRDF brdf, GI gi) {
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	// 首先将片元颜色加上采样得来的间接光照（漫反射间接光 + 镜面反射间接光）
	float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);

	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		// 每个光源对该片元产生的直接光照
		Light light  = GetDirectionalLight(i, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}

	// 最多8个光源
	#if defined(_LIGHTS_PER_OBJECT)
		for (int j = 0; j < min(unity_LightData.y, 8); j++) {
			int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
			Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
			color += GetLighting(surfaceWS, brdf, light);
		}
	#else
		for (int j = 0; j < GetOtherLightCount(); j++)
		{
			Light light = GetOtherLight(j, surfaceWS, shadowData);
			color += GetLighting(surfaceWS, brdf, light);
		}
	#endif
	return color;
}

#endif