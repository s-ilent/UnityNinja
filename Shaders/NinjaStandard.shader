Shader "Ninja/Standard"
{
    Properties
    {
        // Primary Surface & Colors
        _Color ("Main Color", Color) = (1,1,1,1)
        _AmbientColor ("Ambient Color", Color) = (1,1,1,1)
        _MainTex ("Base Texture (RGB) Alpha (A)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [ToggleUI] _AlphaTest ("Enable Alpha Cutout", Float) = 0.0

        // Specular & Exponent
        _SpecColor ("Specular Color", Color) = (0, 0, 0, 1)
        _Shininess ("Shininess / Exponent", Range(0, 1)) = 0.0

        // Environment Reflection (Spherical Normal Mapping)
        [ToggleUI] _UseEnvMap ("Base Texture as EnvMap (Replace UVs)", Float) = 0.0
        [ToggleUI] _AddEnvMap ("Add Second Layer EnvMap Reflection", Float) = 0.0
        _EnvMap ("Reflection Map", 2D) = "black" {}
        _EnvColor ("Reflection Color", Color) = (1,1,1,1)
        _EnvPower ("Reflection Intensity", Float) = 1.0

        // UV Clamping & Mirror / Flipping Flags (TileMode)
        [ToggleUI] _ClampU ("Clamp U", Float) = 0.0
        [ToggleUI] _ClampV ("Clamp V", Float) = 0.0
        [ToggleUI] _FlipU ("Flip / Mirror U", Float) = 0.0
        [ToggleUI] _FlipV ("Flip / Mirror V", Float) = 0.0

        // Raw Metadata Flags (Bitmask)
        _MaterialFlags ("Ninja Material Flags", Float) = 0.0

        // Rendering Pipeline States
        _Mode ("Rendering Mode", Float) = 0.0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 0.0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Operation", Float) = 0.0
        [Enum(Off,0,On,1)] _ZWrite ("Depth Write", Float) = 1.0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0.0
        [Queue] _CustomRenderQueue ("Custom Render Queue", Float) = -1.0
        [ToggleUI] _Unlit ("Unlit Mode", Float) = 0.0
        [ToggleUI] _AlphaToMask ("Alpha to Coverage", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull [_Cull]

        // 1. Forward Base Pass
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            Blend [_SrcBlend] [_DstBlend]
            BlendOp [_BlendOp]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            AlphaToMask [_AlphaToMask]

            CGPROGRAM
            #ifndef UNITY_PASS_FORWARDBASE
            #define UNITY_PASS_FORWARDBASE
            #endif

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

        // 2. Forward Add Pass (Point / Spot lights)
        Pass
        {
            Name "FORWARD_DELTA"
            Tags { "LightMode" = "ForwardAdd" }

            Blend [_SrcBlend] One
            BlendOp [_BlendOp]
            ZWrite Off
            ZTest LEqual
            AlphaToMask [_AlphaToMask]

            CGPROGRAM
            #ifndef UNITY_PASS_FORWARDADD
            #define UNITY_PASS_FORWARDADD
            #endif

            #pragma vertex vert_ninja
            #pragma fragment frag_ninja_add
            #pragma target 3.0
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "NinjaCore.cginc"
            ENDCG
        }

        // 3. Shadow Caster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On ZTest LEqual
            AlphaToMask Off

            CGPROGRAM
            #pragma vertex vertShadowCaster
            #pragma fragment fragShadowCaster
            #pragma target 3.0
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"

            struct v2f_shadow
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AlphaTest;
            float _Cutoff;
            float _Mode;

            v2f_shadow vertShadowCaster(appdata_base v)
            {
                v2f_shadow o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            float4 fragShadowCaster(v2f_shadow i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                if (_Mode > 1.5) clip(-1);
                if (_AlphaTest > 0.5)
                {
                    clip(col.a - _Cutoff);
                }
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }

        // 4. Depth Only Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct v2f_depth
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AlphaTest;
            float _Cutoff;
            float _Mode;

            v2f_depth vertDepth(appdata_base v)
            {
                v2f_depth o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 fragDepth(v2f_depth i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                if (_Mode > 1.5) clip(-1);
                if (_AlphaTest > 0.5)
                {
                    clip(col.a - _Cutoff);
                }
                return 0;
            }
            ENDCG
        }
    }
    CustomEditor "UnityNinja.Editor.NinjaShaderGUI"
}