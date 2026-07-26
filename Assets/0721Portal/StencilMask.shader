// Custom/StencilMask
// 只寫入 stencil buffer，不畫任何顏色、不寫深度。
// 貼在「窗口」那片 Quad（卡片的洞）上。
Shader "Custom/StencilMask"
{
    Properties
    {
        [IntRange] _StencilID ("Stencil ID", Range(0, 255)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2   // Back
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4 // LEqual
    }

    SubShader
    {
        // Geometry-1 = 1999，保證比所有要被遮罩的物件先畫
        Tags
        {
            "RenderType"       = "Opaque"
            "Queue"            = "Geometry-1"
            "RenderPipeline"   = "UniversalPipeline"
        }

        Pass
        {
            Name "StencilMask"
            Tags { "LightMode" = "UniversalForward" }

            Cull  [_Cull]
            ZTest [_ZTest]
            ZWrite Off        // 不寫深度，後面的物件才畫得出來
            ColorMask 0       // 不寫任何顏色

            Stencil
            {
                Ref  [_StencilID]
                Comp Always    // 一定通過
                Pass Replace   // 通過時把 stencil 值蓋成 Ref
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _StencilID;
                float _Cull;
                float _ZTest;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return half4(0, 0, 0, 0); // ColorMask 0，這裡回什麼都不影響
            }
            ENDHLSL
        }
    }

    Fallback Off
}
