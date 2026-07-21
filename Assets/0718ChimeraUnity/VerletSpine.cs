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

        [Header("地板")]
        [Tooltip("任何一顆 zooid 都不會低於 floorY。垂到底的部分沿著地板往外攤開，不會疊成一坨。")]
        public bool clampToFloor = true;

        [Tooltip("★ 真實世界高度（公尺）。已經把 ChimeraColony 的 colonyScale 換算進去，" +
                 "所以拖 colonyScale 滑桿時這個值不需要跟著改。MRUK 地板通常是 0。")]
        public float floorY = 0f;

        [Tooltip("在 Scene 視窗畫出地板。紅框會對齊 SampleWanderDriver 的活動範圍水平投影。")]
        public bool drawFloorGizmo = true;

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
        ChimeraColony _colony;
        SampleWanderDriver _driver;

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

        // ── 座標空間 ────────────────────────────────────────────────
        // ChimeraColony 擺位時做的是：world = head + (spine - head) * colonyScale。
        // 也就是脊索座標不等於球真正在的位置（colonyScale = 0.29 時差 3.4 倍）。
        // 所以「地板」不能直接拿 _pts[i].y 去比——要先把世界高度換算回脊索空間。
        float ColonyScale()
        {
            if (_colony == null) _colony = GetComponent<ChimeraColony>();
            return _colony != null ? Mathf.Max(_colony.colonyScale, 1e-4f) : 1f;
        }

        /// 世界高度 → 脊索空間高度。anchor 是頭端（縮放的錨點，它自己不動）。
        float SpineFloor(Vector3 anchor) => anchor.y + (floorY - anchor.y) / ColonyScale();

        /// 脊索座標 → 世界座標。gizmo 和外部除錯用。
        public Vector3 ToWorld(Vector3 spinePoint)
        {
            Vector3 anchor = (_pts != null && _pts.Length > 0) ? _pts[0] : transform.position;
            return anchor + (spinePoint - anchor) * ColonyScale();
        }
        // ────────────────────────────────────────────────────────────

        public void Tick(float dt)
        {
            if (_pts == null || _pts.Length != nodeCount) Rebuild();
            if (dt <= 0f) return;

            Vector3 target = headTarget ? headTarget.position : transform.position;
            _pts[0] = Vector3.Lerp(_pts[0], target, 1f - Mathf.Exp(-FOLLOW * dt));

            // 頭端是縮放錨點，它的脊索座標就是世界座標，直接拿 floorY 夾。
            if (clampToFloor && _pts[0].y < floorY) _pts[0].y = floorY;

            float spineFloor = SpineFloor(_pts[0]);

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

                if (clampToFloor && _pts[i].y < spineFloor)
                {
                    _pts[i].y = spineFloor;

                    // 抬起來之後這一節就比 restLength 短了。把差額補到水平分量上，
                    // 讓垂到底的尾巴沿著地板攤開，而不是一節一節疊成一坨。
                    Vector3 e = _pts[i] - _pts[i - 1];
                    float horiz = new Vector2(e.x, e.z).magnitude;
                    float want = restLength * restLength - e.y * e.y;
                    if (want > 0f && horiz > 1e-5f)
                    {
                        float k = Mathf.Sqrt(want) / horiz;
                        _pts[i].x = _pts[i - 1].x + e.x * k;
                        _pts[i].z = _pts[i - 1].z + e.z * k;
                    }

                    // ★ 把垂直速度歸零。verlet 的速度是 (pts - prev) 隱含的，
                    // 只夾 pts 不動 prev 的話，下一幀會讀到一個向上的速度，
                    // 尾巴會在地板上彈跳。
                    _prev[i].y = _pts[i].y;
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
            if (_pts == null || _pts.Length < 2) return;

            // ★ 畫縮放後的位置。之前畫的是脊索原始座標，長度跟球差了 1/colonyScale 倍，
            // 看起來線比身體長一大截——那條線不能拿來判斷任何東西。
            Gizmos.color = Color.cyan;
            for (int i = 1; i < _pts.Length; i++)
                Gizmos.DrawLine(ToWorld(_pts[i - 1]), ToWorld(_pts[i]));

            // 每個節點畫一個小點，跟球一一對應
            Gizmos.color = new Color(0.4f, 1f, 1f, 0.6f);
            for (int i = 0; i < _pts.Length; i++)
                Gizmos.DrawWireSphere(ToWorld(_pts[i]), 0.02f);
        }

        void OnDrawGizmos()
        {
            if (!drawFloorGizmo || !clampToFloor) return;

            // 紅框 = 活動範圍的水平投影。之前畫在 transform.position 上，
            // 跟藍框的 boundsCenter 是兩個不同的參考點，所以永遠對不齊。
            if (_driver == null) _driver = FindFirstObjectByType<SampleWanderDriver>();

            Vector3 c, size;
            if (_driver != null)
            {
                c = new Vector3(_driver.boundsCenter.x, floorY, _driver.boundsCenter.z);
                size = new Vector3(_driver.boundsSize.x, 0f, _driver.boundsSize.z);
            }
            else
            {
                // 找不到 driver 時的退路，畫一個 4×4 佔位框
                c = new Vector3(transform.position.x, floorY, transform.position.z);
                size = new Vector3(4f, 0f, 4f);
            }

            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.8f);
            Gizmos.DrawWireCube(c, size);
        }
    }
}