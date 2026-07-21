using System.Collections.Generic;
using UnityEngine;

// 掛在一個空的 GameObject(叫 Worm)上。
// 讀 JSON(或沒 JSON 時用隨機 seed),沿 Z 軸把節段串成一條。
public class WormBuilder : MonoBehaviour
{
    [Header("必要:把你的 Icosphere prefab 拖到這裡")]
    public Segment segmentPrefab;

    [Header("資料來源(可留空)")]
    public TextAsset jsonFile;     // 留空 → 用隨機 seed 生成
    public int randomCount = 20;   // 沒 JSON 時生幾節

    [Header("排列")]
    public float unitLength = 1.0f; // segmentLength = 1 時,一節的長度
    public float gap = 0.05f;       // 節與節之間的縫隙

    readonly List<Segment> spawned = new List<Segment>();

    void Start() => Build();

    [ContextMenu("Rebuild")]
    public void Build()
    {
        Clear();
        SegmentData[] data = LoadData();

        float cursor = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            SegmentData d = data[i];
            SegmentParams p = SegmentParams.FromSeed(d.seed);

            Segment seg = Instantiate(segmentPrefab, transform);
            seg.name = $"Segment_{i}_{d.sessionId}";

            float len = unitLength * p.segmentLength;
            float center = cursor + len * 0.5f;
            seg.transform.localPosition = new Vector3(0f, 0f, center);
            cursor += len + gap;

            seg.Apply(p, d);   // ← 這次已經改好,吃 SegmentData
            spawned.Add(seg);
        }
    }

    SegmentData[] LoadData()
    {
        if (jsonFile != null && !string.IsNullOrEmpty(jsonFile.text))
        {
            WormData worm = JsonUtility.FromJson<WormData>(jsonFile.text);
            if (worm != null && worm.segments != null && worm.segments.Length > 0)
                return worm.segments;
        }

        var arr = new SegmentData[randomCount];
        for (int i = 0; i < randomCount; i++)
        {
            arr[i] = new SegmentData
            {
                sessionId    = System.Guid.NewGuid().ToString("N").Substring(0, 6),
                seed         = Random.Range(int.MinValue, int.MaxValue),
                arrivalIndex = i,
            };
        }
        return arr;
    }

    void Clear()
    {
        foreach (Segment s in spawned)
            if (s != null) DestroyImmediate(s.gameObject);
        spawned.Clear();
    }
}
