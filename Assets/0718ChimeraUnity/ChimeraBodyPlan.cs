using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// 節點角色。決定 ChimeraMeshBuilder 長哪一類附肢。
    /// ★ 這取代了原本的 Zone（靠位置比例決定）。體制生物的差異來自骨架拓撲，
    ///    所以角色必須由骨架直接指定，不能再從 index/count 推。
    public enum ChimeraRole { Head, Trunk, Limb, Tail, Drift, Shell }

    /// 體制（body plan）介面。在 ISpineProvider 之上多告訴 ChimeraColony 兩件事：
    /// 每個節點是什麼角色、每個節點多大。
    public interface IBodyPlan : ISpineProvider
    {
        ChimeraRole GetRole(int i);

        /// 節點半徑（世界單位，公尺）。已經含 creatureScale，
        /// 所以 ChimeraColony 不需要知道生物有多大。
        float GetNodeRadius(int i);
    }

    /// 頭部走過的路徑歷史。軀幹沿著同一條路跟上 —— 獸、鳥、海兔共用。
    public class ChimeraPath
    {
        readonly List<Vector3> _pts = new List<Vector3>();
        readonly List<float> _len = new List<float>();

        public void Reset(Vector3 p)
        {
            _pts.Clear(); _len.Clear();
            _pts.Add(p); _len.Add(0f);
        }

        public void Push(Vector3 p)
        {
            if (_pts.Count == 0) { Reset(p); return; }
            Vector3 last = _pts[_pts.Count - 1];
            float d = Vector3.Distance(p, last);
            if (d < 0.005f) return;                       // 太近就不記，避免路徑被無數重複點塞爆
            _pts.Add(p);
            _len.Add(_len[_len.Count - 1] + d);
            while (_len[_len.Count - 1] - _len[0] > 60f) { _pts.RemoveAt(0); _len.RemoveAt(0); }
        }

        /// 從頭端往回 s 公尺的位置
        public Vector3 At(float s)
        {
            if (_pts.Count == 0) return Vector3.zero;
            float total = _len[_len.Count - 1];
            float target = total - s;
            if (target <= _len[0]) return _pts[0];
            for (int i = _len.Count - 1; i > 0; i--)
            {
                if (_len[i - 1] <= target)
                {
                    float t = (target - _len[i - 1]) / Mathf.Max(1e-6f, _len[i] - _len[i - 1]);
                    return Vector3.Lerp(_pts[i - 1], _pts[i], t);
                }
            }
            return _pts[0];
        }
    }

    /// 所有體制的共同基底。
    /// 子類別只要做兩件事：Layout() 宣告骨架（角色 + 節點大小），Solve() 每幀算位置與朝向。
    /// 移動、邊界、地板、gizmo、跟 ChimeraColony 的介面全部在這裡處理完。
    [ExecuteAlways]
    public abstract class ChimeraBodyPlan : MonoBehaviour, IBodyPlan
    {
        // 節點半徑 = sc * creatureScale * NODE_R。0.30 是從瀏覽器原型抄過來的比例，
        // 改這個會讓所有體制同時變胖／變瘦。
        protected const float NODE_R = 0.30f;

        [Header("體型")]
        [Tooltip("★ 整隻生物的真實尺寸倍率。1 ≈ 2~3 公尺長。骨架長度、腳長、步幅、移動速度" +
                 "全部一起縮放，所以不管調到多小，腳都還是踩在 Floor Y 上。" +
                 "用這個調大小，ChimeraColony 的 Colony Scale 請保持 1。")]
        [Range(0.05f, 3f)] public float creatureScale = 0.35f;

        [Header("活動範圍 (MR 魚缸設定)")]
        [Tooltip("範圍中心（世界座標）。")]
        public Vector3 boundsCenter = new Vector3(0f, 1.2f, 0f);

        [Tooltip("★ 完整邊長，不是半徑。3×3 的房間就填 (3, 2, 3)。")]
        public Vector3 boundsSize = new Vector3(3f, 2f, 3f);

        // ========================================================
        // ★ 新增：將旋轉功能完美整合在父類別，所有動物直接受惠
        // ========================================================
        [Tooltip("旋轉角度。可讓魚缸傾斜或對齊現實房間的牆壁。")]
        public Vector3 boundsRotation = Vector3.zero;

        [Tooltip("打勾後，直接使用此 GameObject 的 Transform 旋轉來控制魚缸角度，非常直覺！")]
        public bool useTransformRotation = true;
        // ========================================================

        [Tooltip("地面高度（世界座標，公尺）。MRUK 地板通常是 0。貼地的體制會踩在這個高度上。")]
        public float floorY = 0f;

        [Tooltip("在 Scene 視窗畫出範圍與骨架。")]
        public bool drawGizmos = true;

        [Header("運動")]
        [Range(0f, 3f)] public float speed = 0.8f;

        [Header("診斷")]
        [Tooltip("用 unscaledDeltaTime 推進。timeScale 被設成 0 時仍然會動。")]
        public bool useUnscaledTime = true;

        // ── 狀態 ────────────────────────────────────────────────
        protected Vector3[] _pos;
        protected Vector3[] _dir;
        protected float[] _sc;       // 節點大小係數（體制單位，不含 creatureScale）
        protected float[] _dyn;      // 每幀動態縮放（章魚外套膜收縮之類），Solve 前會被歸 1
        protected ChimeraRole[] _roles;
        protected float _t;          // 自己累積的時間，不讀 Time.time
        protected float _heading;    // 水平朝向（弧度）
        bool _laidOut;

        // 內部取得旋轉角度的捷徑
        protected Quaternion BoundsRot => useTransformRotation ? transform.rotation : Quaternion.Euler(boundsRotation);

        /// 子類別實作：宣告骨架。用 Alloc() 把角色與大小填進去。
        protected abstract void Layout();

        /// 子類別實作：每幀解出 _pos / _dir。t 是累積時間，dt 已夾上限。
        protected abstract void Solve(float t, float dt);

        /// 子類別實作：重置到初始位置（Rebuild 時呼叫）。
        protected abstract void ResetState();

        // ── IBodyPlan / ISpineProvider ──────────────────────────
        public int Count => _pos == null ? 0 : _pos.Length;
        public Vector3 GetPoint(int i) => _pos[Mathf.Clamp(i, 0, _pos.Length - 1)];
        public Vector3 GetForward(int i)
        {
            Vector3 d = _dir[Mathf.Clamp(i, 0, _dir.Length - 1)];
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.up;
        }
        public ChimeraRole GetRole(int i) => _roles[Mathf.Clamp(i, 0, _roles.Length - 1)];
        public float GetNodeRadius(int i)
        {
            int k = Mathf.Clamp(i, 0, _sc.Length - 1);
            return _sc[k] * _dyn[k] * creatureScale * NODE_R;
        }

        public void Tick(float dt)
        {
            if (!_laidOut || _pos == null) Rebuild();
            if (dt <= 0f) return;
            _t += dt;
            for (int i = 0; i < _dyn.Length; i++) _dyn[i] = 1f;
            Solve(_t, dt);
        }

        // ── 生命週期 ────────────────────────────────────────────
        protected virtual void OnEnable() => Rebuild();

        protected virtual void OnValidate()
        {
            Rebuild();
            // 節點數變了就必須通知群體重建，否則滑桿看起來沒作用
            var colony = GetComponent<ChimeraColony>();
            if (colony != null) colony.rebuildNow = true;
        }

        public void Rebuild()
        {
            Layout();
            ResetState();
            _laidOut = true;
        }

        /// 子類別在 Layout() 裡呼叫這個配置陣列。
        protected void Alloc(List<ChimeraRole> roles, List<float> sc)
        {
            int n = roles.Count;
            _roles = roles.ToArray();
            _sc = sc.ToArray();
            _pos = new Vector3[n];
            _dir = new Vector3[n];
            _dyn = new float[n];
            for (int i = 0; i < n; i++) { _dir[i] = Vector3.up; _dyn[i] = 1f; }
        }

        // ── 移動輔助 (已全面升級為支援旋轉的 OBB 空間魔法) ──────────────
        /// 水平漫遊：基礎擺動 + 靠近邊界時把 heading 轉回中心。
        protected void SteerHeading(Vector3 p, float wanderRate, float wanderAmount, float dt)
        {
            _heading += Mathf.Sin(_t * wanderRate) * wanderAmount * dt * speed;

            Vector3 half = boundsSize * 0.5f;
            
            // ★ 將世界座標轉換為旋轉後的魚缸局部座標
            Vector3 localP = Quaternion.Inverse(BoundsRot) * (p - boundsCenter);

            float ox = Mathf.Abs(localP.x) / Mathf.Max(1e-4f, half.x);
            float oz = Mathf.Abs(localP.z) / Mathf.Max(1e-4f, half.z);
            float edge = Mathf.Clamp01((Mathf.Max(ox, oz) - 0.70f) / 0.30f);
            if (edge <= 0f) return;

            Vector3 toC = boundsCenter - p; toC.y = 0f;
            if (toC.sqrMagnitude < 1e-6f) return;
            float want = Mathf.Atan2(toC.z, toC.x);
            float diff = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            _heading += diff * edge * Mathf.Min(1f, dt * 4f);
        }

        /// 最後一道保險：把水平位置壓回盒子裡（轉向來不及時才會生效）。
        protected Vector3 ClampHorizontal(Vector3 p)
        {
            Quaternion rot = BoundsRot;
            Vector3 localP = Quaternion.Inverse(rot) * (p - boundsCenter);
            Vector3 half = boundsSize * 0.5f;

            // ★ 進行局部座標的限制 (OBB)
            localP.x = Mathf.Clamp(localP.x, -half.x, half.x);
            localP.z = Mathf.Clamp(localP.z, -half.z, half.z);

            return boundsCenter + rot * localP;
        }

        protected Vector3 ClampAll(Vector3 p)
        {
            Quaternion rot = BoundsRot;
            Vector3 localP = Quaternion.Inverse(rot) * (p - boundsCenter);
            Vector3 half = boundsSize * 0.5f;

            // ★ 進行 3D 全方位的局部座標限制 (OBB)
            localP.x = Mathf.Clamp(localP.x, -half.x, half.x);
            localP.y = Mathf.Clamp(localP.y, -half.y, half.y);
            localP.z = Mathf.Clamp(localP.z, -half.z, half.z);

            Vector3 worldP = boundsCenter + rot * localP;
            
            // 絕對地板防護 (世界座標)
            worldP.y = Mathf.Max(floorY, worldP.y);
            return worldP;
        }

        protected Vector3 Fwd => new Vector3(Mathf.Cos(_heading), 0f, Mathf.Sin(_heading));
        protected Vector3 Side => new Vector3(-Mathf.Sin(_heading), 0f, Mathf.Cos(_heading));

        /// 兩節肢 IK：給髖與足，算出膝。pole 決定膝往哪邊彎。
        protected static Vector3 Knee(Vector3 hip, Vector3 foot, float l1, float l2, Vector3 pole)
        {
            Vector3 d = foot - hip;
            float len = d.magnitude;
            float maxL = (l1 + l2) * 0.995f;
            if (len > maxL) { d *= maxL / len; len = maxL; foot = hip + d; }
            if (len < 1e-4f) return hip + pole.normalized * l1;

            Vector3 dir = d / len;
            float a = (l1 * l1 - l2 * l2 + len * len) / (2f * len);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - a * a));
            Vector3 po = pole - dir * Vector3.Dot(pole, dir);
            if (po.sqrMagnitude < 1e-6f) po = Vector3.up;
            po.Normalize();
            return hip + dir * a + po * h;
        }

        /// 把整段節點的朝向設成「指向前一節」。
        protected void DirsAlongChain(int from, int to)
        {
            for (int i = from; i <= to; i++)
            {
                Vector3 a = _pos[Mathf.Max(from, i - 1)];
                Vector3 b = _pos[Mathf.Min(to, i + 1)];
                Vector3 d = a - b;
                _dir[i] = d.sqrMagnitude > 1e-8f ? d.normalized : Fwd;
            }
        }

        // ── 步態 ────────────────────────────────────────────────
        protected static void GaitOffset(float phase01, out float x, out float y)
        {
            float ph = Mathf.Repeat(phase01, 1f);
            if (ph < 0.5f)                       // 擺動：騰空，由後往前
            {
                float u = ph * 2f;
                x = Mathf.SmoothStep(-1f, 1f, u);
                y = Mathf.Sin(u * Mathf.PI);
            }
            else                                 // 支撐：著地，由前往後，等速
            {
                float u = (ph - 0.5f) * 2f;
                x = Mathf.Lerp(1f, -1f, u);
                y = 0f;
            }
        }

        protected static float SlipFreeStride(float bodySpeed, float gaitHz)
            => bodySpeed / (4f * Mathf.Max(0.05f, gaitHz));

        // ── Gizmo ───────────────────────────────────────────────
        protected virtual void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            Quaternion rot = BoundsRot;

            // ★ 完美畫出會跟著轉的青色大框框
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
            Gizmos.matrix = Matrix4x4.TRS(boundsCenter, rot, boundsSize);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            // ★ 畫出底部紅色的地板投影線 (跟著傾斜)
            Gizmos.matrix = Matrix4x4.TRS(new Vector3(boundsCenter.x, floorY, boundsCenter.z), rot, boundsSize);
            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.7f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 0f, 1f));

            // 還原 Gizmos 矩陣，準備畫內部的球體
            Gizmos.matrix = Matrix4x4.identity;

            if (_pos == null) return;
            for (int i = 0; i < _pos.Length; i++)
            {
                Gizmos.color = RoleColor(_roles[i]);
                Gizmos.DrawWireSphere(_pos[i], Mathf.Max(0.005f, GetNodeRadius(i)));
            }
        }

        static Color RoleColor(ChimeraRole r)
        {
            switch (r)
            {
                case ChimeraRole.Head: return new Color(1f, 0.9f, 0.3f, 0.9f);
                case ChimeraRole.Trunk: return new Color(0.4f, 1f, 0.8f, 0.6f);
                case ChimeraRole.Limb: return new Color(1f, 0.5f, 0.9f, 0.6f);
                case ChimeraRole.Tail: return new Color(0.6f, 0.7f, 1f, 0.6f);
                default: return new Color(0.8f, 0.8f, 0.8f, 0.5f);
            }
        }
    }
}