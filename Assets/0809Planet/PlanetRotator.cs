using UnityEngine;

public class PlanetRotator : MonoBehaviour
{
    [Header("需要放進來的物件")]
    // 觀眾的頭盔 (請把 OVRCameraRig 底下的 CenterEyeAnchor 拖曳到這裡)
    public Transform playerHead;   
    
    // 用來旋轉整顆星球的軸心空物件 (請把 Planet200 拖曳到這裡)
    public Transform planet200;    
    
    // 門口正中央的位置參考點 (請把 DoorAnchor 拖曳到這裡)
    public Transform doorLocation; 

    [Header("身體判定設定")]
    // 請把左邊 Hierarchy 裡的那個「body」物件拖曳到這裡！
    public GameObject virtualBody;

    [Header("旋轉數值設定 (你可以自行調整 Y 和 Z)")]
    // 需要背對門幾秒才能觸發轉動 (預設為 0.5 秒)
    public float lookAwayTime = 0.5f; 

    // 【新增】：讓你自訂 Y 軸的基底角度 (因為你的模型需要 -45 度，直接填在這裡！)
    public float baseYRotation = -45f; 

    // 【新增】：讓你自訂 Z 軸的基底角度
    public float baseZRotation = 0f;   

    // 防呆鎖：紀錄這次退回 MR 房間後，是不是已經轉過星球了
    private bool hasRotatedThisTime = false; 
    
    // 計時器
    private float timer = 0f; 

    void Update()
    {
        // -------------------------------------------------------------
        // 自動偵測：檢查這個 FBX 身體目前有沒有任何一個部分被畫出來
        // -------------------------------------------------------------
        bool isBodyVisible = false;

        if (virtualBody != null)
        {
            Renderer[] renderers = virtualBody.GetComponentsInChildren<Renderer>();
            
            foreach (Renderer r in renderers)
            {
                if (r.enabled == true)
                {
                    isBodyVisible = true;
                    break;
                }
            }
        }

        bool isBodyHidden = !isBodyVisible;

        // -------------------------------------------------------------
        // 計算視線夾角
        // -------------------------------------------------------------
        Vector3 directionToDoor = doorLocation.position - playerHead.position;
        float angle = Vector3.Angle(playerHead.forward, directionToDoor);

        // 印出除錯訊息
        Debug.Log("身體是否隱藏: " + isBodyHidden + " | 轉過了嗎: " + hasRotatedThisTime + " | 目前夾角: " + angle);

        // -------------------------------------------------------------
        // 觸發轉動邏輯
        // -------------------------------------------------------------
        if (isBodyHidden == true && hasRotatedThisTime == false)
        {
            if (angle > 90f)
            {
                timer = timer + Time.deltaTime;

                if (timer >= lookAwayTime)
                {
                    // 隨機生成 X 軸角度 (0 到 360)
                    float randomX = Random.Range(0f, 360f);

                    // 【關鍵修改】：X 軸用隨機亂數，但 Y 軸和 Z 軸會完美套用你上面自己設定的數值！
                    planet200.localEulerAngles = new Vector3(randomX, baseYRotation, baseZRotation);

                    hasRotatedThisTime = true; // 上鎖
                }
            }
            else
            {
                timer = 0f;
            }
        }
        else if (isBodyHidden == false)
        {
            hasRotatedThisTime = false; // 觀眾走出去後解鎖
            timer = 0f;
        }
    }
}