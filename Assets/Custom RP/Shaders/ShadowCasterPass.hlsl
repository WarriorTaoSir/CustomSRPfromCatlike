#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED

//使用结构体定义顶点着色器的输入，一个是为了代码更整洁，一个是为了支持GPU Instancing（获取object的index）
struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//为了在片元着色器中获取实例ID，给顶点着色器的输出（即片元着色器的输入）也定义一个结构体
//命名为Varyings是因为它包含的数据可以在同一三角形的片段之间变化
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

bool _ShadowPancaking;

Varyings ShadowCasterPassVertex(Attributes input)
{
    Varyings output;
    //从input中提取实例的ID并将其存储在其他实例化宏所依赖的全局静态变量中
    UNITY_SETUP_INSTANCE_ID(input);
    //将实例ID传递给output
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);

    // 如果启用了阴影平坠，则将阴影三角面的顶点clamp进视椎体
    if(_ShadowPancaking){
        #if UNITY_REVERSED_Z
		    output.positionCS.z =
			    min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
	    #else
		    output.positionCS.z =
			    max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
	    #endif
    }
    //应用纹理ST变换s
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

void ShadowCasterPassFragment(Varyings input)
{
    //从input中提取实例的ID并将其存储在其他实例化宏所依赖的全局静态变量中
    UNITY_SETUP_INSTANCE_ID(input);
    // 进行LOD裁剪
    ClipLOD(input.positionCS.xy, unity_LODFade.x);
    
    InputConfig config = GetInputConfig(input.baseUV);
    //获取采样纹理颜色
    float4 base = GetBase(config);
    //只有在_CLIPPING关键字启用时编译该段代码
    #if defined(_SHADOWS_CLIP)
        //clip函数的传入参数如果<=0则会丢弃该片元
        clip(base.a - GetCutoff(config));
    #elif defined(_SHADOWS_DITHER)
		float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
		clip(base.a - dither);
    #endif
    //到这里就结束了，我们不需要返回任何值，其片元深度会写入阴影贴图的DepthBuffer
}

#endif