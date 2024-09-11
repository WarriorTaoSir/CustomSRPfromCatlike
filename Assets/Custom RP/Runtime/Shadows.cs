using UnityEngine;
using UnityEngine.Rendering;

// ����Shadow Map����߼������ϼ�ΪLighting��
public class Shadows
{
    const string bufferName = "Shadows";
    const int maxShadowedDirectionalLightCount = 4;

    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");

    int ShadowedDirectionalLightCount;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    CullingResults cullingResults;

    ShadowSettings settings;
    // ���ڻ�ȡ��ǰ֧����Ӱ�� �����Դ ��һЩ��Ϣ
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
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
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    // ÿִ֡�У�����light����shadow atlas��Ԥ��һƬ�ռ�����Ⱦ��Ӱ��ͼ
    public void ReserveDirectionalShadows(Light light, int visibleLightIndex) 
    {   
        // ���ù�Դ�����������ֵ
        // ֻ���ÿ�����Ӱ����Ӱǿ�ȴ���0�Ĺ�Դ
        // ���Բ���Ҫ��Ⱦ�κ���Ӱ�Ĺ�Դ��ͨ�� cullingResults.GetShadowCasterBounds������
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f &&
            cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            ShadowedDirectionalLights[ShadowedDirectionalLightCount++] = new ShadowedDirectionalLight
            {
                visibleLightIndex = visibleLightIndex
            };
        }
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
    }

    void RenderDirectionalShadows() 
    {
        int atlasSize = (int)settings.directional.atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize);
        //����GPU������������RT��ShadowAtlas
        //RenderBufferLoadAction.DontCare��ζ���ڽ�������ΪRenderTarget֮�����ǲ��������ĳ�ʼ״̬������������κ�Ԥ����
        //RenderBufferStoreAction.Store��ζ���������RT�ϵ�������Ⱦָ��֮��Ҫ�л�Ϊ��һ��RenderTargetʱ�������ǻὫ��洢���Դ���Ϊ��������ʹ��
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        //����ShadowAtlas��DepthBuffer�����ǵ�ShadowAtlasҲֻ��32bits��DepthBuffer��,��һ�β���true��ʾ���DepthBuffer���ڶ���false��ʾ�����ColorBuffer
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int split = ShadowedDirectionalLightCount <= 1 ? 1 : 2;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows(int index, int split,  int tileSize)
    {   
        // ��ȡ��ǰҪ���õĹ�Դ����Ϣ
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        // ����culling Result�͵�ǰ��Դ������������һ��ShadowDrawingSettings
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        // ʹ��Unity�ṩ�Ľӿ���Ϊ�����Դ���������Ⱦ��Ӱ��ͼ�õ�VP�����splitData
        cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex,
            0, 1, Vector3.zero, tileSize, 0f,
            out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
        //splitData ����Ͷ����Ӱ����Ӧ����α��ü�����Ϣ��������Ҫ�������ݸ�ShadowSettings
        shadowSettings.splitData = splitData;
        SetTileViewport(index, split, tileSize);
        // ����ǰVP��������Ϊ�������VP����׼����Ⱦ��Ӱ��ͼ��
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        ExecuteBuffer();
        // ʹ��context.DrawShadows����Ⱦ��Ӱ��ͼ������Ҫ����һ��shadowSettings
        context.DrawShadows(ref shadowSettings);
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
    void SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
    }



}
