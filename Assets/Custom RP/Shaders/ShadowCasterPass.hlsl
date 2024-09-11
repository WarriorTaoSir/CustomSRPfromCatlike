#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

//��ȡBaseMap����Clip
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

//Ϊ��ʹ��GPU Instancing��ÿʵ������Ҫ����������,ʹ��UNITY_INSTANCING_BUFFER_START(END)������ÿʵ������
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    //���������ƫ�ƺ����ſ�����ÿʵ������
    UNITY_DEFINE_INSTANCED_PROP(float4,_BaseMap_ST)
    //_BaseColor�������еĶ����ʽ
    UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)
    //͸���Ȳ�����ֵ
    UNITY_DEFINE_INSTANCED_PROP(float,_Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

//ʹ�ýṹ�嶨�嶥����ɫ�������룬һ����Ϊ�˴�������࣬һ����Ϊ��֧��GPU Instancing����ȡobject��index��
struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    //����GPU Instancingʹ�õ�ÿ��ʵ����ID������GPU��ǰ���Ƶ����ĸ�Object
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//Ϊ����ƬԪ��ɫ���л�ȡʵ��ID����������ɫ�����������ƬԪ��ɫ�������룩Ҳ����һ���ṹ��
//����ΪVaryings����Ϊ�����������ݿ�����ͬһ�����ε�Ƭ��֮��仯
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
    //����ÿһ��ƬԪ��Ӧ��object��ΨһID
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
    //Ӧ������ST�任s
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    output.baseUV = input.baseUV * baseST.xy + baseST.zw;
    return output;
}

void ShadowCasterPassFragment(Varyings input)
{
    //��input����ȡʵ����ID������洢������ʵ��������������ȫ�־�̬������
    UNITY_SETUP_INSTANCE_ID(input);
    //��ȡ����������ɫ
    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.baseUV);
    //ͨ��UNITY_ACCESS_INSTANCED_PROP��ȡÿʵ������
    float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    float4 base = baseMap * baseColor;
    //ֻ����_CLIPPING�ؼ�������ʱ����öδ���
    #if defined(_CLIPPING)
        //clip�����Ĵ���������<=0��ᶪ����ƬԪ
        clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    #endif
    //������ͽ����ˣ����ǲ���Ҫ�����κ�ֵ����ƬԪ��Ȼ�д����Ӱ��ͼ��DepthBuffer
}

#endif