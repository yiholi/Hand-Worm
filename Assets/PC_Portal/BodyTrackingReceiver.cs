using UnityEngine;
using extOSC; // 引入 extOSC 函式庫

public class BodyTrackingReceiver : MonoBehaviour
{
    // ==========================================
    // [宣告變數區塊]
    // 這裡用來設定接收器，以及你要連動的 3D 模型關節清單
    // ==========================================
    [Header("OSC 設定")]
    public OSCReceiver receiver; 
    public string oscAddress = "/body/fullTracking"; 

    [Header("要連動的身體部位清單")]
    [Tooltip("⚠️ 數量與順序必須跟發送端一模一樣！")]
    public Transform[] trackingPoints; 

    // ==========================================
    // [初始化區塊]
    // 遊戲一開始時執行，負責把接收器打開並綁定頻道
    // ==========================================
    void Start()
    {
        // 安全檢查：確定有綁定接收器
        if (receiver != null)
        {
            // 告訴接收器：當收到 "/body/fullTracking" 的包裹時，交給 ReceiveTrackingData 這個功能處理
            receiver.Bind(oscAddress, ReceiveTrackingData);
        }
    }

    // ==========================================
    // [處理接收訊息區塊]
    // 每次收到一個封包（例如那 33 個關節的包裹）時，就會自動執行這裡
    // ==========================================
    void ReceiveTrackingData(OSCMessage message)
    {
        // 1. 安全檢查：確保包裹裡至少有一個標籤數字，如果是空包裹就跳過
        if (message.Values.Count < 1) return;

        // 2. 讀取標籤：拿出包裹裡的第一個數字，這樣我們就知道這包是從第幾個關節開始的 (例如 0 或是 33)
        int startIndex = message.Values[0].IntValue;

        // 3. 計算這包裹裡裝了幾個關節的資料
        // 因為第 1 個值是標籤，所以剩下的總數量要扣掉 1。
        // 每個關節有 6 個數字 (位置 XYZ + 旋轉 XYZ)，所以除以 6 就是關節的數量。
        int jointsInThisMessage = (message.Values.Count - 1) / 6;

        // 4. 開始把收到的數字，一個一個放回模型的關節上
        for (int i = 0; i < jointsInThisMessage; i++)
        {
            // 算出這個資料在我們清單中真正的索引位置 (標籤起始位置 + 目前處理到第幾個)
            int actualIndex = startIndex + i;

            // 確保這個索引沒有超出我們的清單長度，而且那個格子裡真的有關節模型
            if (actualIndex < trackingPoints.Length && trackingPoints[actualIndex] != null)
            {
                // 計算這筆關節資料在包裹中的位置 (記得要 +1 跳過第一個標籤)
                int dataIndex = 1 + (i * 6);

                // 依序拿出 6 個數值
                float posX = message.Values[dataIndex].FloatValue;
                float posY = message.Values[dataIndex + 1].FloatValue;
                float posZ = message.Values[dataIndex + 2].FloatValue;
                float rotX = message.Values[dataIndex + 3].FloatValue;
                float rotY = message.Values[dataIndex + 4].FloatValue;
                float rotZ = message.Values[dataIndex + 5].FloatValue;

                // 套用到關節的世界座標位置 (Position) 與世界座標旋轉 (EulerAngles)
                trackingPoints[actualIndex].position = new Vector3(posX, posY, posZ);
                trackingPoints[actualIndex].eulerAngles = new Vector3(rotX, rotY, rotZ);
            }
        }
    }
}