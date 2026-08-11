using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 這是用來旋轉天空盒（Sphere）的腳本
public class SkyboxRotator : MonoBehaviour
{
    // 這是一個公開的變數，讓你可以直接在 Unity 右側的 Inspector 面板中調整旋轉速度。
    // 預設值為 5.0f。數字越大轉得越快，如果設定為負數（例如 -5.0f）則會往反方向轉。
    public float rotationSpeed = 5.0f;

    // Update 函數會在遊戲執行的每一幀被呼叫一次，適合用來處理持續發生的動作（例如旋轉、移動）。
    void Update()
    {
        // 讓掛載這個程式的物件（也就是你的 360 galaxy 圓球）沿著 Y 軸持續旋轉。
        // 第一個參數是 X 軸，第二個是 Y 軸，第三個是 Z 軸。
        // 這裡把速度乘上 Time.deltaTime，是為了確保旋轉速度在不同效能的電腦上都能保持平滑且速度一致，不會因為幀數（FPS）高低而忽快忽慢。
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
    }
}