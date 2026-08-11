using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 這是用來讓 DreamSlope 反方向（與天空盒相反）旋轉的腳本
public class DreamSlopeRotator : MonoBehaviour
{
    // 這是一個公開的變數，讓你可以直接在 Unity 右側的 Inspector 面板中調整旋轉速度。
    // 預設值為 5.0f。因為程式碼的計算裡已經加上了負號，所以你只要在這裡輸入正數，它就會自動往反方向轉。
    public float rotationSpeed = 5.0f;

    // Update 函數會在遊戲執行的每一幀被呼叫一次，負責處理持續發生的旋轉動作。
    void Update()
    {
        // 讓掛載這個程式的物件（DreamSlope）沿著 Y 軸持續旋轉。
        // 注意這裡在 rotationSpeed 前面加上了負號（-），這就是讓它方向相反的關鍵。
        // 乘上 Time.deltaTime 是為了確保在不同效能的電腦上，旋轉速度都能保持平滑且速度一致。
        transform.Rotate(0, -rotationSpeed * Time.deltaTime, 0);
    }
}