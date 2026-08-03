using UnityEngine;

public class RotateDirectionalLight : MonoBehaviour
{
    // 這裡控制旋轉的速度：「幾分鐘轉一圈」
    // 預設為 5，代表在遊戲中需要花費 5 分鐘，這個光源才會順時針轉完完整的 360 度
    // 你可以直接在 Unity 編輯器 (Inspector) 中修改這個數字，不需要改程式碼
    public float minutesPerRotation = 5f;

    // Update 是 Unity 內建的方法，在遊戲進行時，每一幀 (Frame) 都會自動執行一次這個區塊內的程式
    void Update()
    {
        // 步驟 1：取得物件目前的旋轉角度
        // 將 X, Y, Z 的角度資訊先存入一個名為 currentRotation 的變數中
        Vector3 currentRotation = transform.eulerAngles;

        // 步驟 2：將「幾分鐘轉一圈」換算成 Unity 需要的「每秒轉動度數」
        // 完整一圈是 360 度。
        // minutesPerRotation * 60f 可以把分鐘換算成秒數。
        // 用 360 度除以總秒數，就能算出每一秒應該轉多少度。
        float rotationSpeed = 360f / (minutesPerRotation * 60f);

        // 步驟 3：讓 Y 軸持續增加角度來達到平滑的順時針旋轉
        // 乘上 Time.deltaTime 能確保不管電腦當下效能如何，旋轉速度都會保持平滑一致
        currentRotation.y += rotationSpeed * Time.deltaTime;

        // 步驟 4：強制固定 X 軸與 Z 軸的角度
        // 依照你的需求，確保 X 軸永遠維持在 30 度
        currentRotation.x = 30f;
        // 確保 Z 軸保持為 0，避免光源產生不預期的歪斜
        currentRotation.z = 0f;

        // 步驟 5：將計算好的新角度重新套用回 Directional Light 上
        transform.eulerAngles = currentRotation;
    }
}
