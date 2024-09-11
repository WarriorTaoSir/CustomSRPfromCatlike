using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
	static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
	static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

	// 渲染上下文
	ScriptableRenderContext context;

	CommandBuffer buffer = new CommandBuffer();
	CullingResults cullingResults;

	// 当前应该渲染的摄像机
	Camera camera;
	Lighting lighting = new Lighting();

	// 摄像机渲染器的渲染函数，在当前渲染上下文的基础上渲染当前摄像机
	public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, ShadowSettings shadowSettings)
	{
		this.context = context;
		this.camera = camera;

		PrepareBuffer();
		PrepareForSceneWindow();
		if (!Cull(shadowSettings.maxDistance))
		{
			return;
		}

		// 在FrameDebugger 中 让Shadows标签被MainCamera标签囊括
		buffer.BeginSample(SampleName);
		ExecuteBuffer();
        // 1.将光源信息传递给GPU，在其中也会完成阴影贴图的渲染
        lighting.Setup(context, cullingResults, shadowSettings);
        buffer.EndSample(SampleName);
        // 2.设置当前摄像机Render Target 准备渲染摄像机画面
        Setup();
        // 绘制可见的几何物体，包括天空盒
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
		DrawUnsupportedShaders();
		DrawGizmos();
		lighting.Cleanup();
		Submit();
	}

	void Setup()
	{
		context.SetupCameraProperties(camera);
		CameraClearFlags flags = camera.clearFlags;
		buffer.ClearRenderTarget(
			flags <= CameraClearFlags.Depth,
			flags <= CameraClearFlags.Color,
			flags == CameraClearFlags.Color ?
				camera.backgroundColor.linear : Color.clear);
		// 开始监测
		buffer.BeginSample(SampleName);
		ExecuteBuffer();
	}

	void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
	{	
		// 渲染不透明物体
		// 决定物体绘制顺序是正交排序还是基于深度排序的配置
		var sortingSettings = new SortingSettings(camera){
			criteria = SortingCriteria.CommonOpaque
        };
		// 决定摄像机支持的Shader Pass和绘制顺序的配置
		var drawingSettings = new DrawingSettings(
			unlitShaderTagId, sortingSettings
		) {
			// 启用动态批处理
			enableDynamicBatching = useDynamicBatching,
			enableInstancing = useGPUInstancing
		};
		// 增加对Lit.shader的绘制支持，index表示本次DrawRenderer中该pass的绘制优先级。
		drawingSettings.SetShaderPassName(1, litShaderTagId);
		// 决定过滤哪些Visible Objects的设置，包括支持的RenderQueue等
		var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
		// 渲染CullingResults内的不透明的Visible Objects
		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);

		// 渲染天空盒
		context.DrawSkybox(camera);

		// 渲染透明物体
		// 设置绘制顺序为从后往前
		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		drawingSettings.sortingSettings = sortingSettings;
		// 过滤出透明的物体
		filteringSettings.renderQueueRange = RenderQueueRange.transparent;
		// 绘制透明物体
		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);
	}

	void Submit()
	{	
		// 结束监测
		buffer.EndSample(SampleName);
		ExecuteBuffer();
		// 提交当前上下文中缓存的指令队列
		context.Submit();
	}

	void ExecuteBuffer()
	{
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	bool Cull(float maxShadowDistance)
	{	
		// 获取摄像机用于剔除的参数
		if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
		{	
			// 实际shadowDistance 取 maxShadowDistance 和camera.farClipPlane中较小值
			p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
			cullingResults = context.Cull(ref p);
			return true;
		}
		return false;
	}
}