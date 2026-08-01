using UnityEngine;
using extOSC; // 引入 extOSC 函式庫

public class BodyTrackingSender : MonoBehaviour
{
    // ==========================================
    // [宣告變數區塊]
    // 這裡用來設定你在 Unity 面板上拉好的東西
    // ==========================================
    [Header("OSC 設定")]
    public OSCTransmitter transmitter; 
    public string oscAddress = "/body/fullTracking"; 

    [Header("發送頻率設定")]
    [Tooltip("對應你筆記的 threshold，設定幾秒發送一次資料。若要順暢不卡頓，建議在面板改為 0.02")]
    public float threshold = 1.0f; 

    [Header("要追蹤的身體部位清單")]
    public Transform[] trackingPoints; 

    // 🌟 這裡就是改成「兩個封包」的關鍵！
    // 總共 66 個關節，一次裝 33 個，程式就會自動分成 2 個封包發送
    private int batchSize = 33; 
    
    // 記錄上一次發送的時間 (你筆記裡的 t0)
    private float lastSendTime = 0f; 
    private bool hasShownError = false;

    // ==========================================
    // [持續更新區塊]
    // 每一幀都會自動執行，檢查時間到了沒
    // ==========================================
    void Update()
    {
        // 1. 安全檢查：確認發送器和清單都有綁定，避免程式報錯當機
        if (transmitter == null || trackingPoints == null || trackingPoints.Length == 0)
        {
            if (!hasShownError) { Debug.LogWarning("❌ 錯誤：發送端忘記綁定 OSC Transmitter 或是清單空的！"); hasShownError = true; }
            return; 
        }
        hasShownError = false;

        // 2. 獲取當前的時間 (你筆記裡的 t1)
        float currentTime = Time.time;

        // 3. 核心邏輯：如果 (現在時間 - 上次發送時間) 大於設定的門檻
        if (currentTime - lastSendTime > threshold)
        {
            // 分批處理迴圈：每次跳 33 個步伐
            // 第一次迴圈：處理第 0 ~ 32 個關節 (第一包)
            // 第二次迴圈：處理第 33 ~ 65 個關節 (第二包)
            for (int startIndex = 0; startIndex < trackingPoints.Length; startIndex += batchSize)
            {
                // 準備一個新包裹
                OSCMessage message = new OSCMessage(oscAddress);

                // 包裹的第一個資料，放一個「整數標籤」，告訴接收端這包是從第幾個關節開始
                message.AddValue(OSCValue.Int(startIndex));

                // 計算這包貨車最多能裝到第幾個關節 (確保不會超過 66 這個總數)
                int endIndex = Mathf.Min(startIndex + batchSize, trackingPoints.Length);

                // 把這一批次 (33個) 的關節資料裝進去
                for (int i = startIndex; i < endIndex; i++)
                {
                    Transform joint = trackingPoints[i];

                    if (joint != null)
                    {
                        // 裝入世界座標位置 (Position)
                        message.AddValue(OSCValue.Float(joint.position.x));
                        message.AddValue(OSCValue.Float(joint.position.y));
                        message.AddValue(OSCValue.Float(joint.position.z));
                        
                        // 裝入世界座標旋轉 (EulerAngles)
                        message.AddValue(OSCValue.Float(joint.eulerAngles.x));
                        message.AddValue(OSCValue.Float(joint.eulerAngles.y));
                        message.AddValue(OSCValue.Float(joint.eulerAngles.z));
                    }
                    else
                    {
                        // 如果有空位，補上 6 個 0，確保資料排列順序不會錯亂
                        message.AddValue(OSCValue.Float(0));
                        message.AddValue(OSCValue.Float(0));
                        message.AddValue(OSCValue.Float(0));
                        message.AddValue(OSCValue.Float(0));
                        message.AddValue(OSCValue.Float(0));
                        message.AddValue(OSCValue.Float(0));
                    }
                }

                // 發送這個裝了 33 個關節的封包
                transmitter.Send(message);
            }

            // 4. 重置計時點：把當下時間記錄下來，當作下一次計算的起點
            lastSendTime = currentTime;
        }
    }
}