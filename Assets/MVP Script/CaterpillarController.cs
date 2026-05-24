using System.Collections.Generic;
using UnityEngine;

public class CaterpillarController : MonoBehaviour
{
    // 建立一個結構來存放頭部走過的足跡 (包含位置與表面的法線方向)
    private struct PathPoint
    {
        public Vector3 position;
        public Vector3 normal;
        public PathPoint(Vector3 p, Vector3 n) { position = p; normal = n; }
    }

    [Header("【身體結構】")]
    public GameObject segmentPrefab;   // 身體節點的預製體 (Prefab)
    public int startSegments = 6;      // 一開始要生成幾節身體
    public float segmentSpacing = 0.5f;// 每一節身體之間的基礎距離
    
    // --- 新增：按 Play 前可手動微調的身體縮放比例 ---
    [Tooltip("手動填寫身體大小比例，預設為 1")]
    public float bodyScale = 1f;       

    [Header("【Sine Wave 擺動 (蛇行設定)】")]
    public float waveAmplitude = 0.08f;// 波浪的基礎幅度 (左右扭動的寬度)
    public float waveFrequency = 3f;   // 波浪的頻率 (扭動的速度)
    public float phaseOffset = 0.8f;   // 每一節身體之間的時間差，讓身體有接續扭動的感覺

    [Header("【貼地】")]
    public LayerMask groundLayer;      // 地板的圖層
    public float groundOffset = 0.5f;  // 身體距離地面的基礎高度

    // 存放所有生成的身體節點
    private List<Transform> segments = new List<Transform>();
    // 存放頭部走過的歷史軌跡
    private List<PathPoint> pathHistory = new List<PathPoint>();
    // 每移動多遠記錄一次軌跡 (數值越小，軌跡越精細)
    private float minDistanceBetweenPoints = 0.02f; 

    void Start()
    {
        // --- 自動縮放乘數優化 ---
        // 為了讓身體完美等比例放大縮小，間距、高度與波動寬度都要乘上 bodyScale
        segmentSpacing *= bodyScale;
        groundOffset *= bodyScale;
        waveAmplitude *= bodyScale;

        // 遊戲剛開始時，先記錄頭部的第一個軌跡點
        pathHistory.Add(new PathPoint(transform.position, transform.up));

        // 根據設定的數量，生成初始的身體節點
        for (int i = 0; i < startSegments; i++)
        {
            // 計算初始位置 (排在頭部的後面)
            Vector3 pos = transform.position + Vector3.back * (i + 1) * segmentSpacing;
            GameObject seg = Instantiate(segmentPrefab, pos, Quaternion.identity);
            
            // 【修改處】將複製出來的身體球體縮放比例設定為我們指定的 bodyScale
            seg.transform.localScale = Vector3.one * bodyScale;

            // 解除父子關係，讓身體節點可以獨立在世界座標中移動
            seg.transform.parent = null;
            segments.Add(seg.transform);
        }
    }

    void Update()
    {
        // --- 負責記錄頭部的歷史軌跡 ---
        Vector3 currentHeadPos = transform.position;
        
        // 如果頭部目前的位置，距離上一個記錄點已經超過了設定的最短距離
        if (Vector3.Distance(currentHeadPos, pathHistory[0].position) > minDistanceBetweenPoints)
        {
            // 抓取頭部當前的法線 (頭部的上方)
            Vector3 headNormal = transform.up; 
            
            // 將最新的足跡插進清單的最前面 (索引值 0)
            pathHistory.Insert(0, new PathPoint(currentHeadPos, headNormal));
        }

        // --- 限制歷史軌跡清單的長度，保護電腦記憶體 ---
        // 計算最多需要保留幾個點 (身體節數 * 間距的 3 倍長度)
        int maxHistory = Mathf.CeilToInt((segments.Count * segmentSpacing) / minDistanceBetweenPoints) * 3;
        
        // 如果清單超過了最大長度，就把最舊(最後面)的紀錄刪除
        if (pathHistory.Count > maxHistory)
        {
            pathHistory.RemoveRange(maxHistory, pathHistory.Count - maxHistory);
        }

        // 按下空白鍵時，動態增加一節身體
        if (Input.GetKeyDown(KeyCode.Space))
            AddNewSegment();
    }

    void LateUpdate()
    {
        // 如果沒有身體或沒有軌跡，就不做任何事
        if (segments.Count == 0 || pathHistory.Count == 0) return;

        // --- 核心：利用歷史軌跡點來精確控制每一節身體 ---
        for (int i = 0; i < segments.Count; i++)
        {
            // 計算這一節身體應該距離頭部多遠的路程
            float targetDistance = (i + 1) * segmentSpacing;
            
            // 從歷史軌跡點中，找出最符合這個距離的座標與法線
            PathPoint samplePoint = GetPointAtDistance(targetDistance);

            // 計算基礎位置：足跡點的位置 + 沿著法線方向往上浮起一定的高度
            Vector3 basePos = samplePoint.position + samplePoint.normal * groundOffset;
            Vector3 normal = samplePoint.normal;

            // 計算旋轉朝向（每一節身體都看向它的前一節，第一節則是看向頭部）
            Vector3 targetLookPos = (i == 0) ? transform.position : segments[i - 1].position;
            Vector3 dir = targetLookPos - segments[i].position;

            // 如果有移動距離，就進行平滑轉向
            if (dir.sqrMagnitude > 0.0001f)
            {
                // Quaternion.LookRotation 可以確保身體面向前方，同時「上方」對齊表面的法線
                segments[i].rotation = Quaternion.Slerp(
                    segments[i].rotation,
                    Quaternion.LookRotation(dir.normalized, normal),
                    Time.deltaTime * 20f);
            }

            // --- 核心：蛇行 S 型擺動 ---
            // 計算 Sine Wave 的數值 (介於 -1 到 1 之間)
            float wave = Mathf.Sin(Time.time * waveFrequency + i * phaseOffset);
            
            // 讓身體沿著它自己的「右方」進行偏移，產生左右蛇行的效果！
            segments[i].position = basePos + segments[i].right * wave * waveAmplitude;
        }
    }

    // 在歷史路徑中，沿著足跡線段「數」出固定距離的坐標點 (用來找出每一節身體應該在的位置)
    private PathPoint GetPointAtDistance(float targetDist)
    {
        // 如果軌跡不夠長，直接回傳最新的點
        if (pathHistory.Count < 2) return pathHistory[0];

        float accumulatedDist = 0f;
        // 跑遍歷史軌跡，把點與點之間的距離加總起來
        for (int i = 0; i < pathHistory.Count - 1; i++)
        {
            float segDist = Vector3.Distance(pathHistory[i].position, pathHistory[i + 1].position);
            
            // 如果加總起來的距離剛好超過了我們目標的距離
            if (accumulatedDist + segDist >= targetDist)
            {
                // 計算比例 (t)，進行線性內插 (Lerp) 來找出精確的位置，這樣移動才會極度流暢
                float t = (targetDist - accumulatedDist) / segDist;
                Vector3 mixedPos = Vector3.Lerp(pathHistory[i].position, pathHistory[i + 1].position, t);
                Vector3 mixedNorm = Vector3.Lerp(pathHistory[i].normal, pathHistory[i + 1].normal, t).normalized;
                return new PathPoint(mixedPos, mixedNorm);
            }
            accumulatedDist += segDist;
        }
        
        // 如果整個歷史軌跡的長度都不夠，就回傳最舊的一個點
        return pathHistory[pathHistory.Count - 1];
    }

    // 在尾巴新增一節身體的功能
    public void AddNewSegment()
    {
        // 如果已經有身體了，新節點就生成在最後一節的位置；如果沒有，就生成在頭部位置
        Vector3 pos = segments.Count > 0 ? segments[^1].position : transform.position;
        GameObject seg = Instantiate(segmentPrefab, pos, Quaternion.identity);
        
        // 【修改處】動態生成的身體節點，也同樣必須強制同步我們設定的 bodyScale 大小
        seg.transform.localScale = Vector3.one * bodyScale;

        // 解除父子關係並加入清單
        seg.transform.parent = null;
        segments.Add(seg.transform);
    }
}