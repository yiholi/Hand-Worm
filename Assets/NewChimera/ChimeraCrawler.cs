using System.Collections.Generic;
using UnityEngine;

// 這是選項 A (神經元網路) 的乾淨修復版
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
    // 記錄每一個單獨漂浮的小球的狀態
    // ==========================================
    private class NeuralNode
    {
        public Transform transform;  // 節點的本體
        public float followDistance; // 這個節點應該跟在頭部後方多遠的位置
        public float noiseOffset;    // 亂數種子，用來讓每個節點有自己獨立的漂浮節奏
    }

    [Header("【神經元節點設定】")]
    public GameObject nodePrefab;       // 神經元的形狀 (圓球)
    public float nodeSize = 0.05f;      // 【新增】控制每一顆圓球的大小，可以在 Inspector 隨時調整
    public int nodeCount = 20;          // 節點數量
    public float swarmLength = 3.0f;    // 整個神經網路在軌跡上拖曳的總長度
    public float floatSpeed = 1.5f;     // 節點隨機漂浮、蠕動的速度
    public float floatRange = 0.4f;     // 節點偏離中心軌跡的漂浮範圍
    public float groundOffset = 0.1f;   // 節點距離牆壁的高度

    [Header("【動態連線設定 (優化系統)】")]
    public Material lineMaterial;       // 連線的材質球
    public float connectDistance = 0.8f;// 觸發連線的距離：節點間距小於這個數值就會牽線
    public float lineWidth = 0.02f;     // 神經連線的粗細

    [Header("【移動設定 (沿用蟲蟲邏輯)】")]
    public float moveSpeed = 0.8f;      // 前進速度
    public float turnSpeed = 55f;       // 轉彎速度
    public LayerMask groundLayer;       // 偵測地面的圖層 (Ground)
    public float heightFromMesh = 0.25f;// 身體距離表面的高度基準

    // ==========================================
    // 系統內部變數 (不需要在介面上調整)
    // ==========================================
    private List<PathPoint> pathHistory = new List<PathPoint>();
    private List<NeuralNode> nodes = new List<NeuralNode>();
    private List<LineRenderer> linePool = new List<LineRenderer>(); // 【效能優化】連線物件池
    private float minDistanceBetweenPoints = 0.05f;

    private Vector3 currentNormal = Vector3.up;
    private bool onSurface = false;
    private Vector3 detectedSurfacePoint;

    // ==========================================
    // 遊戲開始時的初始化
    // ==========================================
    void Start()
    {
        currentNormal = transform.up;
        pathHistory.Add(new PathPoint(transform.position, transform.up, transform.forward));

        // 1. 生成所有的神經元節點
        for (int i = 0; i < nodeCount; i++)
        {
            GameObject obj = Instantiate(nodePrefab, transform.position, Quaternion.identity);
            
            // 使用我們新增的 nodeSize 變數來控制球體大小
            obj.transform.localScale = Vector3.one * nodeSize; 
            
            // 將節點從頭部解綁，讓它們能在世界空間中自由漂浮
            obj.transform.parent = null; 

            NeuralNode n = new NeuralNode();
            n.transform = obj.transform;
            n.followDistance = Random.Range(0f, swarmLength); // 隨機分配跟隨距離
            n.noiseOffset = Random.Range(0f, 100f);           // 給予隨機的漂浮節奏
            
            nodes.Add(n);
        }

        // 2. 建立連線的物件池 (預先生成線條備用)
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
            lr.enabled = false; // 一開始先隱藏
            
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

        // 如果頭部移動距離足夠，就記錄一個新的歷史軌跡點
        if (Vector3.Distance(transform.position, pathHistory[0].position) > minDistanceBetweenPoints)
        {
            pathHistory.Insert(0, new PathPoint(transform.position, currentNormal, transform.forward));
        }

        // 刪除過舊的軌跡，避免記憶體爆炸
        int maxHistory = Mathf.CeilToInt(swarmLength / minDistanceBetweenPoints) + 10;
        if (pathHistory.Count > maxHistory)
        {
            pathHistory.RemoveRange(maxHistory, pathHistory.Count - maxHistory);
        }
    }

    // ==========================================
    // 在所有物件移動完後，更新節點位置與連線
    // ==========================================
    void LateUpdate()
    {
        if (pathHistory.Count < 2) return;

        // 1. 計算每個節點應該漂浮到的位置
        foreach (NeuralNode n in nodes)
        {
            PathPoint p = GetPointAtDistance(n.followDistance);

            // 使用 PerlinNoise 產生有機的隨機晃動感
            float noiseX = Mathf.PerlinNoise(Time.time * floatSpeed, n.noiseOffset) - 0.5f;
            float noiseY = Mathf.PerlinNoise(n.noiseOffset, Time.time * floatSpeed) - 0.5f;

            Vector3 rightDir = Vector3.Cross(p.normal, p.forward).normalized;

            // 最終位置 = 軌跡中心 + 離地高度 + 左右漂浮 + 前後漂浮
            Vector3 targetPos = p.position 
                              + (p.normal * groundOffset) 
                              + (rightDir * noiseX * floatRange)
                              + (p.forward * noiseY * floatRange);

            // 平滑地朝目標位置移動
            n.transform.position = Vector3.Lerp(n.transform.position, targetPos, Time.deltaTime * 3f);
        }

        // 2. 處理節點之間的動態連線
        int activeLineIndex = 0; 

        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                float dist = Vector3.Distance(nodes[i].transform.position, nodes[j].transform.position);

                // 如果兩個球夠靠近，就從物件池拿一條線把它們連起來
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

        // 將剩下的、沒用到的線條隱藏起來
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
        
        // 往下打射線尋找地板，探測距離設為 3.5f 確保不會輕易丟失地面
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
        // 簡單的前方防撞：如果前方有牆壁，就強迫轉彎避免撞牆卡死
        if (Physics.Raycast(transform.position + currentNormal * heightFromMesh, transform.forward, 0.5f, groundLayer))
        {
            transform.Rotate(currentNormal, turnSpeed * Time.deltaTime * 2f);
        }
        else
        {
            // 如果前方沒牆壁，就保持微幅的 S 型擺動
            float turnInput = Mathf.Sin(Time.time * 0.5f); 
            transform.rotation = Quaternion.AngleAxis(turnInput * turnSpeed * Time.deltaTime, currentNormal) * transform.rotation;
        }
    }

    private void MoveAlongSurface()
    {
        // 如果懸空了，就輕輕往下掉去尋找地面
        if (!onSurface) 
        {
            transform.position += -currentNormal * 1.5f * Time.deltaTime;
            return;
        }
        
        // 沿著目前的表面往前走
        Vector3 moveDir = Vector3.ProjectOnPlane(transform.forward, currentNormal).normalized;
        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }

    private void SnapToSurface()
    {
        if (!onSurface) return;
        
        // 平滑地將身體貼附到偵測到的牆面高度與角度
        Vector3 targetPos = detectedSurfacePoint + currentNormal * heightFromMesh;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, 0.05f);
        
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, currentNormal) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 7f * Time.deltaTime);
    }
}