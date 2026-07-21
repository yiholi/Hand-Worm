using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// 節肢：緊密軀幹 · 六足輻射 · 三角步態（alternating tripod）。
    /// 節點配置：0 頭 / 1–2 軀幹 / 3–20 六足（髖膝足 ×6）。
    [ExecuteAlways]
    [AddComponentMenu("Chimera/Body Plan - 節肢 Arthropod")]
    public class PlanArthropod : ChimeraBodyPlan
    {
        [Header("節肢")]
        [Tooltip("★ 步頻（speed = 1 時每秒幾個完整步態週期）。\n" +
                 "步幅不用調，也不能調 —— 它由「移動速度 ÷ 步頻」自動算出來，" +
                 "這樣腳掌著地時才不會在地上滑。\n" +
                 "昆蟲是碎步高頻，所以預設比獸高一倍。")]
        [Range(0.5f, 8f)] public float gaitRate = 3.4f;

        [Tooltip("★ 軀幹隨步態的上下起伏。0 = 完全平穩，軀幹只在水平面上滑行，" +
                 "所有動作都發生在腳上。這是節肢跟四足獸最大的體感差別 —— " +
                 "獸靠軀幹起伏傳達重量，蟲的軀幹是一塊不動的殼。")]
        [Range(0f, 0.12f)] public float bodyBob = 0f;

        Vector3 _body;

        // 三角步態：0,3,4 一組，1,2,5 一組。永遠有三隻腳著地，這是昆蟲穩定的來源。
        static readonly float[] Phase = { 0f, 0.5f, 0.5f, 0f, 0f, 0.5f };
        static readonly float[] Angles = { 0.85f, -0.85f, 1.55f, -1.55f, 2.25f, -2.25f };

        // 前進速度（體制單位／秒，speed = 1 時）。步幅由這個值推出來。
        const float FORWARD_SPEED = 1.2f;

        protected override void Layout()
        {
            var roles = new List<ChimeraRole> { ChimeraRole.Head, ChimeraRole.Trunk, ChimeraRole.Trunk };
            var sc = new List<float> { 1.35f, 1.6f, 1.45f };

            for (int l = 0; l < 6; l++)
            {
                roles.Add(ChimeraRole.Limb); roles.Add(ChimeraRole.Limb); roles.Add(ChimeraRole.Limb);
                sc.Add(0.6f); sc.Add(0.48f); sc.Add(0.4f);
            }
            Alloc(roles, sc);
        }

        protected override void ResetState()
        {
            float K = creatureScale;
            _body = new Vector3(boundsCenter.x, floorY + 0.86f * K, boundsCenter.z);
            _heading = 0f;
            if (_pos != null) for (int i = 0; i < _pos.Length; i++) _pos[i] = _body;
        }

        protected override void Solve(float t, float dt)
        {
            float K = creatureScale, sp = speed;

            SteerHeading(_body, 0.5f, 1.1f, dt);
            Vector3 fwd = Fwd, side = Side;

            // 移動速度與步頻是同一組數字的兩面：步幅由這兩個算出來，不另外給滑桿。
            float bodySpeed = FORWARD_SPEED * sp * K;
            float gaitHz = Mathf.Max(0.05f, gaitRate * sp);
            float stride = SlipFreeStride(bodySpeed, gaitHz);

            _body += fwd * (bodySpeed * dt);
            _body = ClampHorizontal(_body);
            // 上下擺動綁在步頻的兩倍上（一個步態週期兩次落地）。bodyBob = 0 時整項消失。
            _body.y = floorY + (0.86f + Mathf.Sin(t * gaitHz * 4f * Mathf.PI) * bodyBob) * K;

            _pos[0] = _body + fwd * (0.55f * K);
            _pos[1] = _body;
            _pos[2] = _body - fwd * (0.62f * K);
            for (int i = 0; i < 3; i++) _dir[i] = fwd;

            float L1 = 0.42f * K, L2 = 0.44f * K;
            // 抬腳高度綁在步幅上：步頻拉高 → 步幅自動變小 → 抬腳也跟著變低，
            // 整組讀成「細碎快速」而不是「小步但每步都高抬腿」。
            float lift = Mathf.Max(0.045f * K, stride * 0.55f);

            for (int l = 0; l < 6; l++)
            {
                float a = Angles[l];
                Vector3 dirOut = fwd * Mathf.Cos(a) + side * Mathf.Sin(a);

                Vector3 hip = _body + dirOut * (0.28f * K) + Vector3.down * (0.05f * K);
                GaitOffset(t * gaitHz + Phase[l], out float gx, out float gy);

                Vector3 foot = hip + dirOut * (0.55f * K)
                    + fwd * (gx * stride)
                    + Vector3.up * (-0.78f * K + gy * lift);
                foot.y = Mathf.Max(floorY + 0.05f * K, foot.y);

                Vector3 knee = Knee(hip, foot, L1, L2, Vector3.up);

                int b = 3 + l * 3;
                _pos[b] = hip; _dir[b] = -(knee - hip).normalized;
                _pos[b + 1] = knee; _dir[b + 1] = (hip - knee).normalized;
                _pos[b + 2] = foot; _dir[b + 2] = (knee - foot).normalized;
            }
        }
    }
}