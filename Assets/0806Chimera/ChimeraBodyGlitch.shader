Shader "Chimera/BodyGlitch"
{
    // 專為 Quest 3 MR 環境修復的 Shader。
    // 加入了 NaN 防護機制，並透過 Fallback 借用 URP 標準深度通道。
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}

        _UvRect    ("UV Window offset xy size zw", Vector) = (0, 0, 1, 1)
        _ProjScale ("Projection Scale", Range(0.1, 2)) = 0.5

        _Glitch     ("Glitch Amount", Range(0, 1)) = 0.3
        _GlitchRate ("Glitch Rate steps per sec", Range(0, 24)) = 4
        _Burst      ("Burst fraction of time corrupt", Range(0, 1)) = 0.5
        _Drift      ("Drift", Range(0, 0.1)) = 0
        _Blocks     ("Block Count", Range(2, 64)) = 10
        _Tear       ("Tear", Range(0, 0.5)) = 0.14
        _Chroma     ("Chroma Split", Range(0, 0.1)) = 0.02
        _Blowout    ("Channel Blowout", Range(0, 1)) = 0.2
        _Quantize   ("Posterise", Range(0, 1)) = 0.15

        _Seg    ("Segment (per node)", Range(0, 1)) = 0.4
        _Radial ("Radial (per node)", Range(0, 1)) = 0.45
        _Warp   ("Warp (per node)", Range(0, 1)) = 0.15
        _Taper  ("Taper (per node)", Range(0, 1)) = 0.3
        _Lobes  ("Lobes (per node)", Range(0, 8)) = 3
        _Squash ("Squash (per node)", Range(0, 3)) = 1
        _Seed   ("Seed (per node)", Float) = 0
        _Hue    ("Hue Shift (per node)", Float) = 0
        _Pulse  ("Pulse On", Range(0, 1)) = 1

        _Amp      ("Displacement Amp", Range(0, 0.6)) = 0.12
        _Irid     ("Rim Iridescence", Range(0, 2)) = 0.45
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _HueMix   ("Hue Shift Mix", Range(0, 1)) = 0.1
        _Dark     ("Darken", Range(0, 1)) = 0
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
                float _Seg;
                float _Radial;
                float _Warp;
                float _Taper;
                float _Lobes;
                float _Squash;
                float _Seed;
                float _Hue;
                float _Pulse;
                float _Amp;
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

            // 負責計算模型頂點變形的函式
            float3 Displace(float3 p, float3 n)
            {
                float seg = sin(p.y * 9.0 + _Seed * 6.283) * _Seg;
                
                // ★ 致命修復 1：加上 1e-5 避免 Quest 3 顯示卡算到 0 導致 NaN 崩潰
                float ang = atan2(p.z, p.x + 1e-5); 
                
                float rad = sin(ang * max(1.0, _Lobes) + _Seed * 3.1) * _Radial;
                float warp = (sin(p.x * 4.3 + _Seed * 11.0)
                            + sin(p.y * 3.7 + _Seed * 7.0)
                            + sin(p.z * 5.1 + _Seed * 13.0)) * 0.333 * _Warp;
                float breathe = _Pulse * 0.05 * sin(_Time.y * 2.4 + _Seed * 6.283);

                p += n * ((seg + rad + warp) * _Amp + breathe);
                p.xz *= 1.0 - saturate(_Taper) * saturate(p.y * 0.5 + 0.5) * 0.6;
                p.y *= lerp(1.0, _Squash, 0.5);
                return p;
            }

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

                float3 baseOS = IN.positionOS.xyz;
                // ★ 致命修復 2：法線歸一化前加上微小數值，避免法線為 0 時產生 NaN 感染全畫面
                float3 nOS = normalize(IN.normalOS + float3(1e-5, 1e-5, 1e-5));
                float3 dispOS = Displace(baseOS, nOS);

                OUT.basePosOS = baseOS;
                OUT.positionWS = TransformObjectToWorld(dispOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(nOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

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

                // ★ 致命修復 3：計算燈光視角時加入微小數值，防止攝影機貼近物件中心時除以零崩潰
                float3 N = normalize(IN.normalWS + float3(1e-5, 1e-5, 1e-5));
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS + float3(1e-5, 1e-5, 1e-5));

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
    
    // ★ 關鍵修復：呼叫 Unity 內建的 URP Lit 材質球。
    // 這行會自動幫我們的自訂 Shader 補齊 MR 專案 (Portal/Depth API) 必備的「深度通道 DepthOnly」。
    Fallback "Universal Render Pipeline/Lit"
}