Shader "Chimera/SegmentDisplaced"
{
    // 所有 property 都能被 MaterialPropertyBlock 逐 renderer 覆寫。
    // 一份材質、每一節不同外觀,不 leak 材質實例。
    Properties
    {
        _BaseColor    ("Base Color", Color)   = (0.8, 0.8, 0.8, 1)
        _DisplaceAmp  ("Displace Amount", Float) = 0.12
        _NoiseFreq    ("Noise Frequency", Float) = 3.0
        _Seed         ("Seed Offset", Float)     = 0.0
        _Erosion      ("Erosion", Range(0,1))    = 0.0
        _Iridescence  ("Iridescence", Range(0,1))= 0.0
        _EmissionColor("Emission", Color)        = (0,0,0,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _DisplaceAmp;
                float  _NoiseFreq;
                float  _Seed;
                float  _Erosion;
                float  _Iridescence;
                float4 _EmissionColor;
            CBUFFER_END

            // ---- 簡單的 3D value noise(位移用)----
            float hash31(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            float vnoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash31(i + float3(0,0,0));
                float n100 = hash31(i + float3(1,0,0));
                float n010 = hash31(i + float3(0,1,0));
                float n110 = hash31(i + float3(1,1,0));
                float n001 = hash31(i + float3(0,0,1));
                float n101 = hash31(i + float3(1,0,1));
                float n011 = hash31(i + float3(0,1,1));
                float n111 = hash31(i + float3(1,1,1));
                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                // 沿法線位移:noise 以 _Seed 位移 → 每個人凹凸圖樣不同
                float3 posOS = IN.positionOS.xyz;
                float  n = vnoise(posOS * _NoiseFreq + _Seed);
                float  d = (n - 0.5) * 2.0 * _DisplaceAmp; // 置中,可外凸也可內凹
                posOS += IN.normalOS * d;

                VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
                OUT.positionHCS = vpi.positionCS;
                OUT.positionWS  = vpi.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // 主光 + 環境光(球諧)
                Light mainLight = GetMainLight();
                float ndl = saturate(dot(N, mainLight.direction));
                float3 lighting = mainLight.color * ndl + SampleSH(N);

                // 底色;erosion 去飽和 + 壓暗,模擬衰敗
                float3 baseCol = _BaseColor.rgb;
                float  grey = dot(baseCol, float3(0.299, 0.587, 0.114));
                baseCol = lerp(baseCol, grey.xxx * 0.6, _Erosion);

                float3 col = baseCol * lighting;

                // 虹彩:邊緣(fresnel)驅動的彩色薄膜
                float fres = pow(1.0 - saturate(dot(N, V)), 3.0);
                float3 irid = 0.5 + 0.5 * sin(fres * 6.2831 + float3(0.0, 2.094, 4.188));
                col += irid * fres * _Iridescence;

                col += _EmissionColor.rgb;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
