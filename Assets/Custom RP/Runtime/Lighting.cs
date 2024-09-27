using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{

	const string bufferName = "Lighting";
	const int maxDirLightCount = 4, maxOtherLightCount = 64;

    // 方向光源shader属性ID
    static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
	static int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
	static int dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
    static int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

	// 方向光源数据数组
	static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static Vector4[] dirLightDirections = new Vector4[maxDirLightCount];
    static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];


	// 其他类型光源shader属性ID
	static int otherLightCountId = Shader.PropertyToID("_OtherLightCount");
	static int otherLightColorsId = Shader.PropertyToID("_OtherLightColors");
    static int otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");
	static int otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections");
	static int otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
	static int otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

	// 其他类型光源数据数组
	static Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightDirections = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightShadowData = new Vector4[maxOtherLightCount];

    static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";
    CullingResults cullingResults;
	
	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};
    Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings, bool useLightsPerObject)
	{	
		this.cullingResults = cullingResults;
		buffer.BeginSample(bufferName);
		shadows.Setup(context, cullingResults, shadowSettings);
        SetupLights(useLightsPerObject);
        shadows.Render();
        buffer.EndSample(bufferName);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	void SetupLights(bool useLightsPerObject) {

		// 包含光源index
        NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;
        // 所有可见光源
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;       
		int dirLightCount = 0, otherLightCount = 0;
        int i;
		for (i = 0; i < visibleLights.Length; i++)
		{
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];

			// 根据光源类型为每种光源的属性赋值
            switch (visibleLight.lightType)
            {
                case LightType.Directional: // 如果是方向光，那就设置方向光
                    if (dirLightCount < maxDirLightCount)
                    {
                        SetupDirectionalLight(dirLightCount++, ref visibleLight);
                    }
                    break;
                case LightType.Point: // 如果是点光源，那就设置点光源
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupPointLight(otherLightCount++, ref visibleLight);
                    }
                    break;
				case LightType.Spot:
					if(otherLightCount < maxOtherLightCount)
					{
                        newIndex = otherLightCount;
                        SetupSpotLight(otherLightCount++, ref visibleLight);
					}
					break; 
            }
            if (useLightsPerObject)
                indexMap[i] = newIndex;
        }

        // 剩下的光源的下标全部清理掉
        // 这样一来，只有非方向光的下标对应的是otherLightCount，其他的都是-1
        if (useLightsPerObject)
        {
            for (; i < indexMap.Length; i++)
                indexMap[i] = -1;

            cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            Shader.EnableKeyword(lightsPerObjectKeyword);
        }else{
            Shader.DisableKeyword(lightsPerObjectKeyword);
        }

        // 将赋好值的变量传递给shader property
        buffer.SetGlobalInt(dirLightCountId, visibleLights.Length);
		if(dirLightCount > 0)
		{
            buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
            buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
        }

		buffer.SetGlobalInt(otherLightCountId, otherLightCount);
		if(otherLightCount > 0)
		{
            buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
            buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
        }

    }

	// 创建方向光
	void SetupDirectionalLight(int index, ref VisibleLight visibleLight) {
		dirLightColors[index] = visibleLight.finalColor;
		dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);
	}

	// 创建点光源
	void SetupPointLight(int index, ref VisibleLight visibleLight)
	{
		otherLightColors[index] = visibleLight.finalColor;
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
        otherLightSpotAngles[index] = new Vector4(0f, 1f);
		// 配置shadow mask
        Light light = visibleLight.light;
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, index);
    }

	// 创建聚光灯
	void SetupSpotLight(int index, ref VisibleLight visibleLight)
	{
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

        Light light = visibleLight.light;
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        // 配置shadow mask
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, index);
    }


    public void Cleanup()
    {
        shadows.Cleanup();
    }

}