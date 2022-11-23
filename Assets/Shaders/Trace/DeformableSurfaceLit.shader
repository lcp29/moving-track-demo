Shader "Tracing/DeformableSurfaceLit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TraceHeightmap ("TraceHeightmap", 2D) = "white" {}
        [HideInInspector] _PlayerPosition ("PlayerPosition", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Pass
        {
            Tags
            {
                "RenderType"="Opaque" "Lightmode"="UniversalForward"
            }
            
            HLSLPROGRAM
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma vertex DeformableLitPassVertex
            #pragma fragment DeformableLitPassFragment

            #include "DeformableSurfaceLitForward.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Tags
            {
                "Lightmode"="ShadowCaster"
            }
            
            HLSLPROGRAM
            #pragma vertex DeformableShadowCasterVertex
            #pragma fragment DeformableShadowCasterFragment
            
            #include "DeformableSurfaceShadowCaster.hlsl"
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}