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
        // --------------------------------------------------------
        // 1. 基本設定區塊
        // --------------------------------------------------------
        [Header("章魚")]
        [Tooltip("腕數。八是標準，但這是嵌合體，可以不是八。")]
        [Range(4, 10)] public int arms = 8;

        // 內部狀態變數，用來記錄章魚的身體位置、速度與腕數
        Vector3 _body;
        Vector3 _vel;
        int _arms;

        // --------------------------------------------------------
        // 2. 骨架節點配置
        // 決定這隻生物有幾個頭、幾個身體、幾條觸手
        // --------------------------------------------------------
        protected override void Layout()
        {
            _arms = Mathf.Clamp(arms, 4, 10);

            // 建立一個清單，依序放入頭部與兩個軀幹
            var roles = new List<ChimeraRole> { ChimeraRole.Head, ChimeraRole.Trunk, ChimeraRole.Trunk };
            var sc = new List<float> { 1.5f, 1.9f, 1.5f };

            // 幫每一條腕分配 3 個關節節點，並讓尾端越來越細
            for (int a = 0; a < _arms; a++)
                for (int j = 0; j < 3; j++) { roles.Add(ChimeraRole.Drift); sc.Add(0.7f - j * 0.16f); }

            Alloc(roles, sc);
        }

        // --------------------------------------------------------
        // 3. 狀態重置
        // 當程式重新整理時，把章魚放回魚缸正中央，並將速度歸零
        // --------------------------------------------------------
        protected override void ResetState()
        {
            _body = boundsCenter;
            _vel = Vector3.zero;
            _heading = 0f;
            if (_pos != null) for (int i = 0; i < _pos.Length; i++) _pos[i] = _body;
        }

        // --------------------------------------------------------
        // 4. 核心物理運算 (每一幀都會執行)
        // 處理游動、碰撞、觸手擺動
        // --------------------------------------------------------
        protected override void Solve(float t, float dt)
        {
            // 讀取面板上的體型與速度設定
            float K = creatureScale, sp = speed;

            // 處理水平方向的平滑轉向
            SteerHeading(_body, 0.33f, 0.7f, dt);

            // --- 噴射推進與滑行邏輯 ---
            // 模擬章魚「一下一下」的噴射脈動節奏 (數值會在 0 到 1 之間起伏)
            float pulse = Mathf.Sin(t * 1.3f * sp);
            float jet = Mathf.Pow(Mathf.Max(0f, pulse), 4f);
            
            // ★ 新增防卡死機制：給予微弱的基礎滑行推力
            // 讓章魚即使在 pulse 小於 0 的休息期間，也不會完全煞車定格
            float idleDrift = 0.08f; 

            // 取得父類別計算好的魚缸旋轉角度
            Quaternion tankRot = BoundsRot;

            // 將章魚目前的世界座標，轉換為「旋轉魚缸」的內部相對座標
            Vector3 localPos = Quaternion.Inverse(tankRot) * (_body - boundsCenter);
            Vector3 extents = boundsSize * 0.5f;

            // 動態計算垂直俯仰角 (Pitch)，判斷章魚現在離天花板還是地板比較近
            float relativeY = localPos.y / (extents.y + 0.001f);
            float pitch = -relativeY * 0.6f + Mathf.Sin(t * 0.8f) * 0.15f;

            // 決定下一次噴射的前進方向 (結合了水平朝向與垂直俯仰角)
            Vector3 fwd = new Vector3(Mathf.Cos(_heading), pitch, Mathf.Sin(_heading)).normalized;

            // 推進計算：噴射力道 + 基礎滑行力道，再減去水阻的衰減
            _vel += fwd * ((jet * 4.5f + idleDrift) * sp * K * dt);
            _vel *= Mathf.Exp(-2.1f * dt);          
            _body += _vel * dt;

            // --- 邊界碰撞防護 ---
            // 呼叫父類別的 ClampAll 函式，確保位置絕對不會超出旋轉魚缸
            Vector3 clampedWorld = ClampAll(_body);

            // 如果發現章魚撞到牆壁 (位置被強制修正了)
            if ((clampedWorld - _body).sqrMagnitude > 1e-8f) 
            {
                // ★ 終極防卡死機制：強制回歸中心
                // 只要碰到牆壁，就立刻算出魚缸正中央在哪裡
                Vector3 toCenter = (boundsCenter - clampedWorld);
                toCenter.y = 0f; // 忽略高度，只轉動水平頭部方向
                
                if(toCenter.sqrMagnitude > 0.01f)
                {
                    toCenter.Normalize();
                    // 強制把章魚的頭轉向正中央
                    _heading = Mathf.Atan2(toCenter.z, toCenter.x);
                }
                
                // 撞牆後大幅減速，避免高速來回彈跳
                _vel *= 0.3f; 
            }
            // 更新為安全的最終位置
            _body = clampedWorld;

            Vector3 up = Vector3.up;
            Vector3 side = Side;

            // --- 身體與觸手動畫 ---
            // 配合噴射節奏，讓外套膜產生收縮的視覺效果
            float squeeze = 1f - jet * 0.22f;
            _dyn[1] = squeeze; _dyn[2] = squeeze;

            // 更新頭部與身體的三個主要節點位置
            _pos[0] = _body + fwd * (0.30f * K);
            _pos[1] = _body - fwd * (0.22f * K);
            _pos[2] = _body - fwd * (0.72f * K);
            for (int i = 0; i < 3; i++) _dir[i] = fwd;

            float L = 0.40f * K;
            Vector3 bi = Vector3.Cross(side, fwd).normalized;

            // 計算八條觸手的獨立扭動動畫
            for (int a = 0; a < _arms; a++)
            {
                // 計算這條觸手的生長角度
                float ang = (float)a / _arms * Mathf.PI * 2f;
                Vector3 d = (side * Mathf.Cos(ang) + bi * Mathf.Sin(ang) - fwd * 0.55f).normalized;
                Vector3 axis = Vector3.Cross(d, up).normalized;
                if (axis.sqrMagnitude < 1e-6f) axis = side;

                Vector3 p = _pos[0] + d * (0.28f * K);
                for (int j = 0; j < 3; j++)
                {
                    // 讓每條觸手有自己的時間差與捲曲度 (Curl)
                    float curl = Mathf.Sin(t * 1.7f * sp + a * 0.9f - j * 0.7f) * 0.55f - jet * 0.5f;
                    d = (Quaternion.AngleAxis(curl * 0.5f * Mathf.Rad2Deg, axis) * d).normalized;
                    p += d * L;

                    // 將算好的位置與方向存入對應的節點
                    int idx = 3 + a * 3 + j;
                    _pos[idx] = p;
                    _dir[idx] = d;
                }
            }
        }
    }
}