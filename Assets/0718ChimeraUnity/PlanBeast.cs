using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// 獸（馬型四足，頸部可分岔成多頭）：
    /// 剛性深胸廓 · 頸從鬐甲揚起後分岔 · 前後肢反向彎曲 · 對角步態 · 不打滑步幅。
    ///
    /// ★ 多頭不是「把頭複製一份放旁邊」。分岔點在頸的中段，
    ///   兩條頸從同一個基部長出去再各自往外撇 —— 共用的那一段才是「同一隻」的證據。
    ///   從肩膀直接長兩根平行的頸會讀成兩隻動物黏在一起。
    ///
    /// 節點配置（H = 頭數）：
    ///   0 頸基 / 1 分岔點 / 2 起每顆頭 4 節（頸中·頸上·頭骨·吻部）/
    ///   接著 鬐甲·胸廓前·胸廓後·尻 / 四肢（髖膝足 ×4）/ 尾 ×4。
    ///   共 22 + 4H 節。雙頭 = 30 節。
    [ExecuteAlways]
    [AddComponentMenu("Chimera/Body Plan - 獸 Beast")]
    public class PlanBeast : ChimeraBodyPlan
    {
        [Header("獸")]
        [Tooltip("頭數。改這個會重建整隻。頸會在中段分岔，共用的基部保留。")]
        [Range(1, 3)] public int heads = 2;

        [Tooltip("兩顆頭往外撇開的幅度。0 = 平行併攏（會糊成一團），太大則各自獨立、失去同源感。")]
        [Range(0f, 1.2f)] public float headSpread = 0.45f;

        [Tooltip("頸長。1 = 馬。往下調會變成犬科那種短頸，往上調會變成長頸鹿。")]
        [Range(0.4f, 1.8f)] public float neckLength = 1f;

        [Tooltip("腿長。1 = 馬。腿長相對於胸廓深度是「有蹄類 vs 爬蟲」最直接的分界。")]
        [Range(0.7f, 1.4f)] public float legLength = 1f;

        [Tooltip("軀幹離地高度（體制單位）。跟腿長要一起調，太高腿會打直、太低會蹲。")]
        [Range(0.9f, 1.7f)] public float bodyHeight = 1.30f;

        [Tooltip("★ 步頻（speed = 1 時每秒幾個完整步態週期）。\n" +
                 "步幅不用調，也不能調 —— 它由「移動速度 ÷ 步頻」自動算出來，" +
                 "這樣腳掌著地時才不會在地上滑。\n" +
                 "調低 = 大步慢走，調高 = 碎步快走。")]
        [Range(0.4f, 3f)] public float gaitRate = 1.0f;

        [Tooltip("軀幹隨步態的上下起伏。0 = 完全平穩。")]
        [Range(0f, 0.12f)] public float bodyBob = 0.035f;

        readonly ChimeraPath _path = new ChimeraPath();
        Vector3 _root;

        // 前進速度（體制單位／秒，speed = 1 時）。步幅由這個值推出來。
        const float FORWARD_SPEED = 1.3f;

        // 索引在 Layout() 算出來（節點數隨頭數變動，不能寫死常數）
        int _heads;
        int _neckBase, _neckFork, _branch0;
        int _withers, _barrelF, _barrelB, _croup;
        int _leg0, _tail0;

        // 對角步態：左前與右後同相，右前與左後同相。順序 FL FR HL HR。
        static readonly float[] GaitPhase = { 0f, 0.5f, 0.5f, 0f };
        static readonly float[] Lateral = { 1f, -1f, 1f, -1f };

        // 每顆頭在分岔後的四節，相對分岔點的偏移（前, 上, 側）。側向再乘 headSpread。
        static readonly Vector3[] BranchOffset =
        {
            new Vector3(0.20f, 0.24f, 0.35f),   // 頸中
            new Vector3(0.38f, 0.46f, 0.72f),   // 頸上
            new Vector3(0.58f, 0.58f, 1.00f),   // 頭骨
            new Vector3(0.92f, 0.42f, 1.18f),   // 吻部（往前下方伸，臉是斜的）
        };

        protected override void Layout()
        {
            _heads = Mathf.Clamp(heads, 1, 3);
            float H = _heads;

            var roles = new List<ChimeraRole>();
            var sc = new List<float>();

            _neckBase = 0; roles.Add(ChimeraRole.Trunk); sc.Add(1.05f);
            _neckFork = 1; roles.Add(ChimeraRole.Trunk); sc.Add(0.98f);

            // ★ 分岔後每條頸都要變細，否則兩條粗頸的總量會壓過胸廓，
            //   整隻的重心讀起來會跑到頭上。頭本身不用縮那麼多。
            float neckThin = 1f / Mathf.Sqrt(H);
            float headThin = Mathf.Pow(H, -0.22f);

            _branch0 = roles.Count;
            for (int h = 0; h < _heads; h++)
            {
                roles.Add(ChimeraRole.Trunk); sc.Add(0.86f * neckThin);   // 頸中
                roles.Add(ChimeraRole.Trunk); sc.Add(0.78f * neckThin);   // 頸上
                roles.Add(ChimeraRole.Head); sc.Add(1.02f * headThin);   // 頭骨
                roles.Add(ChimeraRole.Head); sc.Add(0.68f * headThin);   // 吻部
            }

            // 頸細、胸廓粗，落差要夠大 —— 粗細一致就會讀成分節的蟲。
            _withers = roles.Count; roles.Add(ChimeraRole.Trunk); sc.Add(1.55f);
            _barrelF = roles.Count; roles.Add(ChimeraRole.Trunk); sc.Add(1.75f);
            _barrelB = roles.Count; roles.Add(ChimeraRole.Trunk); sc.Add(1.62f);
            _croup = roles.Count; roles.Add(ChimeraRole.Trunk); sc.Add(1.35f);

            _leg0 = roles.Count;
            for (int l = 0; l < 4; l++)
            {
                bool front = l < 2;
                roles.Add(ChimeraRole.Limb); sc.Add(front ? 0.70f : 0.78f);
                roles.Add(ChimeraRole.Limb); sc.Add(front ? 0.54f : 0.58f);
                roles.Add(ChimeraRole.Limb); sc.Add(front ? 0.46f : 0.48f);
            }

            _tail0 = roles.Count;
            for (int i = 0; i < 4; i++) { roles.Add(ChimeraRole.Tail); sc.Add(0.62f - i * 0.11f); }

            Alloc(roles, sc);
        }

        protected override void ResetState()
        {
            float K = creatureScale;
            _root = new Vector3(boundsCenter.x, floorY + bodyHeight * K, boundsCenter.z);
            _path.Reset(_root);
            _heading = 0f;
            if (_pos != null) for (int i = 0; i < _pos.Length; i++) _pos[i] = _root;
        }

        protected override void Solve(float t, float dt)
        {
            float K = creatureScale, sp = speed;

            SteerHeading(_root, 0.35f, 0.8f, dt);
            Vector3 fwd = Fwd, side = Side, up = Vector3.up;

            // 移動速度與步頻是同一組數字的兩面：步幅由這兩個算出來，不另外給滑桿。
            float bodySpeed = FORWARD_SPEED * sp * K;
            float gaitHz = Mathf.Max(0.05f, gaitRate * sp);
            float stride = SlipFreeStride(bodySpeed, gaitHz);

            _root += fwd * (bodySpeed * dt);
            _root = ClampHorizontal(_root);
            // 上下擺動綁在步頻的兩倍上（一個步態週期兩次落地）
            _root.y = floorY + (bodyHeight + Mathf.Sin(t * gaitHz * 4f * Mathf.PI) * bodyBob) * K;
            _path.Push(_root);

            // ── 軀幹：剛體，不沿路徑蠕動 ─────────────────────────
            // 四足獸的胸廓是一塊硬的，它整塊轉，不會像蛇那樣彎成 S 形。
            _pos[_withers] = _root + fwd * (0.75f * K);
            _pos[_barrelF] = _root + fwd * (0.25f * K);
            _pos[_barrelB] = _root - fwd * (0.25f * K);
            _pos[_croup] = _root - fwd * (0.75f * K);
            for (int i = _withers; i <= _croup; i++) _dir[i] = fwd;

            // ── 頸：從鬐甲揚起，中段分岔 ─────────────────────────
            float NL = neckLength * K;
            float bob = Mathf.Sin(t * gaitHz * 4f * Mathf.PI + 0.8f) * 0.055f * K;
            Vector3 W = _pos[_withers];

            _pos[_neckBase] = W + fwd * (0.24f * NL) + up * (0.28f * NL + bob);
            Vector3 F = W + fwd * (0.44f * NL) + up * (0.54f * NL + bob);
            _pos[_neckFork] = F;

            Vector3 meanMid = Vector3.zero;

            for (int h = 0; h < _heads; h++)
            {
                // -1 .. +1。單頭時是 0，直接退化成一條直頸。
                float lat = _heads == 1 ? 0f : ((float)h / (_heads - 1)) * 2f - 1f;

                // ★ 每顆頭給自己的相位：兩顆頭同步點動會讀成一個剛體上的裝飾，
                //   錯開之後才會像兩顆各自有意識的頭。
                float hp = t * 0.9f * sp + h * 2.1f;
                float ownBob = Mathf.Sin(hp) * 0.045f * K;
                float ownSway = Mathf.Sin(hp * 0.7f + 1.3f) * 0.06f * K;

                int b = _branch0 + h * 4;
                for (int j = 0; j < 4; j++)
                {
                    Vector3 o = BranchOffset[j];
                    float grow = (j + 1) / 4f;              // 越靠近頭端，自主擺動越明顯
                    _pos[b + j] = F
                        + fwd * (o.x * NL)
                        + up * (o.y * NL + ownBob * grow)
                        + side * (lat * o.z * headSpread * NL + ownSway * grow);
                }

                meanMid += _pos[b];

                // 朝向：沿著這條頸往頭端串，形態才會疊成一條頸而不是一串球
                _dir[b] = (_pos[b + 1] - F).normalized;
                _dir[b + 1] = (_pos[b + 2] - _pos[b]).normalized;
                _dir[b + 2] = (_pos[b + 3] - _pos[b + 1]).normalized;
                // ★ 吻部刻意朝後：頭部節點的口是往 local -up 長的，
                //   朝後才會把口開在臉的最前面。
                _dir[b + 3] = (_pos[b + 2] - _pos[b + 3]).normalized;
            }

            meanMid /= _heads;
            _dir[_neckBase] = (F - W).normalized;
            _dir[_neckFork] = (meanMid - _pos[_neckBase]).normalized;

            // ── 尾：從尻往後下方垂 ───────────────────────────────
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = _pos[_croup]
                    - fwd * ((0.30f + 0.28f * i) * K)
                    + up * ((0.18f - 0.16f * i) * K)
                    + side * (Mathf.Sin(t * 3f * sp - i * 0.8f) * 0.05f * (i + 1) * K);
                _pos[_tail0 + i] = p;
                _dir[_tail0 + i] = i == 0
                    ? (_pos[_croup] - p).normalized
                    : (_pos[_tail0 + i - 1] - p).normalized;
            }

            // ── 四肢 ─────────────────────────────────────────────
            float LL = legLength;
            // 抬腳高度綁在步幅上：碎步就該抬得低，大步才抬得高
            float lift = Mathf.Max(0.08f * K, stride * 0.6f);

            for (int l = 0; l < 4; l++)
            {
                bool front = l < 2;

                Vector3 hip = front
                    ? _pos[_withers] + side * (Lateral[l] * 0.30f * K) - up * (0.15f * K)
                    : _pos[_croup] + side * (Lateral[l] * 0.32f * K) - up * (0.10f * K);

                float l1 = (front ? 0.58f : 0.64f) * LL * K;
                float l2 = (front ? 0.60f : 0.66f) * LL * K;

                GaitOffset(t * gaitHz + GaitPhase[l], out float gx, out float gy);

                Vector3 foot = hip
                    + fwd * (gx * stride + (front ? 0.06f : -0.04f) * K)
                    + up * (-(front ? 1.09f : 1.14f) * LL * K + gy * lift);
                foot.y = Mathf.Max(floorY + 0.06f * K, foot.y);

                // ★ 前肢往後彎、後肢往前彎。這一條是「哺乳類」跟「蟲」最短的分界線。
                Vector3 pole = ((front ? -fwd : fwd) + Vector3.down * 0.15f).normalized;
                Vector3 knee = Knee(hip, foot, l1, l2, pole);

                int b = _leg0 + l * 3;
                _pos[b] = hip; _dir[b] = -(knee - hip).normalized;
                _pos[b + 1] = knee; _dir[b + 1] = (hip - knee).normalized;
                _pos[b + 2] = foot; _dir[b + 2] = (knee - foot).normalized;
            }
        }
    }
}