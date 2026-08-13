using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// 飛龍：長蛇形軀幹 · 頭端蜿蜒帶動全身 S 形 · 一到兩對翼 · 轉彎傾側 · 離地飛行。
    /// 支援兩種航行模式：自由漫遊，或從一個指定起點進入、繞著某個東西盤旋。
    ///
    /// ★ 龍跟鳥的差別不在「有沒有翅膀」，在軀幹長度與軀幹是否參與運動。
    ///   鳥的軀幹是短的一塊剛體，動作全在翼上；龍的軀幹本身就是動作 ——
    ///   頭端左右擺盪，後面每一節沿著頭走過的路徑跟上，S 形是「時間差」長出來的，
    ///   不是每節各自加一個 sin。這跟蛇是同一個機制，只是離地飛。
    ///
    /// ★ 盤旋的做法刻意不是「把座標直接算在圓上」。如果直接指定位置，
    ///   路徑會變成一個完美的圓，蜿蜒就完全消失，整條龍讀成一段圓弧鐵絲。
    ///   這裡改成只控制「朝向」—— 給圓的切線當目標朝向，位置仍然由速度累積出來，
    ///   所以蜿蜒、傾側、身體的時間差全部保留，盤旋是這些之上的一層意圖。
    ///
    /// ★ 上下的 wave 也是同一個道理：只讓頭端的高度走 sin，後面每節沿路徑跟上，
    ///   垂直方向的波就自己沿身體往後傳。不要對每一節分別加 sin ——
    ///   那會變成整條同時上下抖，讀成彈簧而不是波。
    ///
    /// 節點配置：
    ///   0 頭骨 / 1 吻部 / 2 起 軀幹 ×bodySegments /
    ///   翼（每對 2 側 × 4 節）/ 四肢（4 × 2 節，收在腹下）/ 尾 ×tailSegments。
    [ExecuteAlways]
    [AddComponentMenu("Chimera/Body Plan - 飛龍 Dragon")]
    public class PlanDragon : ChimeraBodyPlan
    {
        /// 俯視的迴轉方向。
        public enum OrbitDirection { Left, Right }

        /// 起點從哪裡取。ThisObject = 用這個 GameObject 的 Transform 當虛擬原點。
        public enum StartMode { ThisObject, WorldPosition, TargetTransform, OnOrbit }

        [Header("飛龍")]
        [Tooltip("軀幹節數。改這個會重建整隻。這是「長型」的來源 —— 低於 8 節就會讀成鳥而不是龍。")]
        [Range(6, 18)] public int bodySegments = 11;

        [Tooltip("尾節數。尾接在軀幹末端後面，同樣沿路徑跟上，只是擺幅隨長度放大。")]
        [Range(3, 8)] public int tailSegments = 5;

        [Tooltip("翼對數。兩對時後面那對相位延遲，讀成一道往後傳的波。")]
        [Range(1, 2)] public int wingPairs = 2;

        [Tooltip("要不要四肢。收在腹下、隨飛行微微擺動，不參與推進。")]
        public bool legs = true;

        [Header("原點 (整隻的錨點)")]
        [Tooltip("★ 打勾 = 整個系統以這個 GameObject 為原點：活動範圍盒子、盤旋圓心、起點" +
                 "全部跟著物件移動與旋轉。移動或旋轉 PlanDragon 這個物件，整組東西一起走。\n\n" +
                 "打勾時 Bounds Center 會被這個物件的位置驅動（每幀覆寫），不要再手填 —— " +
                 "要偏移就用下面的 Bounds Center Local。\n" +
                 "不打勾 = 舊行為：Bounds Center 是絕對世界座標，跟物件的 Transform 無關。")]
        public bool anchorToTransform = true;

        [Tooltip("活動範圍盒子的中心相對這個物件的偏移（公尺，會跟著物件旋轉）。" +
                 "通常留 0 —— 也就是盒子就以物件為中心。")]
        public Vector3 boundsCenterLocal = Vector3.zero;

        [Header("飛行")]
        [Tooltip("★ 飛行速度（體制單位／秒）。最終速度 = 這個值 × Speed × Creature Scale，" +
                 "所以把生物調小的時候速度會一起變慢，看起來的「體感速度」不變。\n" +
                 "想要單獨改快慢就動這一個，不要動 Speed —— Speed 同時也在驅動拍翼與蜿蜒的頻率。")]
        [Range(0.2f, 6f)] public float flightSpeed = 2.0f;

        [Tooltip("★ 水平蜿蜒幅度（弧度）。這是龍最重要的一個值 —— 頭端擺得多大，" +
                 "整條身體的 S 就多深。0 = 直線飛的長條，讀起來像木棍。")]
        [Range(0f, 1.2f)] public float weave = 0.55f;

        [Tooltip("蜿蜒頻率。跟 weave 一起決定 S 的「波長」：頻率高 = 波密而短。")]
        [Range(0.2f, 3f)] public float weaveRate = 1.2f;

        [Header("上下起伏 (wave)")]
        [Tooltip("★ 上下擺幅（公尺，不隨 Creature Scale 縮放）。0.15 左右就是「稍微」。\n" +
                 "頭端走 sin，後面每節沿路徑跟上，所以垂直的波會自己沿身體往後傳。\n" +
                 "注意：擺幅 + Orbit Height 要留在活動範圍盒子的高度內，" +
                 "超出去會被 ClampAll 削平成一條直線。")]
        [Range(0f, 1.5f)] public float waveHeight = 0.18f;

        [Tooltip("★ 上下起伏頻率。跟盤旋一起用時，這個值決定「幾圈換一次高度」：" +
                 "調很低（0.1 附近）會變成一圈一圈慢慢爬升／下降的螺旋；" +
                 "調高（1 以上）則是在同一個高度帶上下波動。")]
        [Range(0.05f, 2f)] public float waveRate = 0.7f;

        [Tooltip("拍翼頻率。龍體重，拍得比鳥慢才對得上量感。")]
        [Range(0.3f, 4f)] public float flapRate = 1.2f;

        [Header("盤旋")]
        [Tooltip("打勾 = 繞著一個點盤旋；不打勾 = 原本的自由漫遊（會自己避開邊界）。")]
        public bool orbit = true;

        [Tooltip("★ 迴轉方向（俯視）。Left = 逆時針，Right = 順時針。")]
        public OrbitDirection turnDirection = OrbitDirection.Left;

        [Tooltip("要繞的目標。留空就繞「原點 + 下面那個偏移」。" +
                 "★ 指定 Transform 的意義是可以繞現實空間裡的東西 —— 把它指到 MR 場景裡的物件即可。")]
        public Transform orbitTarget;

        [Tooltip("沒有指定 Transform 時，圓心相對原點的偏移（公尺）。" +
                 "Anchor To Transform 打勾時這個偏移會跟著物件一起旋轉。")]
        public Vector3 orbitCenterOffset = Vector3.zero;

        [Tooltip("★ 盤旋半徑（公尺，不隨 Creature Scale 縮放）。\n" +
                 "活動範圍盒子必須包得住整個圓，否則 ClampAll 會把龍壓在牆上，圓會被切平。" +
                 "Scene 視窗有畫出這個圓（橘色），直接對著青色框框看就知道有沒有超出去。")]
        [Range(0.2f, 8f)] public float orbitRadius = 1.2f;

        [Tooltip("盤旋高度：相對圓心的垂直偏移（公尺）。上下擺幅由上面的 Wave Height 控制。")]
        [Range(-2f, 3f)] public float orbitHeight = 0.4f;

        [Tooltip("★ 修正強度。低 = 半徑鬆散、圓被蜿蜒推得歪歪的（像真的在盤旋）；" +
                 "高 = 死死鎖在圓上，會開始讀成軌道上的機械。2~3 之間最像生物。")]
        [Range(0.3f, 8f)] public float orbitTightness = 2.2f;

        [Tooltip("半徑的緩慢呼吸。0 = 每一圈都一樣大；往上調會時遠時近，破掉「完美圓」的機械感。")]
        [Range(0f, 0.4f)] public float radiusBreath = 0.12f;

        [Header("起點")]
        [Tooltip("起點從哪裡來。\n" +
                 "This Object = ★ 用這個 GameObject 自己的 Transform 當虛擬原點 —— " +
                 "直接用工具列的移動工具拖它就是在調起點，不需要另外建物件。\n" +
                 "World Position = 用下面那組座標（Scene 視窗有可拖的綠色把手）。\n" +
                 "Target Transform = 指到別的物件。\n" +
                 "On Orbit = 舊行為，自動放在圓周上（圓心正 X 方向）。\n\n" +
                 "注意：起點會被活動範圍盒子 ClampAll 夾住。物件放在盒子外面的話，" +
                 "龍會在第一幀就被拉進盒子裡 —— 要嘛把物件移進盒子，要嘛把 Bounds 調大。")]
        public StartMode startMode = StartMode.ThisObject;

        [Tooltip("起點的世界座標（公尺）。Start Mode = World Position 時才用。" +
                 "★ 選到這個模式時 Scene 視窗會出現一個可以直接拖的位置把手。")]
        public Vector3 startPosition = Vector3.zero;

        [Tooltip("起點的 Transform。Start Mode = Target Transform 時才用；留空則退回上面的座標。")]
        public Transform startPoint;

        [Tooltip("★ Start Mode = On Orbit 時，從圓周上的哪個角度出發（度）。\n" +
                 "這就是「為什麼每次都在同一個點生成」的答案 —— 之前寫死在 0 度。\n" +
                 "Anchor To Transform 打勾時這個角度是相對物件的，所以旋轉物件，生成點跟著轉。")]
        [Range(0f, 360f)] public float startAngleDeg = 0f;

        [Tooltip("勾一下 = 立刻回到起點重飛（勾完會自己彈回來）。\n" +
                 "★ 平常不需要用它 —— 起點被移動時會自動回到起點；" +
                 "這格是給你在調完速度、蜿蜒之後想重看一次進場的時候用的。")]
        public bool respawnNow = false;

        [Tooltip("打勾 = 用下面的角度當初始朝向；不打勾 = 自動朝向（盤旋時對著切線，" +
                 "起點在圓外時對著圓）。")]
        public bool useStartHeading = false;

        [Tooltip("初始朝向（度）。0 = +X，90 = +Z。")]
        [Range(-180f, 180f)] public float startHeadingDeg = 0f;

        readonly ChimeraPath _path = new ChimeraPath();
        Vector3 _head;

        int _segs, _tails, _pairs;
        int _body0, _wing0, _leg0, _tail0;

        float _turn;        // 平滑後的轉向率，用來算傾側
        float _prevHw;      // 上一幀的實際朝向（含蜿蜒），傾側要跟著蜿蜒一起壓
        Vector3 _startedFrom = new Vector3(float.NaN, 0f, 0f);   // 這一趟是從哪個起點出發的

        /// ★ 把活動範圍盒子的中心綁到這個物件上。基底類別的 SteerHeading / ClampAll / Gizmo
        ///   全部讀 boundsCenter 這個欄位，所以只要在用到它之前覆寫一次，
        ///   整組行為（漫遊、夾邊界、畫框）就一起跟著物件走 —— 不需要改動基底類別。
        void SyncAnchor()
        {
            if (anchorToTransform)
                boundsCenter = transform.position + transform.rotation * boundsCenterLocal;
        }

        /// 原點：物件位置（有錨定）或欄位裡的絕對座標。
        Vector3 Origin => anchorToTransform
            ? transform.position + transform.rotation * boundsCenterLocal
            : boundsCenter;

        Vector3 OrbitCenter => orbitTarget != null
            ? orbitTarget.position
            : Origin + (anchorToTransform ? transform.rotation * orbitCenterOffset : orbitCenterOffset);

        float TurnSign => turnDirection == OrbitDirection.Right ? -1f : 1f;

        /// 圓周上某個角度的徑向（水平）。錨定時跟著物件的 Y 旋轉一起轉。
        Vector3 RingRadial(float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            Vector3 v = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            if (anchorToTransform) v = transform.rotation * v;
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.right;
        }

        /// 這一幀實際會用的起點（世界座標）。Gizmo 也讀這個，
        /// 所以顯示與行為不可能不一致。
        public Vector3 ResolvedStart
        {
            get
            {
                switch (startMode)
                {
                    case StartMode.ThisObject: return transform.position;
                    case StartMode.WorldPosition: return startPosition;
                    case StartMode.TargetTransform:
                        return startPoint != null ? startPoint.position : startPosition;
                    default:
                        if (!orbit) return Origin;
                        Vector3 c = OrbitCenter;
                        return new Vector3(c.x, c.y + orbitHeight, c.z) + RingRadial(startAngleDeg) * orbitRadius;
                }
            }
        }

        protected override void Layout()
        {
            _segs = Mathf.Clamp(bodySegments, 6, 18);
            _tails = Mathf.Clamp(tailSegments, 3, 8);
            _pairs = Mathf.Clamp(wingPairs, 1, 2);

            var roles = new List<ChimeraRole>();
            var sc = new List<float>();

            roles.Add(ChimeraRole.Head); sc.Add(1.15f);   // 頭骨
            roles.Add(ChimeraRole.Head); sc.Add(0.78f);   // 吻部

            // ★ 軀幹前粗後細，落差要看得出來，否則長條會讀成蜈蚣（等粗分節）
            _body0 = roles.Count;
            for (int i = 0; i < _segs; i++)
            {
                float u = _segs == 1 ? 0f : (float)i / (_segs - 1);
                roles.Add(ChimeraRole.Trunk);
                sc.Add(Mathf.Lerp(1.45f, 0.72f, u));
            }

            _wing0 = roles.Count;
            for (int w = 0; w < _pairs * 2; w++)
                for (int j = 0; j < 4; j++) { roles.Add(ChimeraRole.Limb); sc.Add(0.82f - j * 0.14f); }

            _leg0 = -1;
            if (legs)
            {
                _leg0 = roles.Count;
                for (int l = 0; l < 4; l++)
                {
                    roles.Add(ChimeraRole.Limb); sc.Add(0.52f);   // 股
                    roles.Add(ChimeraRole.Limb); sc.Add(0.38f);   // 爪
                }
            }

            _tail0 = roles.Count;
            for (int i = 0; i < _tails; i++)
            {
                float u = _tails == 1 ? 0f : (float)i / (_tails - 1);
                roles.Add(ChimeraRole.Tail);
                sc.Add(Mathf.Lerp(0.62f, 0.26f, u));
            }

            Alloc(roles, sc);
        }

        /// Inspector 齒輪選單：把目前這個 GameObject 的位置抄成 World Position 起點。
        [ContextMenu("起點 ← 抄目前這個物件的位置")]
        public void CopyTransformToStart()
        {
            startPosition = transform.position;
            startMode = StartMode.WorldPosition;
            Rebuild();
        }

        /// Inspector 齒輪選單：立刻回到起點重飛一次。
        [ContextMenu("回到起點 Respawn")]
        public void Respawn() => Rebuild();

        protected override void OnValidate()
        {
            if (respawnNow) respawnNow = false;
            base.OnValidate();      // 這裡面會 Rebuild()，所以任何 Inspector 改動都會回到起點
        }

        protected override void ResetState()
        {
            SyncAnchor();
            Vector3 c = OrbitCenter;
            _head = ResolvedStart;
            _startedFrom = _head;

            // ── 初始朝向 ────────────────────────────────────────
            if (useStartHeading)
            {
                _heading = startHeadingDeg * Mathf.Deg2Rad;
            }
            else if (orbit)
            {
                Vector3 toC = c - _head; toC.y = 0f;
                float dist = toC.magnitude;
                if (dist < 1e-4f) { toC = new Vector3(1f, 0f, 0f); dist = 1e-4f; }
                Vector3 inward = toC / dist;
                Vector3 tangent = new Vector3(-inward.z, 0f, inward.x) * TurnSign;
                // 起點在圓外時先朝圓飛（比例照 SteerOrbit 的修正項），在圓周上就是純切線
                float err = Mathf.Clamp((dist - orbitRadius) / Mathf.Max(0.2f, orbitRadius), -1f, 1f);
                Vector3 want = (tangent + inward * (err * 1.2f)).normalized;
                _heading = Mathf.Atan2(want.z, want.x);
            }
            else
            {
                _heading = 0f;
            }

            _path.Reset(_head);
            _prevHw = _heading; _turn = 0f;
            if (_pos != null) for (int i = 0; i < _pos.Length; i++) _pos[i] = _head;
        }

        protected override void Solve(float t, float dt)
        {
            float K = creatureScale, sp = speed;

            // ★ 每幀先把盒子中心綁到物件上，後面所有讀 boundsCenter 的東西才會跟著走
            SyncAnchor();

            // ★ 拖 Transform 不會觸發 OnValidate，所以自己盯著起點有沒有被移動。
            //   這一段是「在編輯器裡拖物件 → 龍跟著搬家」唯一的驅動來源，
            //   不做的話你會拖了物件但龍還停在舊位置。只在非播放時作用，
            //   跑起來之後起點就不該再影響飛行。
            if (!Application.isPlaying)
            {
                Vector3 s = ResolvedStart;
                if (float.IsNaN(_startedFrom.x) || (s - _startedFrom).sqrMagnitude > 1e-8f)
                {
                    ResetState();
                    return;
                }
            }

            // ── 航行意圖：盤旋或漫遊 ─────────────────────────────
            if (orbit) SteerOrbit(t, dt);
            else SteerHeading(_head, 0.4f, 1.2f, dt);

            // ★ 蜿蜒疊在轉向之上：_heading 決定「要去哪」，這一項決定「怎麼去」。
            //   只動頭端，軀幹不另外加擺動 —— S 形由路徑的時間差自己長出來。
            float hw = _heading + Mathf.Sin(t * weaveRate * sp) * weave;
            Vector3 fwd = new Vector3(Mathf.Cos(hw), 0f, Mathf.Sin(hw));
            Vector3 side = new Vector3(-Mathf.Sin(hw), 0f, Mathf.Cos(hw));

            // 傾側取自實際朝向的變化率（含蜿蜒、盤旋與撞牆折返），
            // 所以持續盤旋時會維持一個固定的側傾角 —— 這是盤旋最關鍵的一眼
            float rate = Mathf.DeltaAngle(_prevHw * Mathf.Rad2Deg, hw * Mathf.Rad2Deg)
                         * Mathf.Deg2Rad / Mathf.Max(1e-4f, dt);
            _prevHw = hw;
            _turn = Mathf.Lerp(_turn, Mathf.Clamp(rate, -3f, 3f), Mathf.Min(1f, dt * 4f));

            _head += fwd * (flightSpeed * sp * K * dt);

            // ── 上下 wave：只動頭端的高度，波靠路徑往後傳 ────────
            float baseY = orbit ? OrbitCenter.y + orbitHeight : boundsCenter.y;
            _head.y = baseY + Mathf.Sin(t * waveRate * sp * Mathf.PI * 2f) * waveHeight;
            _head = ClampAll(_head);
            _path.Push(_head);

            float SEG = 0.52f * K;

            // ── 頭 ───────────────────────────────────────────────
            _pos[0] = _head;
            _dir[0] = fwd;
            _pos[1] = _head + fwd * (0.30f * K) - Vector3.up * (0.06f * K);
            // ★ 吻部刻意朝後：頭部節點的口是往 local -up 長的，朝後才會把口開在臉的最前面。
            _dir[1] = (_pos[0] - _pos[1]).normalized;

            // ── 軀幹：沿頭走過的路徑跟上 ─────────────────────────
            for (int i = 0; i < _segs; i++) _pos[_body0 + i] = _path.At((i + 1) * SEG);
            DirsAlongChain(_body0, _body0 + _segs - 1);

            // 傾側：把 up / side 繞前進軸旋轉
            float bank = -_turn * 0.55f;
            Quaternion roll = Quaternion.AngleAxis(bank * Mathf.Rad2Deg, fwd);
            Vector3 up = roll * Vector3.up;
            Vector3 wside = roll * side;

            // ── 翼 ───────────────────────────────────────────────
            for (int p = 0; p < _pairs; p++)
            {
                int anchor = Mathf.Min(_segs - 1, 1 + p * 3);
                Vector3 shoulder = _pos[_body0 + anchor];
                float pairLag = p * 0.8f;
                float spanMul = 1f - p * 0.15f;

                for (int s = 0; s < 2; s++)
                {
                    float sgn = s == 0 ? 1f : -1f;
                    for (int j = 0; j < 4; j++)
                    {
                        float ph = t * flapRate * 2.6f * sp - j * 0.5f - pairLag;
                        float lift = Mathf.Sin(ph) * 0.5f * ((j + 1) / 4f) * K;
                        int idx = _wing0 + (p * 2 + s) * 4 + j;

                        _pos[idx] = shoulder
                            + wside * (sgn * (j + 1) * 0.50f * spanMul * K)
                            + up * lift
                            + fwd * (-0.12f * j * K);
                        _dir[idx] = (wside * sgn + up * (Mathf.Cos(ph) * 0.5f)).normalized;
                    }
                }
            }

            // ── 四肢：收在腹下，只有懸垂與微擺，不推進 ───────────
            if (_leg0 >= 0)
            {
                for (int l = 0; l < 4; l++)
                {
                    bool front = l < 2;
                    float sgn = (l % 2 == 0) ? 1f : -1f;
                    int anchor = Mathf.Min(_segs - 1, front ? 2 : Mathf.Max(3, _segs - 4));
                    Vector3 at = _pos[_body0 + anchor];

                    float swing = Mathf.Sin(t * 1.6f * sp + l * 1.7f) * 0.10f * K;
                    Vector3 hip = at + wside * (sgn * 0.22f * K) - up * (0.18f * K);
                    Vector3 claw = hip - up * (0.44f * K) + fwd * (0.08f * K + swing);

                    int b = _leg0 + l * 2;
                    _pos[b] = hip; _dir[b] = -(claw - hip).normalized;
                    _pos[b + 1] = claw; _dir[b + 1] = (hip - claw).normalized;
                }
            }

            // ── 尾：接在軀幹末端後面，擺幅隨長度放大 ─────────────
            for (int i = 0; i < _tails; i++)
            {
                Vector3 p = _path.At((_segs + 1 + i * 1.05f) * SEG);
                p += side * (Mathf.Sin(t * 2.2f * sp - i * 0.9f) * 0.055f * (i + 1) * K);
                _pos[_tail0 + i] = p;

                Vector3 prev = i == 0 ? _pos[_body0 + _segs - 1] : _pos[_tail0 + i - 1];
                Vector3 d = prev - p;
                _dir[_tail0 + i] = d.sqrMagnitude > 1e-8f ? d.normalized : fwd;
            }
        }

        /// 盤旋：把朝向拉向圓的切線，再依「現在的半徑跟目標差多少」往內或往外偏一點。
        /// 只碰 _heading，位置照舊由速度累積 —— 蜿蜒因此還在。
        void SteerOrbit(float t, float dt)
        {
            Vector3 c = OrbitCenter;
            Vector3 toC = c - _head; toC.y = 0f;

            float dist = toC.magnitude;
            if (dist < 1e-4f) { toC = new Vector3(1f, 0f, 0f); dist = 1e-4f; }
            Vector3 inward = toC / dist;

            float rad = Mathf.Max(0.05f, orbitRadius * (1f + Mathf.Sin(t * 0.37f) * radiusBreath));

            // 切線（俯視左轉／右轉）
            Vector3 tangent = new Vector3(-inward.z, 0f, inward.x) * TurnSign;

            // 半徑誤差：正 = 太遠（往內收），負 = 太近（往外推）
            float err = Mathf.Clamp((dist - rad) / Mathf.Max(0.2f, rad), -1f, 1f);
            Vector3 want = (tangent + inward * (err * 1.2f)).normalized;

            float wantH = Mathf.Atan2(want.z, want.x);
            float h = Mathf.LerpAngle(_heading * Mathf.Rad2Deg, wantH * Mathf.Rad2Deg,
                                      Mathf.Min(1f, dt * orbitTightness));
            _heading = h * Mathf.Deg2Rad;
        }

        // ── Gizmo：畫出盤旋的圓、起伏帶與起點 ────────────────────
        protected override void OnDrawGizmos()
        {
            SyncAnchor();          // 畫框框之前先同步，否則 Gizmo 會慢一幀
            base.OnDrawGizmos();
            if (!drawGizmos) return;

            if (orbit)
            {
                Vector3 c = OrbitCenter;
                float yMid = c.y + orbitHeight;

                Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
                DrawRing(new Vector3(c.x, yMid, c.z), orbitRadius);
                if (waveHeight > 0.001f)
                {
                    Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.3f);
                    DrawRing(new Vector3(c.x, yMid + waveHeight, c.z), orbitRadius);
                    DrawRing(new Vector3(c.x, yMid - waveHeight, c.z), orbitRadius);
                }
                Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.6f);
                Gizmos.DrawLine(new Vector3(c.x, yMid - waveHeight, c.z),
                                new Vector3(c.x, yMid + waveHeight, c.z));
            }

            // 起點：綠色小球 + 一條指向初始朝向的線
            Vector3 s = ResolvedStart;
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireSphere(s, 0.06f);
            float hh = useStartHeading ? startHeadingDeg * Mathf.Deg2Rad : _heading;
            Gizmos.DrawLine(s, s + new Vector3(Mathf.Cos(hh), 0f, Mathf.Sin(hh)) * 0.35f);
        }

        static void DrawRing(Vector3 center, float r)
        {
            const int N = 40;
            Vector3 prev = center + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= N; i++)
            {
                float a = (float)i / N * Mathf.PI * 2f;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}