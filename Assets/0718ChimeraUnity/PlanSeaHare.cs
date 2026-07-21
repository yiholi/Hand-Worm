using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// 海兔：緩慢貼地滑行 · 背側鰓突行波 · 嗅角。
    /// 節點配置：0 頭 / 1–5 外套膜 / 6–21 背側鰓突（8 叢 × 2 節）/ 22–25 嗅角（2 對 × 2 節）。
    [ExecuteAlways]
    [AddComponentMenu("Chimera/Body Plan - 海兔 Sea Hare")]
    public class PlanSeaHare : ChimeraBodyPlan
    {
        [Header("海兔")]
        [Tooltip("背側鰓突叢數。左右交錯排列，相位沿身體往後傳。")]
        [Range(4, 12)] public int cerataClusters = 8;

        const int B = 6;              // 主體節數
        readonly ChimeraPath _path = new ChimeraPath();
        Vector3 _head;
        int _clusters;

        protected override void Layout()
        {
            _clusters = Mathf.Clamp(cerataClusters, 4, 12);

            var roles = new List<ChimeraRole>();
            var sc = new List<float>();

            for (int i = 0; i < B; i++)
            {
                roles.Add(i == 0 ? ChimeraRole.Head : ChimeraRole.Trunk);
                sc.Add(i == 0 ? 1.4f : 1.7f - 0.16f * i);
            }
            for (int c = 0; c < _clusters; c++)
                for (int j = 0; j < 2; j++) { roles.Add(ChimeraRole.Drift); sc.Add(0.55f - j * 0.18f); }
            for (int r = 0; r < 2; r++)
                for (int j = 0; j < 2; j++) { roles.Add(ChimeraRole.Limb); sc.Add(0.42f - j * 0.12f); }

            Alloc(roles, sc);
        }

        protected override void ResetState()
        {
            float K = creatureScale;
            _head = new Vector3(boundsCenter.x, floorY + 0.42f * K, boundsCenter.z);
            _path.Reset(_head);
            _heading = 0f;
            if (_pos != null) for (int i = 0; i < _pos.Length; i++) _pos[i] = _head;
        }

        protected override void Solve(float t, float dt)
        {
            float K = creatureScale, sp = speed;

            SteerHeading(_head, 0.42f, 0.9f, dt);
            Vector3 fwd = Fwd, side = Side;

            _head += fwd * (0.6f * sp * K * dt);      // 慢，是海兔的識別特徵
            _head = ClampHorizontal(_head);
            _head.y = floorY + 0.42f * K;
            _path.Push(_head);

            float SEG = 0.40f * K;
            for (int i = 0; i < B; i++)
            {
                Vector3 p = _path.At(i * SEG);
                p.y = floorY + (0.36f + Mathf.Sin(t * 2.2f * sp - i * 0.6f) * 0.05f) * K;   // 外套膜蠕動
                _pos[i] = p;
            }
            DirsAlongChain(0, B - 1);

            int idx = B;
            for (int c = 0; c < _clusters; c++)
            {
                int seg = Mathf.Min(B - 1, 1 + c * (B - 1) / Mathf.Max(1, _clusters));
                float sgn = (c % 2 == 0) ? 1f : -1f;
                Vector3 anchor = _pos[seg];
                float wave = Mathf.Sin(t * 3f * sp - c * 0.7f);

                for (int j = 0; j < 2; j++)
                {
                    _pos[idx] = anchor
                        + side * (sgn * (0.24f + j * 0.14f + wave * 0.07f) * K)
                        + Vector3.up * ((0.28f + j * 0.30f + wave * 0.06f * (j + 1)) * K);
                    _dir[idx] = (side * (sgn * 0.35f) + Vector3.up).normalized;
                    idx++;
                }
            }

            for (int r = 0; r < 2; r++)
            {
                float sgn = r == 0 ? 1f : -1f;
                for (int j = 0; j < 2; j++)
                {
                    _pos[idx] = _pos[0]
                        + fwd * (0.16f * K)
                        + side * (sgn * 0.16f * K)
                        + Vector3.up * ((0.26f + j * 0.26f + Mathf.Sin(t * 2f * sp + r) * 0.04f) * K);
                    _dir[idx] = (side * (sgn * 0.25f) + Vector3.up + fwd * 0.1f).normalized;
                    idx++;
                }
            }
        }
    }
}
