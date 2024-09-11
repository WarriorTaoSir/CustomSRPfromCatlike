using UnityEngine;
using UnityEngine.Rendering;

// 所有Shadow Map相关逻辑，其上级为Lighting类
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
    // 用于获取当前支持阴影的 方向光源 的一些信息
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
    }

    // 用数组存储
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
    // 每帧执行，用于light配置shadow atlas上预留一片空间来渲染阴影贴图
    public void ReserveDirectionalShadows(Light light, int visibleLightIndex) 
    {   
        // 配置光源数不超过最大值
        // 只配置开启阴影且阴影强度大于0的光源
        // 忽略不需要渲染任何阴影的光源（通过 cullingResults.GetShadowCasterBounds方法）
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
        //告诉GPU接下来操作的RT是ShadowAtlas
        //RenderBufferLoadAction.DontCare意味着在将其设置为RenderTarget之后，我们不关心它的初始状态，不对其进行任何预处理
        //RenderBufferStoreAction.Store意味着完成这张RT上的所有渲染指令之后（要切换为下一个RenderTarget时），我们会将其存储到显存中为后续采样使用
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        //清理ShadowAtlas的DepthBuffer（我们的ShadowAtlas也只有32bits的DepthBuffer）,第一次参数true表示清除DepthBuffer，第二个false表示不清除ColorBuffer
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
        // 获取当前要配置的光源的信息
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        // 根据culling Result和当前光源的索引来构造一个ShadowDrawingSettings
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        // 使用Unity提供的接口来为方向光源计算出其渲染阴影贴图用的VP矩阵和splitData
        cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex,
            0, 1, Vector3.zero, tileSize, 0f,
            out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
        //splitData 包括投射阴影物体应该如何被裁剪的信息，我们需要把它传递给ShadowSettings
        shadowSettings.splitData = splitData;
        SetTileViewport(index, split, tileSize);
        // 将当前VP矩阵设置为计算出的VP矩阵，准备渲染阴影贴图。
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        ExecuteBuffer();
        // 使用context.DrawShadows来渲染阴影贴图，其需要传入一个shadowSettings
        context.DrawShadows(ref shadowSettings);
    }
    // cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives方法
    // 作用：计算方向光的视图与投影矩阵以及阴影分割数据
    // 1、int activeLightIndex 当前要计算的光源索引
    // 2、int splitIndex 级联索引，阴影级联相关，暂时不深入
    // 3、int splitCount 级联的数量，
    // 4、Vector3 splitRatio 级联比率
    // 5、int shadowResoluition 阴影贴图分辨率
    // 6、float shadowNearPlaneOffset 光源的近平面偏移
     
    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }

    // <summary>
    // 设置当前要渲染的Tile区域
    // </summary>
    // <param name="index">Tile索引</param>
    // <param name="split">Tile一个方向上的总数</param>
    // <param name="tileSize">一个Tile的宽度（高度）</param>
    void SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
    }



}
