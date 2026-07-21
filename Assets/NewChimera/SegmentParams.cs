using UnityEngine;

// 一個決定性的亂數產生器(xorshift)。
// 同一顆 seed 永遠吐出同一串數字 —— 跟 UnityEngine.Random 不同,
// 它不依賴全域狀態,所以跨場次、跨平台都可重現。
// 這是「同一個人 → 同一節」與資料持久化的基礎。
public struct Rng
{
    uint state;

    public Rng(int seed)
    {
        state = (uint)seed;
        if (state == 0) state = 0x9E3779B9u;
        NextUInt(); // warm up
    }

    public uint NextUInt()
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }

    // [0, 1)
    public float NextFloat() => (NextUInt() & 0xFFFFFF) / (float)0x1000000;

    public float Range(float a, float b) => a + (b - a) * NextFloat();

    public int RangeInt(int minInclusive, int maxExclusive)
        => minInclusive + (int)(NextFloat() * (maxExclusive - minInclusive));
}

// 一節的「表現型」。全部由一顆 seed 決定性地取樣而來。
// 幾何軸(length / girth / displace / protrusion)+ 表面軸(hue / iridescence / erosion)
// 一起偏移,才會跨過「這是不同碎片」的感知門檻 —— 單一 hue slider = reskin = 死。
[System.Serializable]
public class SegmentParams
{
    // ---- 幾何軸 ----
    public float segmentLength;   // 這一節沿身體方向的長度
    public float girth;           // 粗細
    public float displaceAmp;     // 表面起伏振幅(這階段先當資料,shader 才會用到)
    public int   protrusionCount; // 突起/纖毛數量(shader 階段用)

    // ---- 表面軸 ----
    public float hueShift;        // 色相 0..1
    public float iridescence;     // 虹彩(shader 階段用)
    public float erosion;         // 衰敗程度 —— 越早路過的人可以侵蝕越深

    public static SegmentParams FromSeed(int seed)
    {
        var rng = new Rng(seed);
        return new SegmentParams
        {
            segmentLength   = rng.Range(0.6f, 1.3f),
            girth           = rng.Range(0.7f, 1.4f),
            displaceAmp     = rng.Range(0f,   0.5f),
            protrusionCount = rng.RangeInt(2, 9),
            hueShift        = rng.NextFloat(),
            iridescence     = rng.Range(0f, 1f),
            erosion         = rng.Range(0f, 0.4f),
        };
    }
}

// ---- JSON 結構 ----
// 注意:JSON 只存 seed,不存上面那一堆參數。
// 參數在 runtime 由 seed 重算 —— 資料庫超輕,而且「一人 = 一 seed」的對應很乾淨。
[System.Serializable]
public class SegmentData
{
    public string sessionId;
    public int    seed;
    public int    arrivalIndex;
}

[System.Serializable]
public class WormData
{
    public SegmentData[] segments;
}
