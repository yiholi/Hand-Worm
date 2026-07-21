Shader "Chimera/Body"
{
    Properties
    {
        _Seg    ("Segment",     Range(0,1)) = 0.4
        _Radial ("Radial",      Range(0,1)) = 0.4
        _Warp   ("Warp",        Range(0,1)) = 0.3
        _Taper  ("Taper",       Range(0,1)) = 0.4
        _Seed   ("Seed",        Float)      = 0
        _Lobes  ("Lobes",       Range(2,8)) = 3
        _Squash ("Squash",      Range(0.3,2)) = 1
        _Pulse  ("Swim Pulse",  Range(0,1)) = 1
        _Facet  ("Facet",       Range(0,1)) = 0.45
        _Glass  ("Glass",       Range(0,1)) = 1
        _Irid   ("Iridescence", Range(0,2)) = 0.8
        _Hue    ("Hue Phase",   Float)      = 0
        _Dark   ("Darken",      Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ChimeraBody"
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
                float _Seg, _Radial, _Warp, _Taper, _Seed, _Lobes, _Squash, _Pulse;
                float _Facet, _Glass, _Irid, _Hue, _Dark;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                float  shell       : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float shell;
                float3 p = ChimeraDisplace(IN.positionOS.xyz, _Seg, _Radial, _Warp, _Taper,
                                           _Seed, _Lobes, _Squash, _Pulse, _Time.y, shell);
                OUT.worldPos = TransformObjectToWorld(p);
                OUT.positionHCS = TransformWorldToHClip(OUT.worldPos);
                OUT.shell = shell;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N = ChimeraFlatNormal(IN.worldPos, _Facet);
                return ChimeraShade(IN.worldPos, N, IN.shell, 0.0,
                                    _Irid, _Hue, _Dark, _Glass);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
