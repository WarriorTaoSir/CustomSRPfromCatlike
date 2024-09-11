using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
	static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
	static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

	// ��Ⱦ������
	ScriptableRenderContext context;

	CommandBuffer buffer = new CommandBuffer();
	CullingResults cullingResults;

	// ��ǰӦ����Ⱦ�������
	Camera camera;
	Lighting lighting = new Lighting();

	// �������Ⱦ������Ⱦ�������ڵ�ǰ��Ⱦ�����ĵĻ�������Ⱦ��ǰ�����
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

		// ��FrameDebugger �� ��Shadows��ǩ��MainCamera��ǩ����
		buffer.BeginSample(SampleName);
		ExecuteBuffer();
        // 1.����Դ��Ϣ���ݸ�GPU��������Ҳ�������Ӱ��ͼ����Ⱦ
        lighting.Setup(context, cullingResults, shadowSettings);
        buffer.EndSample(SampleName);
        // 2.���õ�ǰ�����Render Target ׼����Ⱦ���������
        Setup();
        // ���ƿɼ��ļ������壬������պ�
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
		// ��ʼ���
		buffer.BeginSample(SampleName);
		ExecuteBuffer();
	}

	void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
	{	
		// ��Ⱦ��͸������
		// �����������˳�������������ǻ���������������
		var sortingSettings = new SortingSettings(camera){
			criteria = SortingCriteria.CommonOpaque
        };
		// ���������֧�ֵ�Shader Pass�ͻ���˳�������
		var drawingSettings = new DrawingSettings(
			unlitShaderTagId, sortingSettings
		) {
			// ���ö�̬������
			enableDynamicBatching = useDynamicBatching,
			enableInstancing = useGPUInstancing
		};
		// ���Ӷ�Lit.shader�Ļ���֧�֣�index��ʾ����DrawRenderer�и�pass�Ļ������ȼ���
		drawingSettings.SetShaderPassName(1, litShaderTagId);
		// ����������ЩVisible Objects�����ã�����֧�ֵ�RenderQueue��
		var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
		// ��ȾCullingResults�ڵĲ�͸����Visible Objects
		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);

		// ��Ⱦ��պ�
		context.DrawSkybox(camera);

		// ��Ⱦ͸������
		// ���û���˳��Ϊ�Ӻ���ǰ
		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		drawingSettings.sortingSettings = sortingSettings;
		// ���˳�͸��������
		filteringSettings.renderQueueRange = RenderQueueRange.transparent;
		// ����͸������
		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);
	}

	void Submit()
	{	
		// �������
		buffer.EndSample(SampleName);
		ExecuteBuffer();
		// �ύ��ǰ�������л����ָ�����
		context.Submit();
	}

	void ExecuteBuffer()
	{
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	bool Cull(float maxShadowDistance)
	{	
		// ��ȡ����������޳��Ĳ���
		if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
		{	
			// ʵ��shadowDistance ȡ maxShadowDistance ��camera.farClipPlane�н�Сֵ
			p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
			cullingResults = context.Cull(ref p);
			return true;
		}
		return false;
	}
}