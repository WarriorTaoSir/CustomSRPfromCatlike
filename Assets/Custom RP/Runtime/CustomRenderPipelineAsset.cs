using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{

    [SerializeField] bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true;

    [SerializeField] ShadowSettings shadows = default;
    // ��д����ʵ��RenderPipeline �ĺ���
    protected override RenderPipeline CreatePipeline()
    {   
        // ���úø���Ⱦ���ߵĸ��������Ȼ�󷵻���Ⱦ���ߵ�ʵ��������
        return new CustomRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBatcher, shadows);
    }
}
