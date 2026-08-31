Shader "Ninja/Standard"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _AmbientColor ("Ambient Color", Color) = (1,1,1,1)
        _MainTex ("Base Texture (RGB) Alpha (A)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [ToggleUI] _AlphaTest ("Enable Alpha Cutout", Float) = 0.0

        _SpecColor ("Specular Color", Color) = (0, 0, 0, 1)
        _Shininess ("Shininess / Exponent", Range(0, 1)) = 0.0

        [ToggleUI] _UseEnvMap ("Environment Reflection", Float) = 0.0
        _EnvMap ("Reflection Map", 2D) = "black" {}

        // Rendering Pipeline States
        _Mode ("Rendering Mode", Float) = 0.0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 0.0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Operation", Float) = 0.0
        [Enum(Off,0,On,1)] _ZWrite ("Depth Write", Float) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0.0
        [ToggleUI] _Unlit ("Unlit Mode", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull [_Cull]

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            Blend [_SrcBlend] [_DstBlend]
            BlendOp [_BlendOp]
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma vertex vert_ninja
            #pragma fragment frag_ninja
            #pragma target 3.0
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "NinjaCore.cginc"
            ENDCG
        }
    }
    CustomEditor "UnityNinja.Editor.NinjaShaderGUI"
}