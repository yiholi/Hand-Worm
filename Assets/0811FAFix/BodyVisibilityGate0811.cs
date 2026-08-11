using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 控制玩家身體顯示狀態的腳本。
/// 當玩家身體完全在 MR 空間內時隱藏，有任何部位超出時顯示。
/// </summary>
public class BodyVisibilityGate0811 : MonoBehaviour
{
    [Header("References (參考設定)")]
    [Tooltip("角色的根物件 (Body)。它的渲染器會被開關，骨骼會被監測。")]
    [SerializeField] private GameObject bodyRoot;

    // 【修改重點】把這個欄位加回來了！讓你可以把做好的 Collider 拖曳進來
    [Tooltip("代表 MR 空間的碰撞器 (Box Collider)。只要身體超出這個範圍就會顯示。")]
    [SerializeField] private Collider mrVolume;

    [Header("Performance Settings (效能設定)")]
    [Tooltip("每秒執行幾次邊界檢查？預設 20 次。數字越小越省效能。")]
    [SerializeField] private float checksPerSecond = 20f;

    // 系統內部使用的變數，不需要顯示在編輯器中
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Transform> testPoints = new List<Transform>();
    
    private bool currentlyVisible = true;
    private float checkTimer = 0f; // 用來計算時間間隔的計時器

    void Awake()
    {
        // 2. 準備角色的渲染器 (用來隱藏/顯示) 和骨骼節點 (用來偵測位置)
        if (bodyRoot != null)
        {
            // 抓取所有的 Renderer (模型外觀)
            bodyRoot.GetComponentsInChildren(true, renderers);

            // 抓取所有的 SkinnedMeshRenderer 來獲得骨骼
            var skinned = new List<SkinnedMeshRenderer>();
            bodyRoot.GetComponentsInChildren(true, skinned);
            foreach (var smr in skinned)
            {
                if (smr.bones != null)
                {
                    foreach (var bone in smr.bones)
                    {
                        if (bone != null && !testPoints.Contains(bone))
                        {
                            // 把找到的骨骼加入測試清單
                            testPoints.Add(bone); 
                        }
                    }
                }
            }

            // 如果這個模型沒有骨骼，就退一步把所有子物件當作測試點
            if (testPoints.Count == 0)
            {
                var all = new List<Transform>();
                bodyRoot.GetComponentsInChildren(true, all);
                testPoints.AddRange(all);
            }
        }

        // 3. 遊戲一開始時，預設將身體隱藏 (假設玩家一開始就站在 MR 範圍內)
        SetVisible(false);
    }

    void Update()
    {
        // 如果檢查頻率設定為 0，直接跳出不執行，避免發生錯誤
        if (checksPerSecond <= 0f) return;

        // 計算每次檢查需要間隔幾秒 (例如每秒 20 次 = 每 0.05 秒檢查一次)
        float interval = 1f / checksPerSecond;

        // 讓計時器不斷增加 (Time.deltaTime 是上一幀到這一幀所花費的時間)
        checkTimer += Time.deltaTime;

        // 當計時器超過了我們設定的間隔時間，就執行一次範圍檢查
        if (checkTimer >= interval)
        {
            // 將計時器扣掉間隔時間，重新開始下一輪計算
            checkTimer -= interval;

            // 呼叫 AnyPartOutside 檢查是否在外面，並根據結果設定顯示或隱藏
            bool isOutside = AnyPartOutside();
            SetVisible(isOutside);
        }
    }

    /// <summary>
    /// 檢查是否有任何部位 (骨骼) 超出碰撞體範圍
    /// </summary>
    private bool AnyPartOutside()
    {
        // 如果你忘記在面板上指定 Collider，就當作沒有東西超出範圍，不執行顯示
        if (mrVolume == null) return false;

        // 迴圈檢查清單裡的每一個骨骼點
        for (int i = 0; i < testPoints.Count; i++)
        {
            Transform t = testPoints[i];
            if (t == null) continue;

            Vector3 p = t.position;
            
            // 使用 ClosestPoint 檢查點是否在碰撞體外部
            // 如果回傳的座標不等於原本骨骼的座標，代表這個骨骼超出了方塊邊界
            if (mrVolume.ClosestPoint(p) != p)
            {
                return true; // 只要有任何一個點在外面，立刻回傳 true
            }
        }
        
        // 如果全部檢查完都沒事，代表整個人都在方塊內部
        return false;
    }

    /// <summary>
    /// 控制整個身體模型的顯示或隱藏
    /// </summary>
    public void SetVisible(bool visible)
    {
        // 如果現在的狀態跟準備要切換的狀態一樣 (例如已經隱藏了又要隱藏)，就直接跳出，節省效能
        if (visible == currentlyVisible) return;
        
        currentlyVisible = visible;

        // 利用迴圈把所有的 Renderer 開啟 (顯示) 或關閉 (隱藏)
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }
}