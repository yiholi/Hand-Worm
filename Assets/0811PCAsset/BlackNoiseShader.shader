Shader "Custom/BlackNoiseShader"
{
    // [屬性區塊] 可以在 Unity 的 Inspector 面板中調整這些數值
    Properties
    {
        // 人物的主顏色（預設為深黑色）
        _BaseColor ("Base Color (主色)", Color) = (0.05, 0.05, 0.05, 1.0)
        
        // 雜訊雪花的顏色（預設為灰色/白灰）
        _NoiseColor ("Noise Color (雜訊顏色)", Color) = (0.7, 0.7, 0.7, 1.0)
        
        // 雜訊密度（數值越大，雪花點越細小密度越高）
        _NoiseScale ("Noise Scale (雜訊密度)", Range(10, 2000)) = 500.0
        
        // 雜訊閃爍速度（數值越大，畫面閃爍跳動越快）
        _NoiseSpeed ("Noise Speed (閃爍速度)", Range(0, 100)) = 30.0
        
        // 雜訊強度（控制雪花混合的明顯程度）
        _NoiseIntensity ("Noise Intensity (雜訊強度)", Range(0, 1)) = 0.6
    }

    SubShader
    {
        // 指定給 URP 渲染管線使用，並設定為不透明（Opaque）物體
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            // 宣告頂點與像素著色器函數
            #pragma vertex vert
            #pragma fragment frag

            // 引入 URP 核心函式庫
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // [模型輸入資料] 包含模型頂點位置與 UV 貼圖座標
            struct Attributes
            {
                float4 positionOS : POSITION; // 物體空間頂點位置
                float2 uv : TEXCOORD0;        // UV 座標
            };

            // [傳遞給 Fragment 的資料]
            struct Varyings
            {
                float4 positionHCS : SV_POSITION; // 螢幕裁剪空間位置
                float2 uv : TEXCOORD0;            // 傳遞 UV 座標
            };

            // [變數宣告區] CBUFFER 用於讓 URP 支援 Batching 提升效能
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _NoiseColor;
                float _NoiseScale;
                float _NoiseSpeed;
                float _NoiseIntensity;
            CBUFFER_END

            // [數學函數] 生成 0 到 1 之間的偽隨機黑白雜訊值
            float SimpleNoise(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            // [頂點著色器] 將 3D 模型頂點轉換為螢幕位置
            Varyings vert(Attributes input)
            {
                Varyings output;
                // 將物件空間點轉為螢幕空間點
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // [像素著色器] 計算每個像素最終顯示的顏色
            half4 frag(Varyings input) : SV_Target
            {
                // 利用時間 (_Time.y) 與速度計算步進值，讓雜訊產生跳動閃爍感
                float timeOffset = floor(_Time.y * _NoiseSpeed);
                
                // 根據密度調整 UV 規模，並加上時間位移
                float2 noiseUV = input.uv * _NoiseScale + float2(timeOffset, timeOffset * 0.5);

                // 計算當前像素點的隨機雜訊值 (0~1)
                float noiseVal = SimpleNoise(noiseUV);

                // 將黑色主色與雜訊顏色進行線性混合
                half4 finalColor = lerp(_BaseColor, _NoiseColor, noiseVal * _NoiseIntensity);

                return finalColor;
            }
            ENDHLSL
        }
    }
}