using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreepyCrawler : MonoBehaviour
{
    [Header("【移動設定】")]
    public float moveSpeed = 5f;       // 向前爬行的速度 (現在永遠不會減速！)
    public float turnSpeed = 100f;     // 轉彎的速度

    [Header("【AI 自動隨機漫遊】")]
    public bool enableAIWander = true; // 開啟後蟲子會自己亂走
    public float changeDirectionInterval = 2f; // 每隔幾秒換一次隨機方向
    private float aiTurnValue = 0f;    // 目前 AI 決定的轉彎數值 (-1 到 1)
    private float aiTimer = 0f;        // AI 換向的計時器

    [Header("【表面貼合設定】")]
    public LayerMask groundLayer;      // 設定哪些圖層是地形或牆壁
    public float heightFromMesh = 0.5f;// 頭部距離建築模型的基準距離
    public float alignSpeed = 10f;     // 貼合表面法線的旋轉速度
    public float frontFeelerLength = 1.5f; // 往前摸索的射線長度 (偵測牆壁)
    public float downFeelerLength = 3.0f;  // 往下摸索的射線長度 (偵測地板)

    [Header("【全速過彎防掉落設定】")]
    public float edgeLookAhead = 0.8f; // 探照燈距離：提早多遠發現邊緣
    public float edgeTurnBoost = 1.5f; // 遇到邊緣時的轉彎爆發力

    [Header("【MR網格平滑設定】")]
    public float probeRadius = 0.15f;  // 十字射線的擴散半徑

    void Update()
    {
        // 1. 處理基礎旋轉 (玩家控制與 AI 隨機漫遊)
        HandleRotation();

        // 2. 處理移動、全速過彎與表面貼合
        PerformMovementAndAlignment();
    }

    // --- 負責決定蟲子要往哪個方向轉 ---
    private void HandleRotation()
    {
        float finalTurnInput = 0f;
        float playerInput = Input.GetAxis("Horizontal");

        // 玩家手動控制優先
        if (Mathf.Abs(playerInput) > 0.1f)
        {
            finalTurnInput = playerInput;
        }
        // AI 隨機漫遊
        else if (enableAIWander)
        {
            aiTimer -= Time.deltaTime;
            if (aiTimer <= 0)
            {
                // 時間到，隨機決定新的旋轉方向
                aiTurnValue = Random.Range(-1f, 1f);
                aiTimer = Random.Range(0.5f, changeDirectionInterval);
            }
            finalTurnInput = aiTurnValue;
        }

        // 執行基礎旋轉
        transform.Rotate(0, finalTurnInput * turnSpeed * Time.deltaTime, 0);
    }

    // --- 負責永遠保持全速移動，並在邊緣強制轉向 ---
    private void PerformMovementAndAlignment()
    {
        Vector3 probeRayOrigin = transform.position + (transform.up * heightFromMesh * 1.5f);
        Vector3 targetNormal = transform.up;
        Vector3 targetSurfacePoint = transform.position;
        bool surfaceFound = false;

        // 1. 前方觸鬚 (牆壁偵測)
        if (GetAverageSurfaceData(probeRayOrigin, transform.forward, frontFeelerLength, out targetNormal, out targetSurfacePoint))
        {
            surfaceFound = true;
        }
        // 2. 下方觸鬚 (地板偵測)
        else if (GetAverageSurfaceData(probeRayOrigin, -transform.up, downFeelerLength, out targetNormal, out targetSurfacePoint))
        {
            surfaceFound = true;
        }

        // 狀況 A：觸鬚成功摸到 Plane 地面 (正常的移動與過彎)
        if (surfaceFound)
        {
            // --- 探照燈預判系統 ---
            // 往正前方預測一段距離，看看那裡有沒有地板
            Vector3 lookAheadPos = transform.position + (transform.forward * edgeLookAhead);
            Vector3 lookAheadRayOrigin = lookAheadPos + (targetNormal * heightFromMesh * 1.5f);
            
            bool isEdgeAhead = !Physics.Raycast(lookAheadRayOrigin, -targetNormal, out RaycastHit hit, downFeelerLength + 1f, groundLayer);

            // 如果前方是懸崖！
            if (isEdgeAhead)
            {
                // 強制接管 AI，讓牠死命往同一個方向轉彎！
                aiTurnValue = 1f;
                aiTimer = 0.5f;  

                // 加入轉向爆發力，讓全速前進的蟲子來得及轉過彎
                transform.Rotate(0, turnSpeed * edgeTurnBoost * Time.deltaTime, 0);
            }

            // --- 物理更新區塊 ---
            
            // 平滑調整高度
            Vector3 finalTargetPosition = targetSurfacePoint + (targetNormal * heightFromMesh);
            transform.position = Vector3.Lerp(transform.position, finalTargetPosition, alignSpeed * Time.deltaTime);
            
            // 平滑對齊表面法線
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, targetNormal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, alignSpeed * Time.deltaTime);
            
            // 正常狀況下保持全速前進
            transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
        }
        // 狀況 B：【新增防卡死安全線】當頭部不小心衝過頭、短暫踩空時
        else
        {
            // 不進行長距離倒車，而是每格畫面稍微往後退一點點 (Vector3.back)，把頭部縮回 Plane 範圍內
            transform.Translate(Vector3.back * (moveSpeed * 0.5f) * Time.deltaTime);
            
            // 往後退的同時將 AI 計時器清空，這樣只要頭部一退回地面，下一幀立刻重新隨機往前衝！
            aiTimer = 0f;
        }
    }

    // --- 法線平均化，發射 5 條射線來取得平滑的地形資料 (維持不變) ---
    private bool GetAverageSurfaceData(Vector3 origin, Vector3 direction, float length, out Vector3 avgNormal, out Vector3 avgPoint)
    {
        avgNormal = Vector3.zero;
        avgPoint = Vector3.zero;
        int hitCount = 0;

        Vector3 spreadUp, spreadRight;
        if (direction == transform.forward)
        {
            spreadUp = transform.up;
            spreadRight = transform.right;
        }
        else
        {
            spreadUp = transform.forward;
            spreadRight = transform.right;
        }

        Vector3[] offsets = new Vector3[]
        {
            Vector3.zero,
            spreadUp * probeRadius,
            -spreadUp * probeRadius,
            spreadRight * probeRadius,
            -spreadRight * probeRadius
        };

        foreach (Vector3 offset in offsets)
        {
            RaycastHit hit;
            if (Physics.Raycast(origin + offset, direction, out hit, length, groundLayer))
            {
                avgNormal += hit.normal;
                avgPoint += hit.point;  
                hitCount++;              
            }
        }

        if (hitCount > 0)
        {
            avgNormal = (avgNormal / hitCount).normalized;
            avgPoint = avgPoint / hitCount;
            return true;
        }

        return false;
    }
}