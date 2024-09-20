Shader "Custom RP/Unlit" {
	
	Properties {
		_BaseMap("Texture", 2D) = "white" {}
		[HDR] _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
	}
	
	SubShader {
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "UnlitInput.hlsl"
		ENDHLSL
		
Pass {
	Blend [_SrcBlend] [_DstBlend]
	ZWrite [_ZWrite]
	HLSLPROGRAM
	#pragma target 3.5
	#pragma shader_feature _CLIPPING
	#pragma multi_compile_instancing
	#pragma vertex UnlitPassVertex
	#pragma fragment UnlitPassFragment
	#include "UnlitPass.hlsl"

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

Pass {
	Tags {
		"LightMode" = "Meta"
	}

	Cull Off

	HLSLPROGRAM
	#pragma target 3.5
	#pragma vertex MetaPassVertex
	#pragma fragment MetaPassFragment
	#include "MetaPass.hlsl"
	ENDHLSL
}
	}
	CustomEditor "CustomShaderGUI"
}