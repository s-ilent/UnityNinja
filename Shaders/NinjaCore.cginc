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
sampler2D _EnvMap;

struct appdata_ninja
{
    float4 vertex   : POSITION;
    float3 normal   : NORMAL;
    float4 texcoord : TEXCOORD0;
    fixed4 color    : COLOR;
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
};

v2f_ninja vert_ninja(appdata_ninja v)
{
    v2f_ninja o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.color = v.color;

    UNITY_TRANSFER_FOG(o, o.pos);
    TRANSFER_SHADOW(o);
    return o;
}

fixed4 frag_ninja(v2f_ninja i) : SV_Target
{
    fixed4 tex = tex2D(_MainTex, i.uv);
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

    fixed3 ambient = ShadeSH9(float4(N, 1.0)) * _AmbientColor.rgb;
    fixed3 diffuse = _LightColor0.rgb * NdotL * atten;
    fixed3 specular = (_Shininess > 0.0) ? _SpecColor.rgb * pow(NdotH, max(1.0, _Shininess * 64.0)) * atten : fixed3(0, 0, 0);

    fixed3 finalRGB = baseColor.rgb * (ambient + diffuse) + specular;

    if (_UseEnvMap > 0.5)
    {
        float2 envUV = N.xy * 0.5 + 0.5;
        fixed4 envCol = tex2D(_EnvMap, envUV);
        finalRGB += envCol.rgb * 0.5;
    }

    fixed4 outColor = fixed4(finalRGB, baseColor.a);
    UNITY_APPLY_FOG(i.fogCoord, outColor);
    return outColor;
}

#endif