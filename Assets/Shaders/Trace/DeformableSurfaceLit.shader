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
        Tags
        {
            "RenderType"="Opaque" "Lightmode"="UniversalForward"
        }

        Pass
        {
            HLSLPROGRAM
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma vertex DeformableLitPassVertex
            #pragma fragment DeformableLitPassFragment

            #include "DeformableSurfaceLitForward.hlsl"
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}