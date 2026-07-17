using System.Collections.Generic;
using UnityEngine;

public class CaterpillarController : MonoBehaviour
{
    // 用來記錄頭部走過的每個點（位置 + 當時的法線），讓身體可以沿著軌跡走
    private struct PathPoint
    {
        public Vector3 position;
        public Vector3 normal;
        public PathPoint(Vector3 p, Vector3 n) { position = p; normal = n; }
    }

    // 新增一個資料結構來記錄每一節身體
    // 因為現在每節的長度都不一樣，我們必須記錄這節專屬的「跟隨距離」
    private class SegmentNode
    {
        public Transform transform;
        public float followDistance; // 這節身體應該距離頭部多遠
    }

    [Header("【身體結構】")]
    public Segment segmentPrefab;       
    public int startSegments = 6;       
    public float segmentSpacing = 0.5f; // 基礎間距
    public float bodyScale = 1f;        // 整體縮放
    
    // 【新增】控制身體轉向的敏感度，數值越低轉得越慢、越不容易亂轉
    public float rotationSpeed = 8f;    

    [Header("【Sine Wave 擺動】")]
    public float waveAmplitude = 0.08f; // 左右擺動幅度
    public float waveFrequency = 3f;    // 擺動頻率（越大越快）
    public float phaseOffset = 0.8f;    // 相位偏移（讓每節錯開，形成波浪感）

    [Header("【貼地】")]
    public LayerMask groundLayer;
    public float groundOffset = 0.5f;   // 身體節距離表面的高度

    private List<SegmentNode> segments = new List<SegmentNode>();
    private List<PathPoint> pathHistory = new List<PathPoint>();

    // 相鄰兩個記錄點之間的最小距離，太小會讓 list 爆炸，太大路徑不夠精細
    private float minDistanceBetweenPoints = 0.02f;
    private int totalSpawnedCount = 0;  // 記錄總共生成了幾節，用來當作 arrivalIndex

    void Start()
    {
        // 把設定值乘上縮放，之後所有計算都用縮放後的值
        segmentSpacing *= bodyScale;
        groundOffset   *= bodyScale;
        waveAmplitude  *= bodyScale;

        // 記錄初始頭部點
        pathHistory.Add(new PathPoint(transform.position, transform.up));

        // 根據 startSegments 的數量，依序生成初始的身體節
        float currentDistance = 0f;
        for (int i = 0; i < startSegments; i++)
        {
            CreateNewSegment(ref currentDistance);
        }
    }

    void Update()
    {
        // 只要頭部移動超過最小距離，就把當前位置和法線記進歷史
        if (Vector3.Distance(transform.position, pathHistory[0].position) > minDistanceBetweenPoints)
            pathHistory.Insert(0, new PathPoint(transform.position, transform.up));

        // 限制歷史長度：取得最後一節所需的跟隨距離來計算最大歷史長度
        float maxFollowDist = segments.Count > 0 ? segments[segments.Count - 1].followDistance : 0f;
        int maxHistory = Mathf.CeilToInt(maxFollowDist / minDistanceBetweenPoints) * 5 + 50;
        if (pathHistory.Count > maxHistory)
            pathHistory.RemoveRange(maxHistory, pathHistory.Count - maxHistory);

        // 空白鍵：即時新增一節身體（用於 debug 或互動）
        if (Input.GetKeyDown(KeyCode.Space))
        {
            AddNewSegment();
        }
    }

    void LateUpdate()
    {
        // 在 Update 之後才更新身體，確保頭部位置已經是本幀最終值
        if (segments.Count == 0 || pathHistory.Count == 0) return;

        for (int i = 0; i < segments.Count; i++)
        {
            // 讀取這節專屬的 followDistance，在軌跡中找出對應的點
            PathPoint sample = GetPointAtDistance(segments[i].followDistance);

            // 基準位置：表面點 + 法線方向抬高 groundOffset
            Vector3 basePos = sample.position + sample.normal * groundOffset;

            // 讓這一節朝向前一節（或頭部），形成跟隨感
            Vector3 lookTarget = (i == 0) ? transform.position : segments[i - 1].transform.position;
            Vector3 dir = lookTarget - segments[i].transform.position;

            // 【修改點】拉高判斷閾值，並且加入防萬向鎖的保護機制
            if (dir.sqrMagnitude > 0.001f)
            {
                // 用內積 (Dot) 檢查前進方向(dir)和地面法線(normal)是不是快要平行了
                // 如果兩者太過平行，LookRotation 會崩潰導致亂轉，所以要避開這個情況
                if (Mathf.Abs(Vector3.Dot(dir.normalized, sample.normal)) < 0.99f)
                {
                    segments[i].transform.rotation = Quaternion.Slerp(
                        segments[i].transform.rotation,
                        Quaternion.LookRotation(dir.normalized, sample.normal),
                        Time.deltaTime * rotationSpeed); // 使用新的變數來控制平滑度
                }
            }

            // Sine wave 橫向擺動，i * phaseOffset 讓每節錯開相位
            float wave = Mathf.Sin(Time.time * waveFrequency + i * phaseOffset);
            segments[i].transform.position = basePos + segments[i].transform.right * wave * waveAmplitude;
        }
    }

    // 在 pathHistory 中找到「距離起點 targetDist」的插值點
    private PathPoint GetPointAtDistance(float targetDist)
    {
        if (pathHistory.Count < 2) return pathHistory[0];

        float accumulatedDist = 0f;
        for (int i = 0; i < pathHistory.Count - 1; i++)
        {
            float segDist = Vector3.Distance(pathHistory[i].position, pathHistory[i + 1].position);
            if (accumulatedDist + segDist >= targetDist)
            {
                // 在兩點之間線性插值
                float t = (targetDist - accumulatedDist) / segDist;
                return new PathPoint(
                    Vector3.Lerp(pathHistory[i].position, pathHistory[i + 1].position, t),
                    Vector3.Lerp(pathHistory[i].normal,   pathHistory[i + 1].normal,   t).normalized
                );
            }
            accumulatedDist += segDist;
        }

        // 歷史不夠長時，回傳最後一個記錄點
        return pathHistory[pathHistory.Count - 1];
    }

    // 把生成單一身體節的邏輯獨立出來，套用動態資料
    private void CreateNewSegment(ref float distanceCursor)
    {
        // 1. 建立這節身體的專屬亂數資料與 ID
        SegmentData data = new SegmentData
        {
            sessionId = System.Guid.NewGuid().ToString("N").Substring(0, 6),
            seed = Random.Range(int.MinValue, int.MaxValue), // 隨機抽一個種子
            arrivalIndex = totalSpawnedCount
        };

        // 2. 根據種子計算出這節身體的長度、粗細、顏色等參數
        SegmentParams p = SegmentParams.FromSeed(data.seed);

        // 3. 計算這節身體應該排在距離頭部多遠的地方 (加上它本身的長度比例)
        distanceCursor += segmentSpacing * p.segmentLength;

        // 4. 生成物件
        Vector3 pos = transform.position + Vector3.back * distanceCursor;
        Segment seg = Instantiate(segmentPrefab, pos, Quaternion.identity);
        
        // 5. 將生成的資料套用到物件上 (這會改變材質顏色與模型比例)
        seg.Apply(p, data);

        // 6. 疊加我們整體的 bodyScale 縮放，並解除父子關係獨立移動
        seg.transform.localScale *= bodyScale;
        seg.transform.parent = null;

        // 7. 將設定好的節點與專屬跟隨距離記錄起來
        segments.Add(new SegmentNode 
        { 
            transform = seg.transform, 
            followDistance = distanceCursor 
        });

        totalSpawnedCount++;
    }

    // 在尾部新增一節身體（相容原本的空白鍵或外部呼叫）
    public void AddNewSegment()
    {
        // 接在最後一節的距離後面繼續往後排
        float dist = segments.Count > 0 ? segments[segments.Count - 1].followDistance : 0f;
        CreateNewSegment(ref dist);
    }
}