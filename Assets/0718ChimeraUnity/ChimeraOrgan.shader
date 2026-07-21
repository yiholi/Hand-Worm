Shader "Chimera/Organ"
{
    Properties
    {
        _Phase ("Sway Phase", Float)      = 0
        _Len   ("Tendril Len", Range(0.1,4)) = 1
        _Facet ("Facet",      Range(0,1)) = 0.3
        _Glass ("Glass",      Range(0,1)) = 1
        _Irid  ("Iridescence",Range(0,2)) = 0.8
        _Hue   ("Hue Phase",  Float)      = 0
        _Dark  ("Darken",     Range(0,1)) = 0.25
        _Sway  ("Sway Amount",Range(0,2)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ChimeraOrgan"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ChimeraCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Phase, _Len, _Facet, _Glass, _Irid, _Hue, _Dark, _Sway;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv1        : TEXCOORD1;   // x = sway 權重, y = 頂點型別
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                float2 swayType    : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float sway = IN.uv1.x;
                float vtype = IN.uv1.y;

                float3 p = IN.positionOS.xyz;
                // 只有一般組織（型別 0）跟著觸手長度縮放，眼睛牙齒不變形
                p *= lerp(1.0, _Len, sway * step(vtype, 0.5));

                float s = sway * sway * _Sway;
                p.x += sin(_Time.y * 1.5 + _Phase + p.y * 2.2) * 0.14 * s;
                p.z += cos(_Time.y * 1.2 + _Phase * 1.7 + p.y * 1.8) * 0.11 * s;

                OUT.worldPos = TransformObjectToWorld(p);
                OUT.positionHCS = TransformWorldToHClip(OUT.worldPos);
                OUT.swayType = float2(sway, vtype);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N = ChimeraFlatNormal(IN.worldPos, _Facet);
                return ChimeraShade(IN.worldPos, N, IN.swayType.x, IN.swayType.y,
                                    _Irid, _Hue, _Dark, _Glass);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
