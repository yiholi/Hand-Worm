using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// 章魚：輻射無主軸 · 八腕各自捲曲 · 噴射脈動推進。
    /// 節點配置：0 頭 / 1–2 外套膜 / 3–26 八腕（各 3 節）。
    [ExecuteAlways]
    [AddComponentMenu("Chimera/Body Plan - 章魚 Octopus")]
    public class PlanOctopus : ChimeraBodyPlan
    {
        [Header("章魚")]
        [Tooltip("腕數。八是標準，但這是嵌合體，可以不是八。")]
        [Range(4, 10)] public int arms = 8;

        Vector3 _body;
        Vector3 _vel;
        int _arms;

        protected override void Layout()
        {
            _arms = Mathf.Clamp(arms, 4, 10);

            var roles = new List<ChimeraRole> { ChimeraRole.Head, ChimeraRole.Trunk, ChimeraRole.Trunk };
            var sc = new List<float> { 1.5f, 1.9f, 1.5f };

            for (int a = 0; a < _arms; a++)
                for (int j = 0; j < 3; j++) { roles.Add(ChimeraRole.Drift); sc.Add(0.7f - j * 0.16f); }

            Alloc(roles, sc);
        }

        protected override void ResetState()
        {
            _body = boundsCenter;
            _vel = Vector3.zero;
            _heading = 0f;
            if (_pos != null) for (int i = 0; i < _pos.Length; i++) _pos[i] = _body;
        }

        protected override void Solve(float t, float dt)
        {
            float K = creatureScale, sp = speed;

            SteerHeading(_body, 0.33f, 0.7f, dt);

            // 噴射：不是等速，是「一下一下」。這個節奏本身就是頭足類的識別特徵。
            float jet = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 1.3f * sp)), 4f);

            Vector3 fwd = new Vector3(Mathf.Cos(_heading), 0.18f, Mathf.Sin(_heading)).normalized;
            _vel += fwd * (jet * 4.5f * sp * K * dt);
            _vel *= Mathf.Exp(-2.1f * dt);          // 水阻。用 exp 而不是每幀乘常數，換 fps 手感才一樣
            _body += _vel * dt;

            Vector3 clamped = ClampAll(_body);
            if ((clamped - _body).sqrMagnitude > 1e-8f) _vel *= 0.3f;   // 撞到範圍邊界就洩掉動量
            _body = clamped;

            Vector3 up = Vector3.up;
            Vector3 side = Side;

            // 外套膜：噴射的瞬間收縮
            float squeeze = 1f - jet * 0.22f;
            _dyn[1] = squeeze; _dyn[2] = squeeze;

            _pos[0] = _body + fwd * (0.30f * K);
            _pos[1] = _body - fwd * (0.22f * K);
            _pos[2] = _body - fwd * (0.72f * K);
            for (int i = 0; i < 3; i++) _dir[i] = fwd;

            float L = 0.40f * K;
            Vector3 bi = Vector3.Cross(side, fwd).normalized;

            for (int a = 0; a < _arms; a++)
            {
                float ang = (float)a / _arms * Mathf.PI * 2f;
                Vector3 d = (side * Mathf.Cos(ang) + bi * Mathf.Sin(ang) - fwd * 0.55f).normalized;
                Vector3 axis = Vector3.Cross(d, up).normalized;
                if (axis.sqrMagnitude < 1e-6f) axis = side;

                Vector3 p = _pos[0] + d * (0.28f * K);
                for (int j = 0; j < 3; j++)
                {
                    // 每條腕自己的相位 → 八條腕不同步，這是「沒有中央節奏」的來源
                    float curl = Mathf.Sin(t * 1.7f * sp + a * 0.9f - j * 0.7f) * 0.55f - jet * 0.5f;
                    d = (Quaternion.AngleAxis(curl * 0.5f * Mathf.Rad2Deg, axis) * d).normalized;
                    p += d * L;

                    int idx = 3 + a * 3 + j;
                    _pos[idx] = p;
                    _dir[idx] = d;
                }
            }
        }
    }
}
