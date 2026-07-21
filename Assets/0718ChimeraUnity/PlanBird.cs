using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// 鳥：軀幹 + 一到三對翼 · 對與對之間相位延遲 · 轉彎傾側 · 離地飛行。
    /// 節點配置：0 頭 / 1–4 軀幹 / 5 起每對翼各 2 側 × 4 節 / 最後 3 節尾。
    [ExecuteAlways]
    [AddComponentMenu("Chimera/Body Plan - 鳥 Bird")]
    public class PlanBird : ChimeraBodyPlan
    {
        [Header("鳥")]
        [Tooltip("翼對數。改這個會重建整隻。三對翼靠相位延遲讀成一道往後傳的波。")]
        [Range(1, 3)] public int wingPairs = 3;

        [Tooltip("飛行高度相對活動範圍中心的振幅比例。")]
        [Range(0f, 0.5f)] public float bobAmount = 0.28f;

        readonly ChimeraPath _path = new ChimeraPath();
        Vector3 _head;
        int _pairs;
        int _tailIdx;
        float _turn;      // 平滑後的轉向率，用來算傾側
        float _prevHeading;

        protected override void Layout()
        {
            _pairs = Mathf.Clamp(wingPairs, 1, 3);

            var roles = new List<ChimeraRole> { ChimeraRole.Head, ChimeraRole.Trunk,
                                                ChimeraRole.Trunk, ChimeraRole.Trunk, ChimeraRole.Trunk };
            var sc = new List<float> { 1.5f, 1.5f, 1.4f, 1.25f, 1.05f };

            for (int w = 0; w < _pairs * 2; w++)
                for (int j = 0; j < 4; j++) { roles.Add(ChimeraRole.Limb); sc.Add(0.85f - j * 0.14f); }

            _tailIdx = 5 + _pairs * 8;
            roles.Add(ChimeraRole.Tail); roles.Add(ChimeraRole.Tail); roles.Add(ChimeraRole.Tail);
            sc.Add(0.8f); sc.Add(0.62f); sc.Add(0.48f);

            Alloc(roles, sc);
        }

        protected override void ResetState()
        {
            _head = boundsCenter;
            _path.Reset(_head);
            _heading = 0f; _prevHeading = 0f; _turn = 0f;
            if (_pos != null) for (int i = 0; i < _pos.Length; i++) _pos[i] = _head;
        }

        protected override void Solve(float t, float dt)
        {
            float K = creatureScale, sp = speed;

            _prevHeading = _heading;
            SteerHeading(_head, 0.4f, 1.3f, dt);
            // 傾側量取自實際轉向率（含邊界修正），所以撞到牆折返時也會壓機翼
            float rate = Mathf.DeltaAngle(_prevHeading * Mathf.Rad2Deg, _heading * Mathf.Rad2Deg) * Mathf.Deg2Rad / Mathf.Max(1e-4f, dt);
            _turn = Mathf.Lerp(_turn, Mathf.Clamp(rate, -3f, 3f), Mathf.Min(1f, dt * 4f));

            Vector3 fwd = Fwd;
            _head += fwd * (2.6f * sp * K * dt);
            _head.y = boundsCenter.y + Mathf.Sin(t * 1.1f * sp) * boundsSize.y * bobAmount;
            _head = ClampAll(_head);
            _path.Push(_head);

            float SEG = 0.46f * K;
            for (int i = 0; i < 5; i++) _pos[i] = _path.At(i * SEG);
            DirsAlongChain(0, 4);

            // 傾側：轉彎時把 up / side 繞前進軸旋轉
            float bank = -_turn * 0.6f;
            Quaternion roll = Quaternion.AngleAxis(bank * Mathf.Rad2Deg, fwd);
            Vector3 up = roll * Vector3.up;
            Vector3 side = roll * Side;

            for (int p = 0; p < _pairs; p++)
            {
                Vector3 shoulder = _pos[1 + p];
                float pairLag = p * 0.85f;        // ★ 對與對之間的相位延遲：波沿身體往後傳
                float spanMul = 1f - p * 0.13f;

                for (int s = 0; s < 2; s++)
                {
                    float sgn = s == 0 ? 1f : -1f;
                    for (int j = 0; j < 4; j++)
                    {
                        float ph = t * 3.2f * sp - j * 0.55f - pairLag;
                        float lift = Mathf.Sin(ph) * 0.45f * ((j + 1) / 4f) * K;
                        int idx = 5 + (p * 2 + s) * 4 + j;

                        _pos[idx] = shoulder
                            + side * (sgn * (j + 1) * 0.44f * spanMul * K)
                            + up * lift
                            + fwd * (-0.10f * j * K);
                        _dir[idx] = (side * sgn + up * (Mathf.Cos(ph) * 0.55f)).normalized;
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                Vector3 p = _path.At((5f + i * 0.8f) * SEG);
                p += side * (Mathf.Sin(t * 2f * sp - i) * 0.10f * i * K);
                _pos[_tailIdx + i] = p;
                _dir[_tailIdx + i] = _dir[4];
            }
        }
    }
}
