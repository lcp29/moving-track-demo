#ifndef DEFORMABLE_SURFACE_LIT_SHADOW_CASTER_INCLUDED
#define DEFORMABLE_SURFACE_LIT_SHADOW_CASTER_INCLUDED

#include "Assets/Scripts/Trace/TracingCommonConstants.hlsl"
#include "LocalPackages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "LocalPackages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "DeformingReprojection.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
};

struct Varyings
{
    float3 positionWS : TEXCOORD0;
    float4 positionCS : SV_POSITION;
};

Varyings DeformableShadowCasterVertex(Attributes input)
{
    Varyings o;
    o.positionWS = TransformObjectToWorld(input.positionOS);
    o.positionWS -= 0.2;
    o.positionCS = TransformWorldToHClip(o.positionWS);
    return o;
}

struct FragmentOutput
{
    half4 target : SV_Target;
    float4 positionCS : SV_POSITION;
};

half4 DeformableShadowCasterFragment(Varyings input) : SV_Target
{
    return 0;
}

#endif
