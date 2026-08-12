using System.Collections.Generic;
using UnityEngine;

// 這個腳本用來偵測「Meta Eye Gaze Block」的視線是否注視指定的 Quad
// 並且在身體隱藏時，讓 Planet 隨機改變 X 軸旋轉
public class EyeGazePlanetController : MonoBehaviour
{
    [Header("眼睛與視線設定")]
    [Tooltip("請將 Hierarchy 裡的 [BuildingBlock] Eye Gaze Left 或 Right 拖入這裡")]
    public Transform eyeTransform; // 這裡負責接收 Meta Eye Gaze Block 的方向
    
    [Tooltip("視線發射的最大距離")]
    public float rayDistance = 10.0f;
    
    [Tooltip("可以觸發事件的 Quad 清單，請把 EyeQuads 底下的 Quad 都拉進來")]
    public List<GameObject> targetQuads; // 用來存放所有可以被眼睛觸發的方塊

    [Header("身體判定設定")]
    [Tooltip("身體的根物件 (body)，腳本會透過它來檢查身體現在有沒有被渲染出來")]
    public GameObject bodyRoot; // 檢查身體是否隱藏的關鍵物件

    [Header("星球設定")]
    [Tooltip("要旋轉的大球 (Planet)")]
    public Transform planetTransform; // 準備被旋轉的星球

    [Header("效能設定")]
    [Tooltip("每秒發射幾次視線射線？預設 20 次。數字越小越省效能。")]
    public float checksPerSecond = 20f; // 控制每秒判定的次數，節省 Quest 效能

    // 用來記錄前一次檢查時「是不是已經在看 Quad」的狀態，避免重複觸發
    private bool wasLookingAtQuad = false;

    // 用來計算時間間隔的內部計時器
    private float checkTimer = 0f;

    void Update()
    {
        // 防呆檢查：確保你在 Inspector 面板上該拉的物件都有拉進來，避免遊戲報錯
        if (eyeTransform == null || planetTransform == null || bodyRoot == null) 
        {
            return;
        }

        // 如果檢查頻率設定為 0 或是負數，直接跳出不執行，避免數學計算出錯
        if (checksPerSecond <= 0f) return;

        // 計算每次檢查需要間隔幾秒 (例如每秒 20 次 = 每 0.05 秒檢查一次)
        float interval = 1f / checksPerSecond;

        // 讓計時器不斷增加 (Time.deltaTime 是上一幀到這一幀所花費的時間)
        checkTimer += Time.deltaTime;

        // 當計時器超過了我們設定的間隔時間，才真正執行一次射線檢查
        if (checkTimer >= interval)
        {
            // 將計時器扣掉間隔時間，重新開始下一輪的倒數
            checkTimer -= interval;

            // ==========================================
            // 條件一：判斷身體是否「沒有被渲染出來」(隱藏狀態)
            // ==========================================
            // 抓取 bodyRoot 底下的 Renderer，檢查它是不是被關閉了
            Renderer bodyRenderer = bodyRoot.GetComponentInChildren<Renderer>();
            bool isBodyHidden = (bodyRenderer != null && bodyRenderer.enabled == false);

            // 預設這一次的檢查，眼睛沒有在看 Quad
            bool isCurrentlyLookingAtQuad = false;

            // ==========================================
            // 條件二：當身體是隱藏的，才利用 Meta Eye Gaze 發射射線
            // ==========================================
            if (isBodyHidden)
            {
                RaycastHit hit;

                // 發射物理射線：從 Meta Eye Gaze 的位置 (position) 往它的前方 (forward) 發射
                // 因為 Meta Block 已經幫我們轉動了這個物件，所以 forward 就是眼球真正的視線方向
                if (Physics.Raycast(eyeTransform.position, eyeTransform.forward, out hit, rayDistance))
                {
                    // 檢查打到的物件，有沒有在 targetQuads 清單裡面
                    if (targetQuads.Contains(hit.collider.gameObject))
                    {
                        // 如果眼睛確實看著 Quad，就把狀態標記為 true
                        isCurrentlyLookingAtQuad = true;
                    }
                }
            }

            // ==========================================
            // 執行結果：如果這次檢查有看 Quad，且上次沒看，就改變 Planet 的 rotationX
            // ==========================================
            if (isCurrentlyLookingAtQuad && !wasLookingAtQuad)
            {
                // 隨機產生 0 到 360 之間的數字
                float randomX = Random.Range(0f, 360f);

                // 取得 Planet 目前的旋轉數值 (保留 Y 和 Z 不變)
                Vector3 currentRotation = planetTransform.localEulerAngles;

                // 將 Planet 的旋轉更新：X 帶入隨機數字，Y 和 Z 照舊
                planetTransform.localEulerAngles = new Vector3(randomX, currentRotation.y, currentRotation.z);
                
                // 在 Console 印出訊息，讓你知道成功觸發了
                Debug.Log("眼球追蹤觸發成功！Planet 的 Rotation X 變成：" + randomX);
            }

            // 把這一次的觀看狀態存起來，留給下一次 (0.05 秒後) 比對
            wasLookingAtQuad = isCurrentlyLookingAtQuad;
        }
    }
}