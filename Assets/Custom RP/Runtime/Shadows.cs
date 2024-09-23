using UnityEngine;
using UnityEngine.Rendering;

// ����Shadow Map����߼������ϼ�ΪLighting��
public class Shadows
{
    const string bufferName = "Shadows";
    const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;

    // ���˹ؼ���
    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
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
    static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    static int cascadeDataId = Shader.PropertyToID("_CascadeData");
    static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
    static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];
    // �洢ÿһ�����Ĳü���
    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];
    static Vector4[] cascadeData = new Vector4[maxCascades];
    int ShadowedDirectionalLightCount;
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

    // ���ڻ�ȡ��ǰ֧����Ӱ�� �����Դ ��һЩ��Ϣ
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    // ������洢
    ShadowedDirectionalLight[] ShadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults,
        ShadowSettings settings)
    {   
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        ShadowedDirectionalLightCount = 0;
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

    public void Render()
    {
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

        // ��Ӱ���ֲ�����ʵʱ�ģ����Լ�ʹ����û����Ⱦ�κ�ʵʱ��Ӱ����Ҫ��������
        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }
    void RenderDirectionalShadows() 
    {
        int atlasSize = (int)settings.directional.atlasSize;
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
        buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
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
                SetTileViewport(tileIndex, split, tileSize), split
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
    
    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
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

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        float scale = 1f / split;
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
