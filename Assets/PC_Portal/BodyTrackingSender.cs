using UnityEngine;
using extOSC; // 引入 extOSC 函式庫

public class BodyTrackingSender : MonoBehaviour
{
    // ==========================================
    // [宣告變數區塊]
    // 這裡用來設定發送器，以及建立一個「清單」來存放你想追蹤的所有關節
    // ==========================================
    [Header("OSC 設定")]
    public OSCTransmitter transmitter; 
    public string oscAddress = "/body/fullTracking"; // 我們統一用這一個頻道來傳送全身資料

    [Header("要追蹤的身體部位清單")]
    [Tooltip("請在這裡設定數量，並依序將 MR 骨架的關節拖曳進來")]
    public Transform[] trackingPoints; // 這會在 Unity 面板產生一個清單，讓你把關節拖進去

    // ==========================================
    // [持續更新區塊]
    // 每一幀都會自動執行，把清單裡所有關節的「位置」與「旋轉」打包發送
    // ==========================================
    void Update()
    {
        // 1. 安全檢查：如果沒放發送器，或是清單是空的，就不要執行，避免報錯
        if (transmitter == null || trackingPoints == null || trackingPoints.Length == 0)
        {
            return; 
        }

        // 2. 建立新訊息：準備一個大包裹，貼上我們設定好的頻道標籤
        OSCMessage message = new OSCMessage(oscAddress);

        // 3. 迴圈處理：讓程式自動把清單裡的關節一個一個拿出來處理
        for (int i = 0; i < trackingPoints.Length; i++)
        {
            Transform joint = trackingPoints[i];

            if (joint != null)
            {
                // 如果這個欄位有放關節，就把它的 3 個位置和 3 個旋轉角度塞進包裹裡
                // 位置 (Position)
                message.AddValue(OSCValue.Float(joint.position.x));
                message.AddValue(OSCValue.Float(joint.position.y));
                message.AddValue(OSCValue.Float(joint.position.z));
                
                // 旋轉角度 (Euler Angles，即我們在 Unity 面板看到的 X, Y, Z 旋轉值)
                message.AddValue(OSCValue.Float(joint.eulerAngles.x));
                message.AddValue(OSCValue.Float(joint.eulerAngles.y));
                message.AddValue(OSCValue.Float(joint.eulerAngles.z));
            }
            else
            {
                // 如果你不小心在清單裡留了空位，我們就塞入 0，確保資料的排列順序不會亂掉
                message.AddValue(OSCValue.Float(0));
                message.AddValue(OSCValue.Float(0));
                message.AddValue(OSCValue.Float(0));
                message.AddValue(OSCValue.Float(0));
                message.AddValue(OSCValue.Float(0));
                message.AddValue(OSCValue.Float(0));
            }
        }

        // 4. 發送訊息：把這包裝滿全身數據的超級大包裹發射出去
        transmitter.Send(message);
    }
}