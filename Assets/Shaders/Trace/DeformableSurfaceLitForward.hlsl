#ifndef DEFORMABLE_SURFACE_LIT_FORWARD_INCLUDED
#define DEFORMABLE_SURFACE_LIT_FORWARD_INCLUDED

#include "Assets/Scripts/Trace/TracingCommonConstants.hlsl"
#include "LocalPackages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "LocalPackages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "DeformingReprojection.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 baseUV : TEXCOORD0;
};

struct Varyings
{
    float2 baseUV : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float3 tangentWS : TEXCOORD3;
    float3 bitangentWS : TEXCOORD4;
    float3 uvOriginPositionWS : TEXCOORD5;
    float4 positionCS : SV_POSITION;
};

struct FragmentInputData
{
    float2 baseUV;
    float3 positionWS;
    float3 noemalWS;
    float3 tangentWS;
    float4 positionCS;
};

sampler2D _MainTex;
float4 _MainTex_ST;

Varyings DeformableLitPassVertex(Attributes input)
{
    Varyings o;
    o.positionCS = TransformObjectToHClip(input.positionOS);
    o.positionWS = TransformObjectToWorld(input.positionOS);
    o.normalWS = TransformObjectToWorldNormal(input.normalOS);
    o.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
    o.bitangentWS = cross(o.normalWS, o.tangentWS) * input.tangentOS.w;
    o.baseUV = TRANSFORM_TEX(input.baseUV, _MainTex);
    
    return o;
}

FragmentInputData getInputData(Varyings input)
{
    FragmentInputData ret;
    
}

half4 DeformableLitPassFragment(Varyings input) : SV_Target
{
    if (haveDeformation(input.positionWS.xz))
    {
        float2 ht = tex2D(_TraceHeightmap, worldPosToTextureUV(input.positionWS.xz)) / 10;
        float4 ret = float4(ht, 0.0f, 1.0f);
    }
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
    half3 albedo = tex2D(_MainTex, input.baseUV);
    half3 diffuse =
        albedo *
        mainLight.color *
        mainLight.shadowAttenuation *
        saturate(dot(normalize(input.normalWS), normalize(mainLight.direction)));
    return half4(diffuse, 1.0f);
}

#endif