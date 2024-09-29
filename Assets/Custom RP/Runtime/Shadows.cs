using UnityEngine;
using UnityEngine.Rendering;

// ����Shadow Map����߼������ϼ�ΪLighting��
public class Shadows
{
    const string bufferName = "Shadows";
    const int maxShadowedDirectionalLightCount = 4, maxShadowedOtherLightCount = 16;
    const int maxCascades = 4;

    // Directional���˹ؼ���
    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };

    // Other���˹ؼ���
    static string[] otherFilterKeywords = {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };

    // ������Ϲؼ���
    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    // ��Ӱ���ֹؼ���
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
    static Matrix4x4[] otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount]; // ������Դ�޼���
    // �洢ÿһ�����Ĳü���
    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];
    static Vector4[] otherShadowTiles = new Vector4[maxShadowedOtherLightCount];
    static Vector4[] cascadeData = new Vector4[maxCascades];
    int ShadowedDirectionalLightCount, ShadowedOtherLightCount;
    // �Ƿ�ʹ����Ӱ����
    bool useShadowMask;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    CullingResults cullingResults;

    // �洢��Ӱ�Ĳ���
    ShadowSettings settings;

    // XY ���������Directional�� ZW�����Other
    Vector4 atlasSizes;

    // ���ڻ�ȡ��ǰ֧����Ӱ�� �����Դ ��һЩ��Ϣ
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
    // ���ڻ�ȡ��ǰ֧����Ӱ�� ������Դ ��һЩ��Ϣ
    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool isPoint;
    }

    // ������洢�����Դ��Ӱ����
    ShadowedDirectionalLight[] ShadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    // ������洢������Դ��Ӱ����
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
    //ÿִ֡�У�����Ϊlight����shadow altas��shadowMap����Ԥ��һƬ�ռ�����Ⱦ��Ӱ��ͼ��ͬʱ�洢һЩ������Ҫ��Ϣ
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex) 
    {   
        // ���ù�Դ�����������ֵ
        // ֻ���ÿ�����Ӱ����Ӱǿ�ȴ���0�Ĺ�Դ
        // ���Բ���Ҫ��Ⱦ�κ���Ӱ�Ĺ�Դ��ͨ�� cullingResults.GetShadowCasterBounds������
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f )
        {
            float maskChannel = -1; // �ù�Դ��Ӧ��ShadowMaskͨ��
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

    // ÿִ֡�У���ǰ��Դ����shadowmask��Ϣ
    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {   
        // �����Ͷ����Ӱ���߸ù�Դ��Ӱǿ��С�ڵ���0
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
            return new Vector4(0f, 0f, 0f, -1f);

        // �決����Ӱ���ֳ�ʼ��
        float maskChannel = -1f;
        LightBakingOutput lightBaking = light.bakingOutput;
        if (// ��Դ��Mixed���� shadow mask
            lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
        )
        {
            useShadowMask = true;                            // ������Ӱ����
            maskChannel = lightBaking.occlusionMaskChannel;  // ��ȡ��Ӧ��maskChannel
        }

        // �ж��Ƿ��ǵ��Դ
        bool isPoint = light.type == LightType.Point;
        int newLightCount = ShadowedOtherLightCount + (isPoint ? 6 : 1);

        // �����Ӱ��Դ�����Ѵﵽ�����ֵ�����߸ù�Դ���Ǵ�û�пɲ�����Ӱ������
        if (newLightCount >= maxShadowedOtherLightCount || !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        // �����鸳ֵ
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
        // ��atlas��Ⱦ�������Ӱ
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

        // ��atlas��Ⱦ��������Ӱ
        if(ShadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }

        // ��Ӱ���ֲ�����ʵʱ�ģ����Լ�ʹ����û����Ⱦ�κ�ʵʱ��Ӱ����Ҫ��������
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

    // ���������Ӱ��Ⱦ��shadowmap��
    void RenderDirectionalShadows() 
    {
        int atlasSize = (int)settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;

        buffer.GetTemporaryRT(
            dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
        );
        //����GPU������������RT��ShadowAtlas
        //RenderBufferLoadAction.DontCare��ζ���ڽ�������ΪRenderTarget֮�����ǲ��������ĳ�ʼ״̬������������κ�Ԥ����
        //RenderBufferStoreAction.Store��ζ���������RT�ϵ�������Ⱦָ��֮��Ҫ�л�Ϊ��һ��RenderTargetʱ�������ǻὫ��洢���Դ���Ϊ��������ʹ��
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        //����ShadowAtlas��DepthBuffer�����ǵ�ShadowAtlasҲֻ��32bits��DepthBuffer��,��һ�β���true��ʾ���DepthBuffer���ڶ���false��ʾ�����ColorBuffer
        buffer.ClearRenderTarget(true, false, Color.clear);
        // ������Ӱƽ׹
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

    // ��������Դ��Ӱ��Ⱦ��shadowmap��
    void RenderOtherShadows()
    {
        int atlasSize = (int)settings.other.atlasSize;
        // ���� altas �ĳߴ�
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;

        buffer.GetTemporaryRT(
            otherShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
        );
        //����GPU������������RT��ShadowAtlas
        buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        //����ShadowAtlas��DepthBuffer�����ǵ�ShadowAtlasҲֻ��32bits��DepthBuffer��,��һ�β���true��ʾ���DepthBuffer���ڶ���false��ʾ�����ColorBuffer
        buffer.ClearRenderTarget(true, false, Color.clear);
        //������Ӱƽ׹
        buffer.SetGlobalFloat(shadowPancakingId, 0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = ShadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedOtherLightCount;)
        {   
            // ���other��Դ�ǵ��Դ
            if (shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else // ������Ǿ۹��
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
        // ��ȡ��ǰҪ���õĹ�Դ����Ϣ
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        // ����culling Result�͵�ǰ��Դ������������һ��ShadowDrawingSettings
        var shadowSettings = new ShadowDrawingSettings(
            cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Orthographic
        );
        // ������Ӱ����
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);

        float tileScale = 1f / split;

        for (int i = 0; i < cascadeCount; i++)
        {
            // ʹ��Unity�ṩ�Ľӿ���Ϊ�����Դ���������Ⱦ��Ӱ��ͼ�õ�VP�����splitData
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            //splitData ����Ͷ����Ӱ����Ӧ����α��ü�����Ϣ��������Ҫ�������ݸ�ShadowSettings
            shadowSettings.splitData = splitData;
            // ���еƵļ������ǵ�Ч�ģ�����ֻ��Ҫ�Ե�һ������ô��
            if (index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }

            // ���㵱ǰ��Ӱ��VP��������ֻ��Ҫ���ƹ����ӰͶ���������ͼ������ˣ��Ϳ��Դ���������ռ䵽�ƹ�ռ��ת������
            // Ȼ���������[-1,1]���ŵ�[0,1]��Ȼ�����Tileƫ�ƺ����ŵ���Ӧ��Դ��tile�ϣ��Ϳ��Խ��в����ˡ�
            int tileIndex = tileOffset + i;
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,
                SetTileViewport(tileIndex, split, tileSize), tileScale
            );
            // ����ǰVP��������Ϊ�������VP����׼����Ⱦ��Ӱ��ͼ��
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            // ʹ��context.DrawShadows����Ⱦ��Ӱ��ͼ������Ҫ����һ��shadowSettings
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
        
    }
    // cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives����
    // ���ã����㷽������ͼ��ͶӰ�����Լ���Ӱ�ָ�����
    // 1��int activeLightIndex ��ǰҪ����Ĺ�Դ����
    // 2��int splitIndex ������������Ӱ������أ���ʱ������
    // 3��int splitCount ������������
    // 4��Vector3 splitRatio ��������
    // 5��int shadowResoluition ��Ӱ��ͼ�ֱ���
    // 6��float shadowNearPlaneOffset ��Դ�Ľ�ƽ��ƫ��

    // ���۹�Ʋ�������Ӱ��Ⱦ��shadow map��
    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(
            cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Perspective  // ͸��ͶӰ
        );
        // �жϵ�ǰ��Χ�Ƿ���Ͷ����Ӱ������
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
        );
        shadowSettings.splitData = splitData;
        // ���㷨��ƫ��
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

    // ��Ⱦ���Դ����Ӱ
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
        // Ҫռ6��tile Ŷ
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
    // ���õ�ǰҪ��Ⱦ��Tile����
    // </summary>
    // <param name="index">Tile����</param>
    // <param name="split">Tileһ�������ϵ�����</param>
    // <param name="tileSize">һ��Tile�Ŀ�ȣ��߶ȣ�</param>
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
