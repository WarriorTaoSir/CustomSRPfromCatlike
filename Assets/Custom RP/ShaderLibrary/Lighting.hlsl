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

    for (int j = 0; j < GetOtherLightCount(); j++)
    {
        Light light = GetOtherLight(j, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
    }
	return color;
}

#endif