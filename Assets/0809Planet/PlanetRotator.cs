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

    [Header("設定數值")]
    // 需要背對門幾秒才能觸發轉動 (預設為 0.5 秒)
    public float lookAwayTime = 0.5f; 

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
            // 自動尋找這個身體裡面所有的 Renderer (外觀渲染器)
            Renderer[] renderers = virtualBody.GetComponentsInChildren<Renderer>();
            
            // 只要其中有一個 Renderer 是顯示的 (enabled == true)，就代表身體看得見
            foreach (Renderer r in renderers)
            {
                if (r.enabled == true)
                {
                    isBodyVisible = true;
                    break;
                }
            }
        }

        // 如果「身體看不見 (isBodyVisible == false)」就代表觀眾在 MR 房間裡！
        bool isBodyHidden = !isBodyVisible;

        // -------------------------------------------------------------
        // 計算視線夾角
        // -------------------------------------------------------------
        Vector3 directionToDoor = doorLocation.position - playerHead.position;
        float angle = Vector3.Angle(playerHead.forward, directionToDoor);

        // 印出除錯訊息，讓你在 Console 看到目前狀態
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
                    float randomX = Random.Range(0f, 360f);
                    planet200.localEulerAngles = new Vector3(randomX, 0f, 0f);
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