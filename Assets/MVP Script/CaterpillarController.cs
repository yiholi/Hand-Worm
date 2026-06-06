using System.Collections.Generic;
using UnityEngine;

public class CaterpillarController : MonoBehaviour
{
    // 用來記錄頭部走過的每個點（位置 + 當時的法線）
    private struct PathPoint
    {
        public Vector3 position;
        public Vector3 normal;
        public PathPoint(Vector3 p, Vector3 n) { position = p; normal = n; }
    }

    [Header("【身體結構】")]
    public GameObject segmentPrefab;    // 身體每節的預製體
    public int startSegments = 6;       // 初始身體節數
    public float segmentSpacing = 0.5f; // 每節之間的距離
    public float bodyScale = 1f;        // 整體縮放

    [Header("【Sine Wave 擺動】")]
    public float waveAmplitude = 0.08f; // 左右擺動幅度
    public float waveFrequency = 3f;    // 擺動頻率（越大越快）
    public float phaseOffset = 0.8f;    // 相位偏移（讓每節錯開，形成波浪感）

    [Header("【貼地】")]
    public LayerMask groundLayer;
    public float groundOffset = 0.5f;   // 身體節距離表面的高度

    private List<Transform> segments = new List<Transform>();
    private List<PathPoint> pathHistory = new List<PathPoint>();

    // 相鄰兩個記錄點之間的最小距離，太小會讓 list 爆炸，太大路徑不夠精細
    private float minDistanceBetweenPoints = 0.02f;

    void Start()
    {
        // 把設定值乘上縮放，之後所有計算都用縮放後的值
        segmentSpacing *= bodyScale;
        groundOffset   *= bodyScale;
        waveAmplitude  *= bodyScale;

        // 記錄初始點
        pathHistory.Add(new PathPoint(transform.position, transform.up));

        // 生成初始身體節，從頭部後面依序排列
        for (int i = 0; i < startSegments; i++)
        {
            Vector3 pos = transform.position + Vector3.back * (i + 1) * segmentSpacing;
            GameObject seg = Instantiate(segmentPrefab, pos, Quaternion.identity);
            seg.transform.localScale = Vector3.one * bodyScale;
            seg.transform.parent = null; // 不掛在頭部底下，各自獨立跟隨 path
            segments.Add(seg.transform);
        }
    }

    void Update()
    {
        // 只要頭部移動超過最小距離，就把當前位置和法線記進歷史
        if (Vector3.Distance(transform.position, pathHistory[0].position) > minDistanceBetweenPoints)
            pathHistory.Insert(0, new PathPoint(transform.position, transform.up));

        // 限制歷史長度：最後一節需要的最大距離 / 最小間隔 * 5（緩衝）
        // * 5 是為了讓尾部有足夠的歷史可以查詢，避免身體節抖動
        int maxHistory = Mathf.CeilToInt((segments.Count * segmentSpacing) / minDistanceBetweenPoints) * 5;
        if (pathHistory.Count > maxHistory)
            pathHistory.RemoveRange(maxHistory, pathHistory.Count - maxHistory);

        // 空白鍵：即時新增一節身體（用於 debug 或互動）
        if (Input.GetKeyDown(KeyCode.Space))
            AddNewSegment();
    }

    void LateUpdate()
    {
        // 在 Update 之後才更新身體，確保頭部位置已經是本幀最終值
        if (segments.Count == 0 || pathHistory.Count == 0) return;

        for (int i = 0; i < segments.Count; i++)
        {
            // 在 pathHistory 中找出「距頭部 N * spacing」的那個點
            PathPoint sample = GetPointAtDistance((i + 1) * segmentSpacing);

            // 基準位置：表面點 + 法線方向抬高 groundOffset
            Vector3 basePos = sample.position + sample.normal * groundOffset;

            // 讓這一節朝向前一節（或頭部），形成跟隨感
            Vector3 lookTarget = (i == 0) ? transform.position : segments[i - 1].position;
            Vector3 dir = lookTarget - segments[i].position;

            if (dir.sqrMagnitude > 0.0001f)
            {
                segments[i].rotation = Quaternion.Slerp(
                    segments[i].rotation,
                    Quaternion.LookRotation(dir.normalized, sample.normal),
                    Time.deltaTime * 20f);
            }

            // Sine wave 橫向擺動，i * phaseOffset 讓每節錯開相位
            float wave = Mathf.Sin(Time.time * waveFrequency + i * phaseOffset);
            segments[i].position = basePos + segments[i].right * wave * waveAmplitude;
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

    // 在尾部新增一節身體
    public void AddNewSegment()
    {
        Vector3 pos = segments.Count > 0 ? segments[^1].position : transform.position;
        GameObject seg = Instantiate(segmentPrefab, pos, Quaternion.identity);
        seg.transform.localScale = Vector3.one * bodyScale;
        seg.transform.parent = null;
        segments.Add(seg.transform);
    }
}