using UnityEngine;

namespace Chimera
{
    /// 預設脊索：verlet 質點鏈 + 距離約束。
    /// 你只需要移動 headTarget 這個 Transform，整條群體會自己跟上並拖出曲線。
    /// 想完全自己控制的話，寫一個自己的 ISpineProvider，把這支刪掉即可。
    [ExecuteAlways]
    public class VerletSpine : MonoBehaviour, ISpineProvider
    {
        [Header("驅動")]
        [Tooltip("你的移動腳本只要移動這個 Transform 就好")]
        public Transform headTarget;

        [Header("鏈")]
        [Tooltip("幾個 zooid。改這個會重建整個群體。")]
        [Range(3, 40)] public int nodeCount = 18;

        [Tooltip("相鄰兩節的目標距離（公尺）。決定整條有多長。0.5 = 18 節約 9 公尺。")]
        [Range(0.05f, 2f)] public float restLength = 0.5f;

        // ── 手感常數 ────────────────────────────────────────────────
        // 原本是 slack / followSpeed / constraintIterations 三個 Inspector 滑桿。
        // 拿掉了，改成寫死在這裡。數值是拿掉當下 Inspector 上的值（slack 0.69、
        // followSpeed 0.59），所以外觀跟拿掉前一致。要改手感就改這三個數字。
        //
        // DAMP  速度保留率。越接近 1 越軟、越會甩尾。
        // DROOP 每秒往下沉多少（公尺／秒）。
        // FOLLOW 頭端追上 headTarget 的速率。越大頭越黏在目標上。
        const float DAMP   = 0.959f;
        const float DROOP  = 0.348f;
        const float FOLLOW = 6.72f;
        // ────────────────────────────────────────────────────────────

        Vector3[] _pts, _prev;

        public int Count => nodeCount;

        void OnEnable() => Rebuild();

        void OnValidate()
        {
            // ★ 之前漏掉這段：改節數必須通知群體重建，否則滑桿看起來沒作用
            Rebuild();
            var colony = GetComponent<ChimeraColony>();
            if (colony != null) colony.rebuildNow = true;
        }

        public void Rebuild()
        {
            _pts = new Vector3[nodeCount];
            _prev = new Vector3[nodeCount];
            Vector3 origin = headTarget ? headTarget.position : transform.position;
            for (int i = 0; i < nodeCount; i++)
            {
                _pts[i] = origin + Vector3.down * (i * restLength);
                _prev[i] = _pts[i];
            }
        }

        public void Tick(float dt)
        {
            if (_pts == null || _pts.Length != nodeCount) Rebuild();
            if (dt <= 0f) return;

            Vector3 target = headTarget ? headTarget.position : transform.position;
            _pts[0] = Vector3.Lerp(_pts[0], target, 1f - Mathf.Exp(-FOLLOW * dt));

            float droop = DROOP * dt;
            for (int i = 1; i < nodeCount; i++)
            {
                Vector3 v = (_pts[i] - _prev[i]) * DAMP;
                _prev[i] = _pts[i];
                _pts[i] += v;
                _pts[i] += Vector3.down * droop;
            }

            // 距離約束：單趟由頭往尾傳遞就收斂，不需要迭代。
            // 只移動下游那一節——之前寫成兩端各修一半，但頭端每幀被 lerp 強制拉向
            // HeadTarget，修正量會沿著鏈往回傳遞，跑幾幀就整條縮進頭裡面。
            for (int i = 1; i < nodeCount; i++)
            {
                Vector3 d = _pts[i] - _pts[i - 1];
                float len = d.magnitude;

                // 退化保護：兩點完全重合時 d 是零向量，沒有方向可以推開，
                // 沒有這段的話整條一旦擠在一起就永遠解不開。
                if (len < 1e-5f)
                {
                    d = new Vector3(0.001f, -1f, 0.001f);
                    len = d.magnitude;
                }

                _pts[i] = _pts[i - 1] + d * (restLength / len);
            }
        }

        public Vector3 GetPoint(int i) => _pts[Mathf.Clamp(i, 0, nodeCount - 1)];

        public Vector3 GetForward(int i)
        {
            i = Mathf.Clamp(i, 0, nodeCount - 1);
            Vector3 d = (i < nodeCount - 1) ? _pts[i] - _pts[i + 1] : _pts[i - 1] - _pts[i];
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.up;
        }

        void OnDrawGizmosSelected()
        {
            if (_pts == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 1; i < _pts.Length; i++) Gizmos.DrawLine(_pts[i - 1], _pts[i]);
        }
    }
}