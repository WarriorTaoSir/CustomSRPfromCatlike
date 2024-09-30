using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{

    [SerializeField] bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true, useLightsPerObject = true;

    [SerializeField] ShadowSettings shadows = default;

    [SerializeField] PostFXSettings postFXSettings = default;
    // 重写创建实际RenderPipeline 的函数
    protected override RenderPipeline CreatePipeline()
    {   
        // 设置好该渲染管线的各项参数，然后返回渲染管线的实例化对象
        return new CustomRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBatcher, useLightsPerObject,shadows, postFXSettings);
    }
}
