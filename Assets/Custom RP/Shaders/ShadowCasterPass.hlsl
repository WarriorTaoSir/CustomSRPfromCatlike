#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED

//ʹ�ýṹ�嶨�嶥����ɫ�������룬һ����Ϊ�˴�������࣬һ����Ϊ��֧��GPU Instancing����ȡobject��index��
struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//Ϊ����ƬԪ��ɫ���л�ȡʵ��ID����������ɫ�����������ƬԪ��ɫ�������룩Ҳ����һ���ṹ��
//����ΪVaryings����Ϊ�����������ݿ�����ͬһ�����ε�Ƭ��֮��仯
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ShadowCasterPassVertex(Attributes input)
{
    Varyings output;
    //��input����ȡʵ����ID������洢������ʵ��������������ȫ�־�̬������
    UNITY_SETUP_INSTANCE_ID(input);
    //��ʵ��ID���ݸ�output
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);

    #if UNITY_REVERSED_Z
		output.positionCS.z =
			min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
	#else
		output.positionCS.z =
			max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
	#endif
    //Ӧ������ST�任s
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

void ShadowCasterPassFragment(Varyings input)
{
    //��input����ȡʵ����ID������洢������ʵ��������������ȫ�־�̬������
    UNITY_SETUP_INSTANCE_ID(input);
    // ����LOD�ü�
    ClipLOD(input.positionCS.xy, unity_LODFade.x);
    //��ȡ����������ɫ
    float4 base = GetBase(input.baseUV);
    //ֻ����_CLIPPING�ؼ�������ʱ����öδ���
    #if defined(_SHADOWS_CLIP)
        //clip�����Ĵ���������<=0��ᶪ����ƬԪ
        clip(base.a - GetCutoff(input.baseUV));
    #elif defined(_SHADOWS_DITHER)
		float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
		clip(base.a - dither);
    #endif
    //������ͽ����ˣ����ǲ���Ҫ�����κ�ֵ����ƬԪ��Ȼ�д����Ӱ��ͼ��DepthBuffer
}

#endif