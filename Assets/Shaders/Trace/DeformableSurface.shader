Shader "Tracing/DeformableSurface"
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
            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/Scripts/Trace/TracingCommonConstants.hlsl"
            #include "LocalPackages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LocalPackages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "DeformingReprojection.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 baseUV : TEXCOORD0;
            };

            struct Varyings
            {
                float2 baseUV : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 positionCS : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _TraceHeightmap;
            float4 _TraceHeightmap_ST;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.positionWS = TransformObjectToWorld(v.positionOS);
                o.baseUV = TRANSFORM_TEX(v.baseUV, _MainTex);
                return o;
            }

            float2 worldPosToTextureUV(float2 worldPos)
            {
                // 16 m grids
                // -x-z corner of 
                float2 xzBias = _PlayerPosition.xz - fmod(_PlayerPosition.xz, GRID_SIZE);
                float2 mainGridPos = worldPos - xzBias;
                mainGridPos.x = mainGridPos.x < 0 ? mainGridPos.x + GRID_SIZE : mainGridPos.x;
                mainGridPos.y = mainGridPos.y < 0 ? mainGridPos.y + GRID_SIZE : mainGridPos.y;
                // 32 x 32 = 1024
                return mainGridPos / GRID_SIZE;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 relPos = i.positionWS.xz - _PlayerPosition.xz;
                if (abs(relPos.x) > 16.0f ||
                    abs(relPos.y) > 16.0f)
                    return float4(0.0f, 0.0f, 0.0f, 1.0f);
                float2 ht = tex2D(_TraceHeightmap, worldPosToTextureUV(i.positionWS.xz)) / 10;
                float4 ret = float4(ht, 0.0f, 1.0f);
                return ret;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}