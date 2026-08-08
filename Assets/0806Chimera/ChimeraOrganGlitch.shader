Shader "Chimera/OrganGlitch"
{
    // 專為「附肢 (Organ)」設計的 Shader。
    // 視覺與身體完全一致，並已移除會導致四肢脫離身體的錯誤縮放算式，
    // 請直接使用 C# 面板上的 Organ Amount 與 Appendage Amount 來控制大小。
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}

        _UvRect    ("UV Window offset xy size zw", Vector) = (0, 0, 1, 1)
        _ProjScale ("Projection Scale", Range(0.1, 2)) = 0.5

        // 視覺特效參數
        _Glitch     ("Glitch Amount", Range(0, 1)) = 0.3
        _GlitchRate ("Glitch Rate steps per sec", Range(0, 24)) = 4
        _Burst      ("Burst fraction of time corrupt", Range(0, 1)) = 0.5
        _Drift      ("Drift", Range(0, 0.1)) = 0
        _Blocks     ("Block Count", Range(2, 64)) = 10
        _Tear       ("Tear", Range(0, 0.5)) = 0.14
        _Chroma     ("Chroma Split", Range(0, 0.1)) = 0.02
        _Blowout    ("Channel Blowout", Range(0, 1)) = 0.2
        _Quantize   ("Posterise", Range(0, 1)) = 0.15

        // C# 傳遞的參數
        _Seed       ("Seed (per node)", Float) = 0
        _Hue        ("Hue Shift (per node)", Float) = 0
        
        // 接收 C# 傳來的原始參數，但不強制變形模型以防脫離
        _Len        ("Length", Float) = 1.0 
        
        _Irid       ("Rim Iridescence", Range(0, 2)) = 0.45
        _RimPower   ("Rim Power", Range(0.5, 8)) = 3
        _HueMix     ("Hue Shift Mix", Range(0, 1)) = 0.1
        _Dark       ("Darken", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _UvRect;
                float _ProjScale;
                float _Glitch;
                float _GlitchRate;
                float _Burst;
                float _Drift;
                float _Blocks;
                float _Tear;
                float _Chroma;
                float _Blowout;
                float _Quantize;
                float _Seed;
                float _Hue;
                float _Len;
                float _Irid;
                float _RimPower;
                float _HueMix;
                float _Dark;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 basePosOS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 HueShift(float3 c, float a)
            {
                float3 k = float3(0.57735, 0.57735, 0.57735);
                float cs = cos(a);
                float sn = sin(a);
                return c * cs + cross(k, c) * sn + k * dot(k, c) * (1.0 - cs);
            }

            float Hash21(float2 p, float s)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233)) + s * 37.0) * 43758.5453);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // ★ 修復核心：移除強制縮放，保持原始模型座標，四肢就不會脫離身體了！
                float3 baseOS = IN.positionOS.xyz;
                float3 nOS = normalize(IN.normalOS);

                OUT.basePosOS = baseOS;
                OUT.positionWS = TransformObjectToWorld(baseOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(nOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // 視覺邏輯與 Body 保持完全一致
                float tq = floor(_Time.y * _GlitchRate);
                float burstH = Hash21(float2(tq, 41.0), _Seed + 5.0);
                float burst = (_GlitchRate <= 0.001 || _Burst >= 0.999) ? 1.0 : step(1.0 - _Burst, burstH);

                float2 local = IN.basePosOS.xy * _ProjScale + 0.5;
                local.x += _Drift * _Time.y;
                local = saturate(frac(local));

                float2 blk = floor(local * _Blocks) / _Blocks;
                float ts = tq * 0.137;

                float gTear  = step(1.0 - _Glitch, Hash21(blk, _Seed + ts + 1.0)) * burst;
                float gChrom = step(1.0 - _Glitch, Hash21(blk, _Seed + ts + 2.0)) * burst;
                float gBlow  = step(1.0 - _Glitch, Hash21(blk, _Seed + ts + 3.0)) * burst;
                float gQuant = step(1.0 - _Glitch, Hash21(blk, _Seed + ts + 4.0)) * burst;
                float hTear  = Hash21(blk, _Seed + ts + 11.0);

                float2 q = lerp(local, blk, _Quantize * (0.35 + 0.65 * gQuant));
                float2 uv = _UvRect.xy + q * _UvRect.zw;

                float row = floor(local.y * _Blocks);
                float hRow = Hash21(float2(row, 7.0), _Seed + ts + 13.0);
                uv.x += (hTear - 0.5) * _Tear * gTear;
                uv.x += (hRow - 0.5) * _Tear * 0.5 * step(1.0 - _Glitch * 0.6, hRow) * burst;

                float cd = (hTear - 0.5) * _Chroma * (0.35 + 0.65 * gChrom);

                half3 col;
                col.r = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv + float2(cd, 0.0), 0).r;
                col.g = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 0).g;
                col.b = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv - float2(cd, 0.0), 0).b;

                col = lerp(col, saturate(col.brg * 1.6), _Blowout * gBlow);
                col = HueShift(col, _Hue * _HueMix);
                col *= 1.0 - _Dark;

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                Light mainLight = GetMainLight();
                float ndl = saturate(dot(N, mainLight.direction)) * 0.5 + 0.5;   

                half3 ambient = half3(0.32, 0.36, 0.42);   
                half3 lit = col * (mainLight.color * ndl + ambient);

                float fres = pow(saturate(1.0 - saturate(dot(N, V))), _RimPower);
                half3 irid = half3(0.5 + 0.5 * sin(_Seed * 2.0 + float3(0.0, 2.1, 4.2) + fres * 6.0));
                lit += irid * fres * _Irid;

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}