using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
    enum Pass
    {
        Copy
    }

    // 后处理贴图Id
    int fxSourceId = Shader.PropertyToID("_PostFXSource");

    const string bufferName = "Post FX";

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    Camera camera;

    PostFXSettings settings;

    // 用来判断这个特效栈是否启用
    public bool IsActive => settings != null;

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings)
    {
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        this.context = context;
        this.camera = camera;
        this.settings = settings;
        ApplySceneViewState();
    }

    public void Render(int sourceId)
    {
        Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget(
            to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.DrawProcedural(
            Matrix4x4.identity, settings.Material, (int)pass,
            MeshTopology.Triangles, 3
        );
    }
}