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

    [Header("生物隨機管理器")]
    public ChimeraManager chimeraManager; 

    [Header("旋轉數值設定 (你可以自行調整)")]
    // 需要在 MR 裡背對門幾秒才能觸發轉動 (預設為 0.5 秒)
    public float lookAwayTime = 0.5f; 

    // 【新增】：必須在 VR 世界裡待滿幾秒，才能解鎖下一次的魔術？(預設 1 秒)
    public float unlockTime = 1.0f; 

    public float baseYRotation = -45f; 
    public float baseZRotation = 0f;   

    // 防呆鎖：紀錄這次退回 MR 房間後，是不是已經轉過星球了
    private bool hasRotatedThisTime = false; 
    
    // 背對門口的計時器
    private float lookAwayTimer = 0f; 

    // 【新增】：待在 VR 空間裡的計時器
    private float inVrTimer = 0f;

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

        // 當身體看不見時，代表觀眾在 MR 真實房間裡
        bool isBodyHidden = !isBodyVisible;

        // -------------------------------------------------------------
        // 計算視線夾角
        // -------------------------------------------------------------
        Vector3 directionToDoor = doorLocation.position - playerHead.position;
        float angle = Vector3.Angle(playerHead.forward, directionToDoor);

        // -------------------------------------------------------------
        // 觸發轉動邏輯與解鎖邏輯
        // -------------------------------------------------------------
        
        if (isBodyHidden == true)
        {
            // 如果觀眾在 MR 房間裡，就把「待在VR的計時器」歸零
            inVrTimer = 0f;

            // 檢查魔術是否還沒觸發 (還沒上鎖)
            if (hasRotatedThisTime == false)
            {
                // 檢查是否背對門口
                if (angle > 90f)
                {
                    lookAwayTimer = lookAwayTimer + Time.deltaTime;

                    if (lookAwayTimer >= lookAwayTime)
                    {
                        // 1. 隨機生成 X 軸角度
                        float randomX = Random.Range(0f, 360f);
                        planet200.localEulerAngles = new Vector3(randomX, baseYRotation, baseZRotation);

                        // 2. 告訴管理器「請給我一隻新生物！」
                        if (chimeraManager != null)
                        {
                            chimeraManager.RandomizeAndShowOne();
                        }

                        // 3. 成功觸發，把鎖鎖上！
                        hasRotatedThisTime = true; 
                    }
                }
                else
                {
                    // 如果中途轉回頭看門，背對計時器歸零
                    lookAwayTimer = 0f;
                }
            }
        }
        else if (isBodyHidden == false)
        {
            // 如果觀眾走進了 VR 虛擬世界裡 (身體出現了)
            
            // 先把「背對門口」的計時器歸零，避免誤判
            lookAwayTimer = 0f;

            // 檢查鎖是不是還鎖著
            if (hasRotatedThisTime == true)
            {
                // 【關鍵防護】：開始計算觀眾待在 VR 裡的時間
                inVrTimer = inVrTimer + Time.deltaTime;

                // 必須連續待滿 unlockTime (預設 1 秒) 才算真正進來，才能解鎖！
                if (inVrTimer >= unlockTime)
                {
                    hasRotatedThisTime = false; // 解鎖！準備迎接下一次退回 MR 的驚喜
                }
            }
        }
    }
}