// FrutigerAeroWater_Opaque.shader
// URP / Quest 3 (Unity 6, Single Pass Instanced) 
// 包含安全時間保護機制與虹光效果的優化版本

Shader "Yiho/FrutigerAeroWater_Opaque"
{
    Properties
    {
        [Header(Water Color)]
        // 水的基礎顏色設定
        _DeepColor        ("Deep Color", Color)              = (0.02, 0.30, 0.42, 1)
        _ShallowColor     ("Shallow Color", Color)           = (0.28, 0.82, 0.78, 1)
        _ShoreColor       ("Shore Blend-out Color", Color)   = (0.62, 0.92, 0.88, 1)

        [Header(Fake Sky for Reflection)]
        // 假天空反射的顏色設定
        _SkyTop           ("Sky Zenith", Color)              = (0.10, 0.38, 0.92, 1)
        _SkyHorizon       ("Sky Horizon", Color)             = (0.82, 0.95, 1.00, 1)
        _ReflStrength     ("Reflection Strength", Range(0,1))= 0.85

        [Header(Ring Shape Object Space)]
        // 水面圓盤的形狀與大小
        _CenterOS         ("Center (Object Space XZ)", Vector)= (0,0,0,0)
        _InnerRadius      ("Inner Radius", Float)            = 1.5
        _OuterRadius      ("Outer Radius", Float)            = 6.0
        _EdgeFade         ("Shore Fade Width", Range(0.01,4))= 0.8

        [Header(Flow)]
        // 水波紋流動的法線貼圖與速度設定
        _NormalMap        ("Normal Map", 2D)                 = "bump" {}
        _AngularTiling    ("Angular Tiling (INTEGER)", Float)= 8
        _RadialTiling     ("Radial Tiling", Float)           = 2
        _FlowSpeed        ("Angular Flow Speed", Float)      = 0.04
        _RadialDrift      ("Radial Drift Speed", Float)      = 0.02
        _Layer2Scale      ("Layer 2 Scale", Float)           = 2.1
        _Layer2Speed      ("Layer 2 Speed Mul", Float)       = -0.55
        _NormalStrength   ("Normal Strength", Range(0,3))    = 1.0

        [Header(Specular)]
        // 光源高光反射設定
        _Smoothness       ("Smoothness", Range(0,1))         = 0.92
        _SpecIntensity    ("Spec Intensity", Range(0,8))     = 2.5
        _FresnelPower     ("Fresnel Power", Range(1,8))      = 5.0
        _FresnelBias      ("Fresnel Bias", Range(0,0.5))     = 0.02

        [Header(Iridescence)]
        // 虹光效果設定 (已為你加回)
        _IriStrength      ("Iridescence Strength", Range(0,2))  = 0.55
        _IriFreq          ("Iridescence Frequency", Range(0,6)) = 2.2
        _IriPhase         ("Iridescence Phase", Range(0,1))     = 0.0
        _IriFromNormal    ("Iri Normal Influence", Range(0,2))  = 0.6

        [Header(Noise For Iridescence)]
        // 雜訊貼圖，提供虹光不規則的自然變化
        _NoiseTex         ("Noise Texture", 2D)                 = "gray" {}
        _FoamNoiseTiling  ("Noise Tiling", Float)               = 6
        _FoamSpeed        ("Noise Scroll Speed", Float)         = 0.09
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque" // 維持不透明
            "Queue"          = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // 開啟深度寫入，確保與場景的物理遮擋正確
            ZWrite On 
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // 宣告貼圖與取樣器
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_NoiseTex);  SAMPLER(sampler_NoiseTex);

            // 宣告在屬性面板可以調整的變數
            CBUFFER_START(UnityPerMaterial)
                half4  _DeepColor, _ShallowColor, _ShoreColor;
                half4  _SkyTop, _SkyHorizon;
                half   _ReflStrength;
                float4 _CenterOS;
                float  _InnerRadius, _OuterRadius;
                half   _EdgeFade;
                float4 _NormalMap_ST;
                float  _AngularTiling, _RadialTiling;
                float  _FlowSpeed, _RadialDrift, _Layer2Scale, _Layer2Speed;
                half   _NormalStrength;
                half   _Smoothness, _SpecIntensity, _FresnelPower, _FresnelBias;
                // 虹光與雜訊的變數
                half   _IriStrength, _IriFreq, _IriPhase, _IriFromNormal;
                float4 _NoiseTex_ST;
                float  _FoamNoiseTiling, _FoamSpeed;
            CBUFFER_END

            // 頂點著色器的輸入資料
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 傳遞給片段著色器的資料
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 polarP     : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #ifndef INV_TWO_PI
                #define INV_TWO_PI 0.15915494
            #endif

            // 頂點著色器：負責計算物件在世界中的位置
            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.polarP     = IN.positionOS.xz - _CenterOS.xz;
                
                return OUT;
            }

            // 虹光調色盤函式：利用 Cosine 數學產生漸層的彩虹顏色
            half3 IridescencePalette(half t)
            {
                return 0.5h + 0.5h * cos(6.28318h * (t + half3(0.0h, 0.33h, 0.67h)));
            }

            // 片段著色器：負責計算最終螢幕上像素的顏色
            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 p  = IN.polarP;
                float  r2 = max(dot(p, p), 1e-6); // 保護機制：避免除以零
                float  r  = sqrt(r2);

                float ang = atan2(p.y, p.x) * INV_TWO_PI;
                float2 uvBase = float2(ang * _AngularTiling, r * _RadialTiling);

                // 【核心保護機制：避免五分鐘後崩潰】
                // fmod 函數將時間限制在 0 到 1000 之間循環
                // 防止時間變數無限增大導致 GPU 運算溢位(NaN錯誤)
                float t = fmod(_Time.y, 1000.0);

                // 計算兩層法線波紋流動的 UV 座標
                float2 uv1 = uvBase + float2(t * _FlowSpeed,  t * _RadialDrift);
                float2 uv2 = uvBase * _Layer2Scale + float2(t * _FlowSpeed * _Layer2Speed, t * -_RadialDrift * 0.7);

                // 取樣兩次法線貼圖來製造波浪交叉感
                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2));
                
                half3 nTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                nTS.xy *= _NormalStrength;
                nTS = normalize(nTS);

                // 計算切線空間 (Tangent Space) 轉換
                float3 tangentOS   = float3(-p.y, 0, p.x) / r;
                float3 bitangentOS = float3( p.x, 0, p.y) / r;
                half3 T = half3(TransformObjectToWorldDir(tangentOS));
                half3 B = half3(TransformObjectToWorldDir(bitangentOS));
                half3 Nbase = normalize(IN.normalWS);
                half3 N = normalize(nTS.x * T + nTS.y * B + nTS.z * Nbase);

                // 計算攝影機視角方向
                half3 V = half3(normalize(GetWorldSpaceViewDir(IN.positionWS)));

                // 計算岸邊平滑漸層
                half edgeDist = min(r - _InnerRadius, _OuterRadius - r);
                half shore  = saturate(edgeDist / _EdgeFade);

                // 混合深水與淺水顏色
                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, shore * shore);
                water = lerp(_ShoreColor.rgb, water, saturate(shore * 3.0h));

                // 計算菲涅耳 (Fresnel) 反射與假天空
                half  ndv = saturate(dot(N, V));
                half  fres = saturate(_FresnelBias + (1.0h - _FresnelBias) * pow(1.0h - ndv, _FresnelPower));
                half3 R = reflect(-V, N);
                half3 sky = lerp(_SkyHorizon.rgb, _SkyTop.rgb, saturate(R.y * 0.5h + 0.5h));

                half3 col = water;
                col = lerp(col, sky, fres * _ReflStrength);

                // 取樣雜訊貼圖 (為了讓虹光效果更自然，帶有一點點油污感)
                half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex,
                                  uvBase * _FoamNoiseTiling + float2(t * _FoamSpeed, t * _FoamSpeed * 0.3));

                // ---------- 虹光運算段落 (加回) ----------
                // 利用菲涅耳與雜訊的值來決定彩虹的色相位置
                half iriT = fres * _IriFreq + N.y * _IriFromNormal + noise.r * 0.25h + _IriPhase;
                half3 iri = IridescencePalette(iriT);
                
                // 將虹光疊加到原本的水面上
                col += iri * (fres * _IriStrength);
                // ----------------------------------------

                // 計算主光的高光反射 (Specular)
                Light mainLight = GetMainLight();
                half3 L = half3(mainLight.direction);
                half3 H = SafeNormalize(L + V);
                half  specPower = exp2(10.0h * _Smoothness + 1.0h);
                half  spec = pow(saturate(dot(N, H)), specPower);
                half  ndl  = saturate(dot(Nbase, L)) * 0.5h + 0.5h;
                
                // 疊加環境漫反射與高光
                col *= ndl;
                col += mainLight.color * spec * _SpecIntensity;

                // 回傳最終顏色，Alpha設定為1(完全不透明)
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }

    // 若硬體不支援則關閉 Shader
    Fallback Off
}