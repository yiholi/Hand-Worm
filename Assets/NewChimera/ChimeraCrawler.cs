using System.Collections.Generic;
using UnityEngine;

// 完美版：生在正中間、開局慢慢長大浮現、移動後才長出軌跡、支援多種 Prefab 混合
public class ChimeraCrawler : MonoBehaviour
{
    // ==========================================
    // 軌跡記錄資料結構
    // 用來記錄頭部走過的路徑，讓神經元節點知道該往哪裡跟隨
    // ==========================================
    private struct PathPoint
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector3 forward;
        public PathPoint(Vector3 p, Vector3 n, Vector3 f) { position = p; normal = n; forward = f; }
    }

    // ==========================================
    // 神經元節點資料結構
    // 記錄每一個單獨漂浮的小球的狀態與大小
    // ==========================================
    private class NeuralNode
    {
        public Transform transform;  // 節點的本體
        public float followDistance; // 這個節點應該跟在頭部後方多遠的位置
        public float noiseOffset;    // 亂數種子，用來讓每個節點有自己獨立的漂浮節奏
        public float currentScale;   // 用來記錄這顆球目前長到多大了
    }

    [Header("【神經元節點設定】")]
    // 【修改重點】這裡改成 GameObject 陣列，讓你可以拖曳多種不同的 Prefab 進來混搭
    public GameObject[] nodePrefabs;    
    public float nodeSize = 0.2f;       // 控制每一顆圓球的最終大小
    public float growSpeed = 3.0f;      // 開局時像氣泡一樣慢慢長大的速度
    public float nodeCatchUpSpeed = 12.0f; // 控制節點動態生長與歸位的速度
    public int nodeCount = 20;          // 節點數量
    public float swarmLength = 2.0f;    // 整個神經網路在軌跡上拖曳的總長度
    public float floatSpeed = 1.5f;     // 節點隨機漂浮、蠕動的速度
    public float floatRange = 0.4f;     // 節點偏離中心軌跡的漂浮範圍
    public float groundOffset = 0.1f;   // 節點距離牆壁的基礎高度

    [Header("【動態連線設定 (優化系統)】")]
    public Material lineMaterial;       // 連線的材質球
    public float connectDistance = 0.8f;// 觸發連線的距離
    public float lineWidth = 0.02f;     // 神經連線的粗細

    [Header("【移動設定 (沿用蟲蟲邏輯)】")]
    public float moveSpeed = 1.0f;
    public float turnSpeed = 55f;
    public LayerMask groundLayer;
    public float heightFromMesh = 0.25f;

    // ==========================================
    // 系統內部變數
    // ==========================================
    private List<PathPoint> pathHistory = new List<PathPoint>();
    private List<NeuralNode> nodes = new List<NeuralNode>();
    private List<LineRenderer> linePool = new List<LineRenderer>(); 
    private float minDistanceBetweenPoints = 0.05f;

    private Vector3 currentNormal = Vector3.up;
    private bool onSurface = false;
    private Vector3 detectedSurfacePoint;

    // ==========================================
    // 遊戲開始時的初始化
    // ==========================================
    void Start()
    {
        // 1. 【暴力對齊中心】只在你放的 X 和 Z 座標往下打射線，找地板貼上去
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, groundLayer))
        {
            transform.position = hit.point + hit.normal * heightFromMesh;
        }

        currentNormal = transform.up;

        // 2. 記錄第一筆起點資料
        pathHistory.Add(new PathPoint(transform.position, currentNormal, transform.forward));

        // 如果你忘記放 Prefab，這裡做個防呆警告避免遊戲當機
        if (nodePrefabs == null || nodePrefabs.Length == 0)
        {
            Debug.LogError("請在 Inspector 的 Node Prefabs 欄位放入至少一個模型！");
            return;
        }

        // 3. 生成所有的神經元節點
        for (int i = 0; i < nodeCount; i++)
        {
            // 【修改重點】從你給的 Prefab 清單中，隨機抽出一個模型來生成
            GameObject selectedPrefab = nodePrefabs[Random.Range(0, nodePrefabs.Length)];
            
            // 使用選中的模型生成
            GameObject obj = Instantiate(selectedPrefab, transform.position, Quaternion.identity);
            
            // 一出生的大小強制設為 0 (隱形狀態)，稍後讓它慢慢長大
            obj.transform.localScale = Vector3.zero; 
            obj.transform.parent = null; 

            NeuralNode n = new NeuralNode();
            n.transform = obj.transform;
            n.followDistance = Random.Range(0f, swarmLength); 
            n.noiseOffset = Random.Range(0f, 100f);
            n.currentScale = 0f; // 初始大小為 0
            
            nodes.Add(n);
        }

        // 4. 建立連線的物件池
        int maxLines = (nodeCount * nodeCount) / 3; 
        for (int i = 0; i < maxLines; i++)
        {
            GameObject lineObj = new GameObject("NeuralLine_" + i);
            lineObj.transform.parent = this.transform;
            
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = lineMaterial;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.positionCount = 2; 
            lr.useWorldSpace = true;
            lr.enabled = false; 
            
            linePool.Add(lr);
        }
    }

    // ==========================================
    // 每一幀處理移動與地形偵測
    // ==========================================
    void Update()
    {
        DetectSurface();
        HandleTurning();
        MoveAlongSurface();
        SnapToSurface();

        if (Vector3.Distance(transform.position, pathHistory[0].position) > minDistanceBetweenPoints)
        {
            pathHistory.Insert(0, new PathPoint(transform.position, currentNormal, transform.forward));
        }

        int maxHistory = Mathf.CeilToInt(swarmLength / minDistanceBetweenPoints) + 10;
        if (pathHistory.Count > maxHistory)
        {
            pathHistory.RemoveRange(maxHistory, pathHistory.Count - maxHistory);
        }
    }

    // ==========================================
    // 釋放動態！更新所有節點 3D 位置與連線
    // ==========================================
    void LateUpdate()
    {
        // 1. 計算每個節點應該漂浮到的 3D 立體位置
        foreach (NeuralNode n in nodes)
        {
            // 如果剛開局軌跡還沒長出來，會自動回傳中心起點
            PathPoint p = GetPointAtDistance(n.followDistance);

            // 產生三個維度 (X, Y, Z) 的隨機晃動感
            float noiseX = Mathf.PerlinNoise(Time.time * floatSpeed, n.noiseOffset) - 0.5f;
            float noiseY = Mathf.PerlinNoise(n.noiseOffset, Time.time * floatSpeed) - 0.5f;
            float noiseZ = Mathf.PerlinNoise(Time.time * floatSpeed, n.noiseOffset + 50f);

            Vector3 rightDir = Vector3.Cross(p.normal, p.forward).normalized;

            // 立體目標位置：當前依附點 + (基礎離地高度 + 立體垂直起伏) + 左右偏移 + 前後偏移
            Vector3 targetPos = p.position 
                              + (p.normal * (groundOffset + (noiseZ * floatRange))) 
                              + (rightDir * noiseX * floatRange)
                              + (p.forward * noiseY * floatRange);

            // 如果它才剛出生 (大小是 0)，就直接把位置設定到目標點，不准它有飛行的過程
            if (n.currentScale == 0f)
            {
                n.transform.position = targetPos;
            }
            else
            {
                // 如果已經出生了，就平滑活躍地游向這個立體新座標
                n.transform.position = Vector3.Lerp(n.transform.position, targetPos, Time.deltaTime * nodeCatchUpSpeed);
            }

            // 慢慢長大的機制
            if (n.currentScale < nodeSize)
            {
                // 使用 Lerp 讓球體從 0 慢慢膨脹到指定的 nodeSize 大小
                n.currentScale = Mathf.Lerp(n.currentScale, nodeSize, Time.deltaTime * growSpeed);
                n.transform.localScale = Vector3.one * n.currentScale;
            }
        }

        // 2. 處理節點之間的動態連線
        int activeLineIndex = 0; 

        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                float dist = Vector3.Distance(nodes[i].transform.position, nodes[j].transform.position);

                if (dist < connectDistance && activeLineIndex < linePool.Count)
                {
                    LineRenderer lr = linePool[activeLineIndex];
                    lr.enabled = true; 
                    lr.SetPosition(0, nodes[i].transform.position); 
                    lr.SetPosition(1, nodes[j].transform.position); 
                    activeLineIndex++;
                }
            }
        }

        for (int i = activeLineIndex; i < linePool.Count; i++)
        {
            linePool[i].enabled = false;
        }
    }

    // ==========================================
    // 在軌跡中尋找對應距離的座標點
    // ==========================================
    private PathPoint GetPointAtDistance(float targetDist)
    {
        if (pathHistory.Count < 2) return pathHistory[0];

        float accumulatedDist = 0f;
        for (int i = 0; i < pathHistory.Count - 1; i++)
        {
            float segDist = Vector3.Distance(pathHistory[i].position, pathHistory[i + 1].position);
            if (accumulatedDist + segDist >= targetDist)
            {
                float t = (targetDist - accumulatedDist) / segDist;
                return new PathPoint(
                    Vector3.Lerp(pathHistory[i].position, pathHistory[i + 1].position, t),
                    Vector3.Lerp(pathHistory[i].normal, pathHistory[i + 1].normal, t).normalized,
                    Vector3.Lerp(pathHistory[i].forward, pathHistory[i + 1].forward, t).normalized
                );
            }
            accumulatedDist += segDist;
        }
        return pathHistory[pathHistory.Count - 1];
    }

    // ==========================================
    // 地面偵測與吸附邏輯
    // ==========================================
    private void DetectSurface()
    {
        Vector3 origin = transform.position + currentNormal * heightFromMesh * 2f;
        
        if (Physics.Raycast(origin, -currentNormal, out RaycastHit hit, 3.5f, groundLayer))
        {
            currentNormal = Vector3.Slerp(currentNormal, hit.normal, 7f * Time.deltaTime).normalized;
            detectedSurfacePoint = hit.point;
            onSurface = true;
        }
        else
        {
            onSurface = false;
        }
    }

    private void HandleTurning()
    {
        if (Physics.Raycast(transform.position + currentNormal * heightFromMesh, transform.forward, 0.5f, groundLayer))
        {
            transform.Rotate(currentNormal, turnSpeed * Time.deltaTime * 2f);
        }
        else
        {
            float turnInput = Mathf.Sin(Time.time * 0.5f); 
            transform.rotation = Quaternion.AngleAxis(turnInput * turnSpeed * Time.deltaTime, currentNormal) * transform.rotation;
        }
    }

    private void MoveAlongSurface()
    {
        if (!onSurface) 
        {
            transform.position += -currentNormal * 1.5f * Time.deltaTime;
            return;
        }
        
        Vector3 moveDir = Vector3.ProjectOnPlane(transform.forward, currentNormal).normalized;
        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }

    private void SnapToSurface()
    {
        if (!onSurface) return;
        
        Vector3 targetPos = detectedSurfacePoint + currentNormal * heightFromMesh;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, 0.05f);
        
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, currentNormal) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 7f * Time.deltaTime);
    }
}