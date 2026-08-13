Shader "Custom/VR_GrassWindRipple"
{
    // Properties 區塊：顯示在 Unity Material 面板上的控制選項
    Properties
    {
        // 主貼圖 (Base Map) - 放你原本的草地底圖
        _BaseMap ("Base Map", 2D) = "white" {}
        // 草地的基本顏色，用來整體調亮或調暗
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        // 波紋移動的速度
        _WaveSpeed ("Wave Speed", Float) = 2.0
        // 波紋的密度與大小 (數值越小，波紋越寬)
        _WaveScale ("Wave Scale", Float) = 0.5
        // 波紋的強度，控制明暗變化的明顯程度
        _WaveStrength ("Wave Strength", Range(0.0, 0.5)) = 0.15
        
        // 使用 0 到 360 度的角度來控制波紋的旋轉方向
        _WaveRotation ("Wave Rotation (Degrees)", Range(0.0, 360.0)) = 0.0
        
        // 【新增】陰影亮度的拉桿。0 代表純黑，數值越大背光面越亮
        _ShadowBrightness ("Shadow Brightness", Range(0.0, 1.0)) = 0.3
    }

    SubShader
    {
        // 標籤設定：告訴 Unity 這是不透明物件，並且專為 URP 渲染管線設計
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            // 設定為 UniversalForward，這樣才能接收到場景中的主要燈光 (Main Light)
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            // 宣告頂點著色器與像素著色器的函數名稱
            #pragma vertex vert
            #pragma fragment frag

            // 引入 URP 核心函式庫
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // 引入 URP 光照函式庫，讓我們可以獲取 Directional Light 的資訊
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Attributes 結構：從 Unity 接收 3D 模型的原始資料
            struct Attributes
            {
                float4 positionOS : POSITION; // 模型的本地座標
                float2 uv : TEXCOORD0;        // 貼圖的 UV 座標
                float3 normalOS : NORMAL;     // 模型的法線 (表面朝向)，用來計算受光角度
            };

            // Varyings 結構：將頂點著色器計算完的資料，傳遞給像素著色器
            struct Varyings
            {
                float4 positionHCS : SV_POSITION; // 螢幕上的裁剪空間座標
                float2 uv : TEXCOORD0;            
                float3 positionWS : TEXCOORD1;    // 世界座標，用來計算風的方向
                float3 normalWS : NORMAL;         // 世界空間的法線，傳遞給像素著色器算光照
            };

            // 宣告與 Properties 對應的貼圖變數
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // 宣告與 Properties 對應的數值變數 (為了效能，包裝在 CBUFFER 中)
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;    
                half4 _BaseColor;      
                float _WaveSpeed;      
                float _WaveScale;      
                float _WaveStrength;   
                float _WaveRotation;   
                float _ShadowBrightness; // 【新增】接收面板上的陰影亮度數值
            CBUFFER_END

            // 頂點著色器 (Vertex Shader)：負責處理每個頂點的位置
            Varyings vert(Attributes input)
            {
                Varyings output;

                // 轉換頂點位置從本地到螢幕空間
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                // 將模型的法線從本地空間轉換到世界空間，才能跟世界空間的燈光方向做計算
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                return output;
            }

            // 像素著色器 (Fragment Shader)：負責計算最終畫在螢幕上的顏色
            half4 frag(Varyings input) : SV_Target
            {
                // 1. 讀取底圖顏色
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = texColor * _BaseColor;

                // 2. 計算風的波紋效果
                float rad = _WaveRotation * (PI / 180.0);
                float dirX = cos(rad);
                float dirZ = sin(rad);
                float timeEffect = _Time.y * _WaveSpeed;
                float waveInput = (input.positionWS.x * dirX + input.positionWS.z * dirZ) * _WaveScale + timeEffect;
                float wave = (sin(waveInput) * 0.5) + 0.5;
                float rippleEffect = 1.0 - (wave * _WaveStrength);
                
                // 將波紋效果先結合到基礎顏色上
                baseColor.rgb *= rippleEffect;

                // 3. 簡易光照計算 (Simple Lambert)
                // 獲取場景中的主要 Directional Light
                Light mainLight = GetMainLight();
                
                // 將法線標準化，確保計算正確
                float3 normal = normalize(input.normalWS);
                
                // 計算「光線方向」與「表面朝向(法線)」的內積 (Dot Product)
                // saturate 確保數值在 0 到 1 之間。向光面會接近 1 (最亮)，背光面會是 0 (最暗)
                float NdotL = saturate(dot(normal, mainLight.direction));
                
                // 【修改】使用 _ShadowBrightness 來決定環境光 (也就是背光面) 的亮度
                // 這樣背光面就不會死黑，而是保有一定的可見度
                float3 ambientLight = float3(_ShadowBrightness, _ShadowBrightness, _ShadowBrightness);
                
                // 最終的光照強度 = (主燈光顏色 * 受光程度) + 調整過亮度的環境光
                float3 lighting = (mainLight.color * NdotL) + ambientLight;

                // 4. 將有波紋的草地顏色，乘上最終的光照結果
                half4 finalColor;
                finalColor.rgb = baseColor.rgb * lighting;
                finalColor.a = baseColor.a;

                return finalColor;
            }
            ENDHLSL
        }
    }
}