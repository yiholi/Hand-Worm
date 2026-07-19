using UnityEngine;

namespace Chimera
{
    /// 移動邏輯佔位：只做一件事 —— 移動自己的 Transform。
    /// 頭端保證待在 boundsCenter / boundsSize 定義的盒子裡，Scene 視窗會把盒子畫出來。
    [ExecuteAlways]
    public class SampleWanderDriver : MonoBehaviour
    {
        [Header("活動範圍")]
        [Tooltip("範圍中心（世界座標）。")]
        public Vector3 boundsCenter = new Vector3(0f, 1.6f, 0f);

        [Tooltip("★ 完整邊長，不是半徑。3×3 的活動範圍就填 (3, 2, 3)。" +
                 "這就是頭端真正會走到的邊界，沒有內縮。")]
        public Vector3 boundsSize = new Vector3(3f, 2f, 3f);

        [Tooltip("在 Scene 視窗畫出範圍。")]
        public bool drawBounds = true;

        [Header("運動")]
        [Range(0f, 2f)] public float speed = 0.4f;

        [Header("診斷")]
        [Tooltip("用 unscaledDeltaTime 累積時間。timeScale 被設成 0 時仍然會動。")]
        public bool useUnscaledTime = true;

        [Tooltip("每秒往 Console 印一次 time / timeScale / deltaTime。確認完關掉。")]
        public bool logDiagnostics = false;

        // 自己累積時間，不直接讀 Time.time。
        // Time.time 會被 timeScale 凍住；而且原本寫成 Time.time * speed，
        // 拖 speed 滑桿時整個相位會瞬間跳掉，看起來像瞬移。
        float _t;
        float _logTimer;

        void Update()
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (!Application.isPlaying) delta = 1f / 60f;

            if (logDiagnostics && Application.isPlaying)
            {
                _logTimer += Time.unscaledDeltaTime;
                if (_logTimer >= 1f)
                {
                    _logTimer = 0f;
                    Debug.Log($"[Wander] time={Time.time:F2} unscaled={Time.unscaledTime:F2} " +
                              $"timeScale={Time.timeScale} dt={Time.deltaTime:F4} pos={transform.position}");
                }
            }

            _t += delta * speed;

            // 先算 -1..1 的正規化位置，最後才乘上半徑。
            // 限制發生在「變成公尺」之前，所以不管參數怎麼調都不可能跑出範圍，
            // 也不需要事後 clamp（clamp 會在邊界產生黏牆的停頓感）。
            // 兩項振幅是 0.7 + 0.3，和恰好等於 1，所以極值剛好貼到邊界。
            Vector3 n = new Vector3(
                Mathf.Sin(_t * 0.21f) * 0.7f + Mathf.Sin(_t * 0.37f) * 0.3f,
                Mathf.Sin(_t * 0.17f),
                Mathf.Cos(_t * 0.26f) * 0.7f + Mathf.Cos(_t * 0.43f) * 0.3f);

            transform.position = boundsCenter + Vector3.Scale(n, boundsSize * 0.5f);
        }

        void OnDrawGizmos()
        {
            if (!drawBounds) return;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireCube(boundsCenter, boundsSize);
        }
    }
}
