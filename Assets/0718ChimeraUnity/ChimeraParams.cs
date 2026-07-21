using System.Text;
using UnityEngine;

namespace Chimera
{
    /// 一個 zooid 的全部形態參數。由 (標籤字串 + 序號) 的雜湊決定，完全 deterministic。
    public struct ZooidParams
    {
        public float seg;     // 分節
        public float radial;  // 輻射葉瓣強度
        public float warp;    // 噪聲擾動
        public float taper;   // 漸變
        public float lobes;   // 葉瓣數
        public float squash;  // 沿軸壓扁
        public float seed;    // 噪聲種子 / 相位
        public float hue;     // 虹光相位
    }

    /// 分區：決定長哪一類附肢。zone < 0 代表頭端泳鐘。
    public enum Zone { Head, Nectosome, Siphosome, Gonosome }

    public static class ChimeraHash
    {
        public static uint Fnv1a(string s)
        {
            uint h = 0x811c9dc5u;
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            for (int i = 0; i < bytes.Length; i++)
            {
                h ^= bytes[i];
                h *= 0x01000193u;
            }
            return h;
        }

        /// 從 uint 取第 n 個 5-bit 切片，正規化到 0..1
        static float Slice(uint h, int n) => ((h >> (n * 5)) & 0x1Fu) / 31f;

        public static ZooidParams Make(string label, int index)
        {
            uint h = Fnv1a(label + "#" + index);
            return new ZooidParams
            {
                seg    = Slice(h, 0),
                radial = Slice(h, 1),
                warp   = Slice(h, 2) * 0.55f,
                taper  = Slice(h, 3),
                lobes  = 2f + Mathf.Floor(Slice(h, 4) * 5f),
                squash = 0.65f + Slice(h, 5) * 0.85f,
                seed   = (h % 9000u) / 83f,
                hue    = (h % 1000u) / 1000f * 6.2831853f
            };
        }

        /// 位置 → 分區。這是「差異化來自位置而非隨機」的規則所在。
        public static Zone ZoneOf(int index, int count)
        {
            if (index == 0) return Zone.Head;
            float t = count > 1 ? (float)index / (count - 1) : 0f;
            if (t < 0.35f) return Zone.Nectosome;
            if (t < 0.70f) return Zone.Siphosome;
            return Zone.Gonosome;
        }

        /// 給幾何生成用的 deterministic 亂數（不吃 UnityEngine.Random，避免污染全域狀態）
        public static float Rnd(float seed, int n)
        {
            float x = Mathf.Sin(seed * 12.9898f + n * 78.233f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }
    }
}
