Shader "Custom RP/Lit" {
	
	Properties {
		_BaseMap("Texture", 2D) = "white" {}
		_BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
		[KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
		_Metallic ("Metallic", Range(0, 1)) = 0
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
		[Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
	}
	
	SubShader {
		
Pass{
	Tags{
		"LightMode" = "CustomLit"
	}
	Blend [_SrcBlend] [_DstBlend]
	ZWrite [_ZWrite]
	HLSLPROGRAM
	#pragma target 3.5
	#pragma shader_feature _CLIPPING
	#pragma shader_feature _PREMULTIPLY_ALPHA
	#pragma shader_feature _RECEIVE_SHADOWS
	#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
	#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
	#pragma multi_compile_instancing
	#pragma vertex LitPassVertex
	#pragma fragment LitPassFragment
	#include "LitPass.hlsl"

	ENDHLSL
}	

Pass{
	//阴影Pass的LightMode为ShadowCaster
	Tags{
		"LightMode" = "ShadowCaster"
	}
	// 因为只需要写入深度，关闭对颜色通道的写入
	ColorMask 0

	HLSLPROGRAM
	//支持的最低平台
	#pragma target 3.5
	//支持Alpha Test的裁剪
	#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
	//定义diffuse项是否使用Premultiplied alpha的关键字
	#pragma multi_compile_instancing
	#pragma vertex ShadowCasterPassVertex
	#pragma fragment ShadowCasterPassFragment
	//阴影相关方法写在ShadowCasterPass.hlsl
	#include "ShadowCasterPass.hlsl"
	ENDHLSL
}
	}
	CustomEditor "CustomShaderGUI"
}