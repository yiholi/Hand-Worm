using UnityEngine;

namespace Chimera
{
    /// ★ 這支是「暫時的」佔位，示範移動邏輯該長什麼樣：
    ///   只做一件事 —— 移動自己的 Transform。
    /// 你之後寫的移動（MRUK 空間漫遊、繞著觀眾、追手部…）就寫在這個位置，
    /// 完全不需要知道 zooid、器官、shader 的存在。用完把這支刪掉即可。
    public class SampleWanderDriver : MonoBehaviour
    {
        [Range(0f, 2f)] public float speed = 0.4f;
        public Vector3 extents = new Vector3(2.4f, 1.1f, 2.0f);
        public Vector3 center = new Vector3(0f, 1.6f, 0f);

        [Header("診斷")]
        [Tooltip("每秒往 Console 印一次 time / timeScale / deltaTime。確認完關掉。")]
        public bool logDiagnostics = true;

        [Tooltip("用 unscaledDeltaTime 累積時間。timeScale 被設成 0 時仍然會動——" +
                 "這是調參用的繞道，不是修好根本原因。")]
        public bool useUnscaledTime = true;

        // 自己累積時間，不直接讀 Time.time。
        // Time.time 會被 timeScale 凍住；而且原本寫成 Time.time * speed，
        // 拖 speed 滑桿時整個相位會瞬間跳掉，看起來像瞬移。
        float _t;
        float _logTimer;

        void Update()
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (logDiagnostics)
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

            transform.position = center + new Vector3(
                Mathf.Sin(_t * 0.21f) * extents.x + Mathf.Sin(_t * 0.37f) * extents.x * 0.4f,
                Mathf.Sin(_t * 0.17f) * extents.y,
                Mathf.Cos(_t * 0.26f) * extents.z);
        }
    }
}
