using UnityEngine;

namespace Chimera
{
    /// 預設脊索：verlet 質點鏈 + 距離約束。
    /// 你只需要移動 headTarget 這個 Transform，整條群體會自己跟上並拖出曲線。
    /// 想完全自己控制的話，寫一個自己的 ISpineProvider，把這支刪掉即可。
    public class VerletSpine : MonoBehaviour, ISpineProvider
    {
        [Header("驅動")]
        [Tooltip("你的移動腳本只要移動這個 Transform 就好")]
        public Transform headTarget;

        [Header("鏈")]
        [Range(3, 40)] public int nodeCount = 18;
        [Range(0.05f, 2f)] public float restLength = 0.5f;
        [Range(0f, 1f)] public float slack = 0.55f;      // 柔軟度／下垂
        [Range(0f, 1f)] public float followSpeed = 0.5f; // 頭端追上目標的速度
        [Range(1, 8)] public int constraintIterations = 4;

        Vector3[] _pts, _prev;

        public int Count => nodeCount;

        void OnEnable() => Rebuild();

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

            // 頭端：追向目標
            Vector3 target = headTarget ? headTarget.position : transform.position;
            _pts[0] = Vector3.Lerp(_pts[0], target, 1f - Mathf.Exp(-(2f + 8f * followSpeed) * dt));

            // verlet 積分
            float damp = 0.90f + 0.085f * slack;
            float droop = (0.10f + 0.36f * slack) * dt;
            for (int i = 1; i < nodeCount; i++)
            {
                Vector3 v = (_pts[i] - _prev[i]) * damp;
                _prev[i] = _pts[i];
                _pts[i] += v;
                _pts[i] += Vector3.down * droop;
            }

            // 距離約束
            float rest = restLength * (0.75f + 0.5f * (1f - slack));
            for (int it = 0; it < constraintIterations; it++)
            {
                for (int i = 1; i < nodeCount; i++)
                {
                    Vector3 d = _pts[i] - _pts[i - 1];
                    float len = d.magnitude;
                    if (len < 1e-5f) continue;
                    Vector3 corr = d * ((len - rest) / len);
                    if (i == 1) _pts[i] -= corr;                    // 頭端固定，只動後面
                    else { _pts[i - 1] += corr * 0.5f; _pts[i] -= corr * 0.5f; }
                }
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
