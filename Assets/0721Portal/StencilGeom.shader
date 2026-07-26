// 定義這個 Shader 的路徑與名稱，這決定了你在 Unity 材質球 (Material) 的下拉選單裡要去哪裡找它
Shader "Examples/Stencil"
{
    // Properties 區塊：這裡的變數會顯示在 Unity 右側的 Inspector 面板上，讓你可以直接調整數值
    Properties
    {
        // 定義一個名為 _StencilID 的變數，顯示名稱為 "Stencil ID"
        // [IntRange] 限制它只能是整數，範圍是 0 到 255，預設值為 0。這就是我們的「印章號碼」
        [IntRange] _StencilID ("Stencil ID", Range(0, 255)) = 0
    }
    
    // SubShader 區塊：包含 Shader 實際的渲染設定與繪圖步驟
    SubShader
    {
        // Tags 區塊：用來告訴 Unity 的系統這個 Shader 屬於什麼類型、應該要在什麼順序被畫出來
        Tags
        {
            // 標示這是一個不透明 (Opaque) 的材質
            "RenderType" = "Opaque"
            // 設定渲染順序 (Queue) 為 Geometry，確保它在正常的不透明物件階段被處理
            "Queue" = "Geometry"
            // 聲明這個 Shader 是專門給通用渲染管線 (URP) 使用的
            "RenderPipeline" = "UniversalPipeline"
        }

        // Pass 區塊：代表一次實際的畫圖流程 (在這裡，我們要畫出隱形的印章)
        Pass
        {
            // Blend Zero One：顏色混合模式。這行程式碼非常關鍵！
            // Zero 代表「完全不顯示這個物件原本的顏色」，One 代表「完全保留背景已經畫好的顏色」
            // 綜合起來，這就是讓這個面具變成「完全隱形」的魔法設定
            Blend Zero One
            
            // ZWrite Off：關閉深度寫入。這代表這個隱形面具在空間中不會實體擋住後面別的東西
            ZWrite Off

            // Stencil 區塊：控制我們剛剛辛苦設定的底層「印章系統」
            Stencil
            {
                // 告訴系統：我們這次要使用的印章編號，就是你在面板上設定的 _StencilID
                Ref [_StencilID]
                
                // Comp Always：比較條件設為「總是通過」。代表不管畫面原本有沒有其他印章，我們都強行蓋上去
                Comp Always
                
                // Pass Replace：如果蓋章成功了，就把這個地方原本的印章號碼「替換 (Replace)」成我們的新號碼
                Pass Replace
                
                // Fail Keep：如果蓋章失敗了，就「保留 (Keep)」原本的狀態不變
                Fail Keep
            }
        }
    }
}