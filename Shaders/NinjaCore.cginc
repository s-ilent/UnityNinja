#ifndef NINJA_CORE_INCLUDED
#define NINJA_CORE_INCLUDED

fixed4 _Color;
fixed4 _AmbientColor;
sampler2D _MainTex;
float4 _MainTex_ST;
float _Cutoff;
float _AlphaTest;
float _Shininess;
float _Unlit;

float _UseEnvMap;
float _ClampU;
float _ClampV;
float _FlipU;
float _FlipV;
float _MaterialFlags;

struct appdata_ninja
{
    float4 vertex   : POSITION;
    float3 normal   : NORMAL;
    float4 texcoord : TEXCOORD0;
    fixed4 color    : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f_ninja
{
    float4 pos         : SV_POSITION;
    float2 uv          : TEXCOORD0;
    float3 worldPos    : TEXCOORD1;
    float3 worldNormal : TEXCOORD2;
    fixed4 color       : COLOR;
    UNITY_FOG_COORDS(3)
    SHADOW_COORDS(4)
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f_ninja vert_ninja(appdata_ninja v)
{
    v2f_ninja o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.color = v.color;

    UNITY_TRANSFER_FOG(o, o.pos);
    TRANSFER_SHADOW(o);
    return o;
}

float2 GetEnvironmentUV(float3 worldNormal, float3 worldPos)
{
    float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
    float3 worldUp = float3(0, 1, 0);
    float3 worldViewUp = normalize(worldUp - viewDir * dot(viewDir, worldUp));
    float3 worldViewRight = normalize(cross(viewDir, worldViewUp));
    return float2(dot(worldViewRight, worldNormal), dot(worldViewUp, worldNormal)) * 0.5 + 0.5;
}

fixed4 frag_ninja(v2f_ninja i) : SV_Target
{
    float2 sampleUV = i.uv;

    // 1. Environment Reflection Mapping (0x400000)
    if (_UseEnvMap > 0.5)
    {
        sampleUV = GetEnvironmentUV(normalize(i.worldNormal), i.worldPos);
    }
    else
    {
        // 2. Texture Wrapping / Clamping / Flipping (Mirror)
        if (_ClampU > 0.5) sampleUV.x = saturate(sampleUV.x);
        if (_ClampV > 0.5) sampleUV.y = saturate(sampleUV.y);
        if (_FlipU > 0.5 && (frac(sampleUV.x * 0.5) >= 0.5)) sampleUV.x = 1.0 - frac(sampleUV.x);
        if (_FlipV > 0.5 && (frac(sampleUV.y * 0.5) >= 0.5)) sampleUV.y = 1.0 - frac(sampleUV.y);
    }

    fixed4 tex = tex2D(_MainTex, sampleUV);
    fixed4 baseColor = tex * _Color * i.color;

    if (_AlphaTest > 0.5)
    {
        clip(baseColor.a - _Cutoff);
    }

    if (_Unlit > 0.5)
    {
        UNITY_APPLY_FOG(i.fogCoord, baseColor);
        return baseColor;
    }

    float3 N = normalize(i.worldNormal);
    float3 L = normalize(UnityWorldSpaceLightDir(i.worldPos));
    float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
    float3 H = normalize(L + V);

    float NdotL = max(0.0, dot(N, L));
    float NdotH = max(0.0, dot(N, H));

    UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

    fixed3 ambient = ShadeSH9(float4(N, 1.0));
    fixed3 diffuse = _LightColor0.rgb * NdotL * atten;
    fixed3 specular = (_Shininess > 0.0) ? _SpecColor.rgb * pow(NdotH, max(1.0, _Shininess * 64.0)) * atten : fixed3(0, 0, 0);

    ambient = lerp(ambient, _LightColor0, _AmbientColor.rgb);

    fixed3 finalRGB = baseColor.rgb * (ambient + diffuse) + specular;
    fixed4 outColor = fixed4(finalRGB, baseColor.a);
    UNITY_APPLY_FOG(i.fogCoord, outColor);
    return outColor;
}

fixed4 frag_ninja_add(v2f_ninja i) : SV_Target
{
    float2 sampleUV = i.uv;
    if (_UseEnvMap > 0.5)
    {
        sampleUV = GetEnvironmentUV(normalize(i.worldNormal), i.worldPos);
    }
    else
    {
        if (_ClampU > 0.5) sampleUV.x = saturate(sampleUV.x);
        if (_ClampV > 0.5) sampleUV.y = saturate(sampleUV.y);
        if (_FlipU > 0.5 && (frac(sampleUV.x * 0.5) >= 0.5)) sampleUV.x = 1.0 - frac(sampleUV.x);
        if (_FlipV > 0.5 && (frac(sampleUV.y * 0.5) >= 0.5)) sampleUV.y = 1.0 - frac(sampleUV.y);
    }

    fixed4 tex = tex2D(_MainTex, sampleUV);
    fixed4 baseColor = tex * _Color * i.color;

    if (_AlphaTest > 0.5)
    {
        clip(baseColor.a - _Cutoff);
    }

    if (_Unlit > 0.5)
    {
        return fixed4(0, 0, 0, 0);
    }

    float3 N = normalize(i.worldNormal);
    float3 L = normalize(UnityWorldSpaceLightDir(i.worldPos));
    float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
    float3 H = normalize(L + V);

    float NdotL = max(0.0, dot(N, L));
    float NdotH = max(0.0, dot(N, H));

    UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

    fixed3 diffuse = _LightColor0.rgb * NdotL * atten;
    fixed3 specular = (_Shininess > 0.0) ? _SpecColor.rgb * pow(NdotH, max(1.0, _Shininess * 64.0)) * atten : fixed3(0, 0, 0);

    fixed3 finalRGB = baseColor.rgb * diffuse + specular;
    fixed4 outColor = fixed4(finalRGB, baseColor.a);
    UNITY_APPLY_FOG_COLOR(i.fogCoord, outColor, fixed4(0, 0, 0, 0));
    return outColor;
}

#endif