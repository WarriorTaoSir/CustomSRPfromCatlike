using UnityEngine;
using UnityEngine.Rendering;

// 所有Shadow Map相关逻辑，其上级为Lighting类
public class Shadows
{
    const string bufferName = "Shadows";
    const int maxShadowedDirectionalLightCount = 4, maxShadowedOtherLightCount = 16;
    const int maxCascades = 4;

    // Directional过滤关键字
    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };

    // Other过滤关键字
    static string[] otherFilterKeywords = {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };

    // 级联混合关键字
    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    // 阴影遮罩关键字
    static string[] shadowMaskKeywords =
    {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };

    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    static int otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas");
    static int otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices");
    static int otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles");
    static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    static int cascadeDataId = Shader.PropertyToID("_CascadeData");
    static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
    static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
    static int shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

    static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];
    static Matrix4x4[] otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount]; // 其它光源无级联
    // 存储每一级联的裁剪球
    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];
    static Vector4[] otherShadowTiles = new Vector4[maxShadowedOtherLightCount];
    static Vector4[] cascadeData = new Vector4[maxCascades];
    int ShadowedDirectionalLightCount, ShadowedOtherLightCount;
    // 是否使用阴影遮罩
    bool useShadowMask;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    CullingResults cullingResults;

    // 存储阴影的参数
    ShadowSettings settings;

    // XY 分量存的是Directional， ZW存的是Other
    Vector4 atlasSizes;

    // 用于获取当前支持阴影的 方向光源 的一些信息
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
    // 用于获取当前支持阴影的 其它光源 的一些信息
    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool isPoint;
    }

    // 用数组存储方向光源阴影数据
    ShadowedDirectionalLight[] ShadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    // 用数组存储其它光源阴影数据
    ShadowedOtherLight[] shadowedOtherLights =
        new ShadowedOtherLight[maxShadowedOtherLightCount];
    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults,
        ShadowSettings settings)
    {   
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        ShadowedDirectionalLightCount = ShadowedOtherLightCount = 0;
        useShadowMask = false;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    //每帧执行，用于为light配置shadow altas（shadowMap）上预留一片空间来渲染阴影贴图，同时存储一些其他必要信息
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex) 
    {   
        // 配置光源数不超过最大值
        // 只配置开启阴影且阴影强度大于0的光源
        // 忽略不需要渲染任何阴影的光源（通过 cullingResults.GetShadowCasterBounds方法）
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f )
        {
            float maskChannel = -1; // 该光源对应的ShadowMask通道
            LightBakingOutput lightBaking = light.bakingOutput;
            if (
                lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
            )
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }


            if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                return new Vector4(light.shadowStrength, 0f, 0f, maskChannel);
            }

            ShadowedDirectionalLights[ShadowedDirectionalLightCount] = new ShadowedDirectionalLight
            {
                visibleLightIndex = visibleLightIndex,
                slopeScaleBias = light.shadowBias,
                nearPlaneOffset = light.shadowNearPlane
            };
            return new Vector4(light.shadowStrength, settings.directional.cascadeCount * ShadowedDirectionalLightCount++, light.shadowNormalBias, maskChannel);
        }
        return new Vector4(0f, 0f, 0f, -1f);
    }

    // 每帧执行，当前光源配置shadowmask信息
    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {   
        // 如果不投射阴影或者该光源阴影强度小于等于0
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
            return new Vector4(0f, 0f, 0f, -1f);

        // 烘焙的阴影遮罩初始化
        float maskChannel = -1f;
        LightBakingOutput lightBaking = light.bakingOutput;
        if (// 光源是Mixed并且 shadow mask
            lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
        )
        {
            useShadowMask = true;                            // 启用阴影遮罩
            maskChannel = lightBaking.occlusionMaskChannel;  // 获取对应的maskChannel
        }

        // 判断是否是点光源
        bool isPoint = light.type == LightType.Point;
        int newLightCount = ShadowedOtherLightCount + (isPoint ? 6 : 1);

        // 如果阴影光源数量已达到了最大值，或者该光源覆盖处没有可产生阴影的物体
        if (newLightCount >= maxShadowedOtherLightCount || !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        // 给数组赋值
        shadowedOtherLights[ShadowedOtherLightCount] = new ShadowedOtherLight {
			visibleLightIndex = visibleLightIndex,
			slopeScaleBias = light.shadowBias,
			normalBias = light.shadowNormalBias,
            isPoint = isPoint
		};

        Vector4 data = new Vector4(
            light.shadowStrength, ShadowedOtherLightCount,
            isPoint ? 1f : 0f, maskChannel
        );
        ShadowedOtherLightCount = newLightCount;
        return data;
    }

    public void Render()
    {   
        // 向atlas渲染方向光阴影
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            buffer.GetTemporaryRT(
                dirShadowAtlasId, 1, 1,
                32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }

        // 向atlas渲染其它光阴影
        if(ShadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }

        // 阴影遮罩并不是实时的，所以即使最终没有渲染任何实时阴影，都要这样做。
        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);

        buffer.SetGlobalInt(cascadeCountId, ShadowedDirectionalLightCount > 0 ? settings.directional.cascadeCount : 0);

        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(
            shadowDistanceFadeId, new Vector4(
                1f / settings.maxDistance, 1f / settings.distanceFade,
                1f / (1f - f * f)
            )
        );
        buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    // 将方向光阴影渲染到shadowmap上
    void RenderDirectionalShadows() 
    {
        int atlasSize = (int)settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;

        buffer.GetTemporaryRT(
            dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
        );
        //告诉GPU接下来操作的RT是ShadowAtlas
        //RenderBufferLoadAction.DontCare意味着在将其设置为RenderTarget之后，我们不关心它的初始状态，不对其进行任何预处理
        //RenderBufferStoreAction.Store意味着完成这张RT上的所有渲染指令之后（要切换为下一个RenderTarget时），我们会将其存储到显存中为后续采样使用
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        //清理ShadowAtlas的DepthBuffer（我们的ShadowAtlas也只有32bits的DepthBuffer）,第一次参数true表示清除DepthBuffer，第二个false表示不清除ColorBuffer
        buffer.ClearRenderTarget(true, false, Color.clear);
        // 启用阴影平坠
        buffer.SetGlobalFloat(shadowPancakingId, 1f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }
        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(
            cascadeCullingSpheresId, cascadeCullingSpheres
        );
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(
            shadowDistanceFadeId, new Vector4(
                1f / settings.maxDistance, 1f / settings.distanceFade,
                1f / (1f - f * f)
                )
        );
        
        SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    // 将其它光源阴影渲染到shadowmap上
    void RenderOtherShadows()
    {
        int atlasSize = (int)settings.other.atlasSize;
        // 设置 altas 的尺寸
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;

        buffer.GetTemporaryRT(
            otherShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
        );
        //告诉GPU接下来操作的RT是ShadowAtlas
        buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        //清理ShadowAtlas的DepthBuffer（我们的ShadowAtlas也只有32bits的DepthBuffer）,第一次参数true表示清除DepthBuffer，第二个false表示不清除ColorBuffer
        buffer.ClearRenderTarget(true, false, Color.clear);
        //禁用阴影平坠
        buffer.SetGlobalFloat(shadowPancakingId, 0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = ShadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedOtherLightCount;)
        {   
            // 如果other光源是点光源
            if (shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else // 否则就是聚光灯
            {
                RenderSpotShadows(i, split, tileSize);
                i += 1;
            }
        }

        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
        SetKeywords(otherFilterKeywords, (int)settings.other.filter - 1);

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for(int i = 0; i < keywords.Length; i++)
        {
            if(i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        // 获取当前要配置的光源的信息
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        // 根据culling Result和当前光源的索引来构造一个ShadowDrawingSettings
        var shadowSettings = new ShadowDrawingSettings(
            cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Orthographic
        );
        // 级联阴影设置
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);

        float tileScale = 1f / split;

        for (int i = 0; i < cascadeCount; i++)
        {
            // 使用Unity提供的接口来为方向光源计算出其渲染阴影贴图用的VP矩阵和splitData
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            //splitData 包括投射阴影物体应该如何被裁剪的信息，我们需要把它传递给ShadowSettings
            shadowSettings.splitData = splitData;
            // 所有灯的级联都是等效的，所以只需要对第一个灯这么做
            if (index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }

            // 计算当前阴影的VP矩阵，这样只需要将灯光的阴影投射矩阵与视图矩阵相乘，就可以创建从世界空间到灯光空间的转换矩阵
            // 然后将其坐标从[-1,1]缩放到[0,1]，然后根据Tile偏移和缩放到对应光源的tile上，就可以进行采样了。
            int tileIndex = tileOffset + i;
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,
                SetTileViewport(tileIndex, split, tileSize), tileScale
            );
            // 将当前VP矩阵设置为计算出的VP矩阵，准备渲染阴影贴图。
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            // 使用context.DrawShadows来渲染阴影贴图，其需要传入一个shadowSettings
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
        
    }
    // cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives方法
    // 作用：计算方向光的视图与投影矩阵以及阴影分割数据
    // 1、int activeLightIndex 当前要计算的光源索引
    // 2、int splitIndex 级联索引，阴影级联相关，暂时不深入
    // 3、int splitCount 级联的数量，
    // 4、Vector3 splitRatio 级联比率
    // 5、int shadowResoluition 阴影贴图分辨率
    // 6、float shadowNearPlaneOffset 光源的近平面偏移

    // 将聚光灯产生的阴影渲染到shadow map上
    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(
            cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Perspective  // 透视投影
        );
        // 判断当前范围是否有投射阴影的物体
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
        );
        shadowSettings.splitData = splitData;
        // 计算法线偏移
        float texelSize = 2f / (tileSize * projectionMatrix.m00);
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        Vector2 offset = SetTileViewport(index, split, tileSize);

        float tileScale = 1f / split;
        SetOtherTileData(index, offset, tileScale, bias);
        otherShadowMatrices[index] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }

    // 渲染点光源的阴影
    void RenderPointShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(
            cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Perspective
        );

        float texelSize = 2f / tileSize;
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float tileScale = 1f / split;

        float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        // 要占6个tile 哦
        for (int i = 0; i < 6; i++)
        {
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, (CubemapFace)i, fovBias,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;

            shadowSettings.splitData = splitData;
            int tileIndex = index + i;

            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);

            SetOtherTileData(tileIndex, offset, tileScale, bias);
            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix, offset, tileScale
            );

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
    {
        float border = atlasSizes.w * 0.5f;
        Vector4 data;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        otherShadowTiles[index] = data;
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        if(ShadowedOtherLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        }
        ExecuteBuffer();
    }

    // <summary>
    // 设置当前要渲染的Tile区域
    // </summary>
    // <param name="index">Tile索引</param>
    // <param name="split">Tile一个方向上的总数</param>
    // <param name="tileSize">一个Tile的宽度（高度）</param>
    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
        ));
        return offset;
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }

    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)settings.directional.filter + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(
            1f / cullingSphere.w,
            filterSize * 1.4142136f
        );
    }
}
