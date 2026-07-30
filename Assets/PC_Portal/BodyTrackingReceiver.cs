using UnityEngine;
using extOSC; // 引入 extOSC 函式庫，讓我們可以使用 OSC 的接收功能

public class BodyTrackingReceiver : MonoBehaviour
{
    // ==========================================
    // [宣告變數區塊]
    // 這裡用來設定接收器，以及設定我們要在哪一個「頻道」等待資料
    // ==========================================
    [Header("OSC 設定")]
    public OSCReceiver receiver; // 存放我們在場景中設定好的 OSC Receiver 組件
    
    // 注意：這裡的頻道名稱必須跟發送端 (Sender) 完全一模一樣，才能成功配對
    public string oscAddress = "/body/fullTracking"; 

    [Header("要連動的身體部位清單")]
    [Tooltip("⚠️ 極度重要：這裡關節的『順序』與『數量』，必須跟發送端一模一樣！")]
    public Transform[] trackingPoints; // 在 Unity 面板產生接收端的 Avatar 關節清單

    // ==========================================
    // [初始化區塊]
    // Start 函數只會在遊戲剛開始時執行一次
    // 我們要在這裡「註冊」我們的接收任務
    // ==========================================
    void Start()
    {
        // 1. 安全檢查：確認接收器有被正確拖曳進來
        if (receiver != null)
        {
            // 2. 綁定任務：告訴接收器「當你從 oscAddress 這個頻道收到訊息時，請去執行下面的 ReceiveTrackingData 功能」
            receiver.Bind(oscAddress, ReceiveTrackingData);
        }
    }

    // ==========================================
    // [處理接收訊息區塊]
    // 這個功能平時不會動，只有在「真的收到正確頻道的 OSC 訊息時」才會自動被觸發
    // ==========================================
    void ReceiveTrackingData(OSCMessage message)
    {
        // 1. 計算預期數量：每個關節有 6 個數值 (3個位置 + 3個旋轉)，所以總數量應該是 關節數量 x 6
        int expectedValueCount = trackingPoints.Length * 6;
        
        // 2. 安全格式檢查：直接檢查這包訊息裡面的「總數值數量」，是否符合我們預期的數量
        // 這個寫法最安全，可以完全避開舊版 GetValues 或新版 FindValues 的相容性警告
        if (message.Values.Count == expectedValueCount)
        {
            // 3. 迴圈解包：把清單裡的關節一個一個拿出來，套用對應的數值
            for (int i = 0; i < trackingPoints.Length; i++)
            {
                if (trackingPoints[i] != null)
                {
                    // 計算這個關節的資料在包裹裡的哪個位置 (例如第0個關節從索引0開始，第1個關節從索引6開始)
                    int dataIndex = i * 6;

                    // 依序把包裹裡面的數字拿出來，轉換成浮點數 (小數點格式)
                    float posX = message.Values[dataIndex].FloatValue;
                    float posY = message.Values[dataIndex + 1].FloatValue;
                    float posZ = message.Values[dataIndex + 2].FloatValue;
                    float rotX = message.Values[dataIndex + 3].FloatValue;
                    float rotY = message.Values[dataIndex + 4].FloatValue;
                    float rotZ = message.Values[dataIndex + 5].FloatValue;

                    // 把剛拿出來的數值，真正套用到這個 PC 場景的 Avatar 關節上
                    trackingPoints[i].position = new Vector3(posX, posY, posZ);
                    trackingPoints[i].eulerAngles = new Vector3(rotX, rotY, rotZ);
                }
            }
        }
    }
}