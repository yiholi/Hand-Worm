using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// 器官／附肢設定。掛在 ChimeraColony 上，全群體共用。
    [System.Serializable]
    public class OrganSettings
    {
        [Header("器官 organs")]
        public bool eyes = true;
        public bool mouths = true;
        public bool headBuds = true;
        public bool limbs = true;
        [Range(0f, 1f)] public float organAmount = 0.6f;

        [Header("附肢 appendages")]
        [Range(0f, 1f)] public float appendageAmount = 0.55f;
    }

    /// 頂點型別，寫進 UV1.y，由 shader 分流著色。
    /// 0 = 一般組織, 1 = 眼球晶體, 2 = 瞳孔/口腔（吸光）, 3 = 齒/爪（骨白）
    public static class VType
    {
        public const float Tissue = 0f, Lens = 1f, Void = 2f, Bone = 3f;
    }

    public static class ChimeraMeshBuilder
    {
        // ---- 緩衝 ----
        class Buf
        {
            public List<Vector3> pos = new List<Vector3>();
            public List<Vector2> uv1 = new List<Vector2>(); // x = sway, y = type
            public List<int> tris = new List<int>();

            public void Add(Vector3 p, float sway, float type)
            {
                tris.Add(pos.Count);
                pos.Add(p);
                uv1.Add(new Vector2(sway, type));
            }
        }

        static Mesh _ico0, _ico1, _ico2;
        // 注意：這裡不能用 ?? 運算子。
        // UnityEngine.Object 覆寫了 ==，被銷毀的物件 == null 會回傳 true，
        // 但 ?? 是 C# 語言層的 null 檢查，不會走覆寫的 ==，
        // 於是它會把「已銷毀但參考還在」的 mesh 當成有效值傳出去 → MissingReferenceException。
        static Mesh Ico(int s)
        {
            if (s == 0) { if (_ico0 == null) _ico0 = IcoSphere.Create(0); return _ico0; }
            if (s == 1) { if (_ico1 == null) _ico1 = IcoSphere.Create(1); return _ico1; }
            if (_ico2 == null) _ico2 = IcoSphere.Create(2);
            return _ico2;
        }

        static void PushMesh(Buf b, Mesh src, Matrix4x4 m, float sway, float type)
        {
            var v = src.vertices; var t = src.triangles;
            for (int i = 0; i < t.Length; i++) b.Add(m.MultiplyPoint3x4(v[t[i]]), sway, type);
        }

        /// 沿一條折線生成漸縮管
        static void PushTube(Buf b, List<Vector3> pts, List<float> radii, int sides, float swayBase, float type)
        {
            int n = pts.Count;
            var rings = new List<Vector3[]>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 c = pts[i];
                Vector3 tan = (i < n - 1 ? pts[i + 1] - c : c - pts[i - 1]).normalized;
                Vector3 up = Mathf.Abs(tan.y) > 0.9f ? Vector3.right : Vector3.up;
                Vector3 nx = Vector3.Cross(up, tan).normalized;
                Vector3 nz = Vector3.Cross(tan, nx).normalized;
                var ring = new Vector3[sides];
                for (int s = 0; s < sides; s++)
                {
                    float a = (float)s / sides * Mathf.PI * 2f;
                    ring[s] = c + nx * (Mathf.Cos(a) * radii[i]) + nz * (Mathf.Sin(a) * radii[i]);
                }
                rings.Add(ring);
            }
            for (int i = 0; i < n - 1; i++)
            {
                float w0 = swayBase * i / (n - 1), w1 = swayBase * (i + 1) / (n - 1);
                for (int s = 0; s < sides; s++)
                {
                    Vector3 A = rings[i][s], B = rings[i][(s + 1) % sides];
                    Vector3 C = rings[i + 1][s], D = rings[i + 1][(s + 1) % sides];
                    b.Add(A, w0, type); b.Add(B, w0, type); b.Add(C, w1, type);
                    b.Add(B, w0, type); b.Add(D, w1, type); b.Add(C, w1, type);
                }
            }
            Vector3 tip = pts[n - 1]; var last = rings[n - 1];
            for (int s = 0; s < sides; s++)
            {
                b.Add(last[s], swayBase, type);
                b.Add(last[(s + 1) % sides], swayBase, type);
                b.Add(tip, swayBase, type);
            }
        }

        static Matrix4x4 M(Vector3 dir, Vector3 pos, Vector3 scale, float roll)
        {
            Vector3 d = dir.normalized;
            Quaternion q = Quaternion.FromToRotation(Vector3.up, d);
            q = Quaternion.AngleAxis(roll * Mathf.Rad2Deg, d) * q;
            return Matrix4x4.TRS(pos, q, scale);
        }
        static Matrix4x4 M(Vector3 dir, Vector3 pos, float s, float roll) => M(dir, pos, Vector3.one * s, roll);

        struct Curve { public List<Vector3> pts; public List<float> radii; }
        static Curve MakeCurve(Vector3 origin, Vector3 dir, float len, float curl, float seed, int segs, float r0)
        {
            var pts = new List<Vector3>(); var radii = new List<float>();
            Vector3 cur = origin, dd = dir.normalized;
            for (int j = 0; j < segs; j++)
            {
                pts.Add(cur);
                radii.Add(r0 * Mathf.Pow(1f - (float)j / segs, 0.9f) + 0.006f);
                dd = (dd + new Vector3(Mathf.Sin(j * 1.3f + seed),
                                       Mathf.Cos(j * 1.7f + seed * 1.4f),
                                       Mathf.Sin(j * 2.1f + seed * 0.7f)) * curl).normalized;
                cur += dd * (len / segs);
            }
            pts.Add(cur); radii.Add(0.006f);
            return new Curve { pts = pts, radii = radii };
        }

        // ---------------- 器官 ----------------
        static void Eye(Buf b, Vector3 d, Vector3 at, float size, float roll)
        {
            // 眼球晶體：略突出
            PushMesh(b, Ico(1), M(d, at + d * (size * 0.35f), size, roll), 0f, VType.Lens);
            // 瞳：更外、深色、略扁。刻意不給高光白點。
            PushMesh(b, Ico(0), M(d, at + d * (size * 0.95f),
                new Vector3(size * 0.55f, size * 0.42f, size * 0.55f), roll), 0f, VType.Void);
        }

        static void Mouth(Buf b, Vector3 d, Vector3 at, float size, float roll, int teeth)
        {
            PushMesh(b, Ico(1), M(d, at - d * (size * 0.1f),
                new Vector3(size, size * 0.30f, size * 0.62f), roll), 0f, VType.Void);
            Quaternion q = Quaternion.FromToRotation(Vector3.up, d.normalized);
            for (int j = 0; j < teeth; j++)
            {
                float a = (float)j / teeth * Mathf.PI * 2f;
                Vector3 off = q * new Vector3(Mathf.Cos(a) * size * 0.78f, 0f, Mathf.Sin(a) * size * 0.46f);
                Vector3 dirT = (d * 0.6f - off.normalized * 0.5f).normalized;
                PushMesh(b, Ico(0), M(dirT, at + off,
                    new Vector3(size * 0.10f, size * 0.34f, size * 0.10f), 0f), 0f, VType.Bone);
            }
        }

        static void HeadBud(Buf b, Vector3 d, Vector3 at, float size, float seed)
        {
            var c = MakeCurve(at, d, size * 1.5f, 0.12f, seed, 3, size * 0.32f);
            PushTube(b, c.pts, c.radii, 5, 0.15f, VType.Tissue);
            Vector3 hc = c.pts[c.pts.Count - 1];
            PushMesh(b, Ico(2), M(d, hc, new Vector3(size * 0.85f, size * 1.05f, size * 0.72f), seed), 0.2f, VType.Tissue);

            int ne = 1 + Mathf.FloorToInt(ChimeraHash.Rnd(seed, 1) * 3f);
            for (int j = 0; j < ne; j++)
            {
                float a = ChimeraHash.Rnd(seed, j + 2) * Mathf.PI * 2f;
                float y = ChimeraHash.Rnd(seed, j + 5) * 1.2f - 0.3f;
                float cc = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                Vector3 ed = new Vector3(Mathf.Cos(a) * cc, y, Mathf.Sin(a) * cc).normalized;
                Eye(b, ed, hc + ed * (size * 0.72f), size * 0.26f, 0f);
            }
            if (ChimeraHash.Rnd(seed, 9) > 0.45f)
            {
                Vector3 md = Vector3.Lerp(d, Vector3.down, 0.5f).normalized;
                Mouth(b, md, hc + md * (size * 0.70f), size * 0.42f, 0f, 5);
            }
        }

        static void Limb(Buf b, Vector3 d, Vector3 at, float len, float seed)
        {
            var c = MakeCurve(at, Vector3.Lerp(d, Vector3.down, 0.35f).normalized, len, 0.30f, seed, 5, 0.085f);
            PushTube(b, c.pts, c.radii, 5, 0.9f, VType.Tissue);
            Vector3 tip = c.pts[c.pts.Count - 1];
            for (int j = 0; j < 3; j++)
            {
                float a = (float)j / 3f * Mathf.PI * 2f + seed;
                Vector3 dd = new Vector3(Mathf.Cos(a) * 0.7f, -0.7f, Mathf.Sin(a) * 0.7f).normalized;
                PushMesh(b, Ico(0), M(dd, tip + dd * (len * 0.09f),
                    new Vector3(0.022f, len * 0.16f, 0.022f), 0f), 1f, VType.Bone);
            }
        }

        // ================= 附肢層（依角色，供兩個入口共用） =================
        // ★ 這幾塊原本寫死在 Build() 的 if/else 裡。抽出來之後，
        //   Zone 版（管水母）與 Role 版（其他體制）長出來的東西完全一致，
        //   所以換體制不會換掉美術方向。

        /// 羽片扇列 + 膜葉。管水母的泳鐘區、其他體制的軀幹。
        static void AppFins(Buf b, float S, float amount, System.Func<int, float> rnd,
                            System.Func<int, Vector3> dirAt, System.Func<Vector3, Vector3> basePt)
        {
            int n = Mathf.RoundToInt((5f + rnd(1) * 4f) * amount);
            float y = 0.05f + rnd(2) * 0.4f;
            for (int i = 0; i < n; i++)
            {
                float th = (float)i / Mathf.Max(1, n) * Mathf.PI * 2f + S;
                float c = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                Vector3 d = new Vector3(Mathf.Cos(th) * c, y, Mathf.Sin(th) * c);
                float len = 0.55f + 0.35f * Mathf.Abs(Mathf.Sin(i * 1.7f + S));
                PushMesh(b, Ico(1), M(Vector3.Lerp(d, Vector3.up, 0.25f).normalized, basePt(d),
                    new Vector3(0.20f, len, 0.035f), th * 1.6f), 0.55f, VType.Tissue);
            }
            for (int i = 0; i < Mathf.RoundToInt(3f * amount); i++)
            {
                Vector3 d = dirAt(i + 11);
                PushMesh(b, Ico(1), M(d, basePt(d), new Vector3(0.34f, 0.40f, 0.05f), rnd(i) * 6.28f),
                    0.4f, VType.Tissue);
            }
        }

        /// 觸手 + 囊泡 + 突觸。管水母的營養區、章魚的腕、海兔的鰓突。
        static void AppDrift(Buf b, float S, float amount, System.Func<int, float> rnd,
                             System.Func<int, Vector3> dirAt, System.Func<Vector3, Vector3> basePt)
        {
            for (int i = 0; i < Mathf.RoundToInt((2f + rnd(3) * 3f) * amount); i++)
            {
                Vector3 d = dirAt(i + 3);
                var c = MakeCurve(basePt(d), d, 0.9f + 0.5f * rnd(i + 7), 0.5f, S + i, 5, 0.055f);
                PushTube(b, c.pts, c.radii, 4, 1f, VType.Tissue);
                PushMesh(b, Ico(0), M(Vector3.up, c.pts[c.pts.Count - 1], 0.05f, 0f), 1f, VType.Tissue);
            }
            for (int i = 0; i < Mathf.RoundToInt((2f + rnd(4) * 3f) * amount); i++)
            {
                Vector3 d = dirAt(i + 21), c0 = basePt(d);
                for (int j = 0; j < 3; j++)
                {
                    Vector3 o = new Vector3(rnd(i * 3 + j) - 0.5f, rnd(i * 3 + j + 9) - 0.5f,
                                            rnd(i * 3 + j + 17) - 0.5f) * 0.3f;
                    PushMesh(b, Ico(1), M(Vector3.up, c0 + o, 0.085f + 0.05f * rnd(j), 0f),
                        0.25f, VType.Tissue);
                }
            }
            for (int i = 0; i < Mathf.RoundToInt(2f * amount); i++)   // 突觸
            {
                Vector3 d = dirAt(i + 41), c0 = basePt(d) + d * 0.18f;
                PushMesh(b, Ico(0), M(Vector3.up, c0, 0.075f, 0f), 0.5f, VType.Tissue);
                for (int j = 0; j < 4; j++)
                {
                    Vector3 dd = new Vector3(rnd(i * 4 + j) - 0.5f, rnd(i * 4 + j + 5) - 0.5f,
                                             rnd(i * 4 + j + 13) - 0.5f).normalized;
                    var cc = MakeCurve(c0, dd, 0.30f, 0.15f, S + i * 4 + j, 3, 0.025f);
                    PushTube(b, cc.pts, cc.radii, 3, 0.8f, VType.Tissue);
                }
            }
        }

        /// 細長觸手 + 棘刺。管水母的生殖區、獸與鳥的尾。
        static void AppTail(Buf b, float S, float amount, System.Func<int, float> rnd,
                            System.Func<int, Vector3> dirAt, System.Func<Vector3, Vector3> basePt)
        {
            for (int i = 0; i < Mathf.RoundToInt((3f + rnd(5) * 3f) * amount); i++)
            {
                Vector3 d = Vector3.Lerp(dirAt(i + 61), Vector3.down, 0.45f).normalized;
                var c = MakeCurve(basePt(d), d, 1.3f + 0.6f * rnd(i + 2), 0.35f, S + i, 6, 0.04f);
                PushTube(b, c.pts, c.radii, 3, 1f, VType.Tissue);
                PushMesh(b, Ico(0), M(Vector3.up, c.pts[c.pts.Count - 1], 0.042f, 0f), 1f, VType.Tissue);
            }
            for (int i = 0; i < Mathf.RoundToInt(4f * amount); i++)
            {
                Vector3 d = dirAt(i + 81);
                PushMesh(b, Ico(0), M(d, basePt(d), new Vector3(0.055f, 0.42f, 0.055f), 0f),
                    0.3f, VType.Tissue);
            }
        }

        /// 剛毛／棘：短而硬，讀成肢節上的角質。
        /// ★ 肢節刻意不給觸手 —— 腳上長觸手會讓步態完全讀不出來。
        static void AppBristles(Buf b, float amount, System.Func<int, float> rnd,
                                System.Func<int, Vector3> dirAt, System.Func<Vector3, Vector3> basePt)
        {
            for (int i = 0; i < Mathf.RoundToInt(4f * amount); i++)
            {
                Vector3 d = dirAt(i + 81);
                PushMesh(b, Ico(0), M(d, basePt(d), new Vector3(0.05f, 0.36f, 0.05f), 0f), 0.3f, VType.Tissue);
            }
        }

        // ================= 主入口 A：Zone（管水母／VerletSpine 用，行為不變） =================
        public static Mesh Build(ZooidParams zp, Zone zone, OrganSettings cfg)
        {
            ChimeraRole role;
            switch (zone)
            {
                case Zone.Head: role = ChimeraRole.Head; break;
                case Zone.Nectosome: role = ChimeraRole.Trunk; break;
                case Zone.Siphosome: role = ChimeraRole.Drift; break;
                default: role = ChimeraRole.Tail; break;
            }
            return Build(zp, role, cfg);
        }

        // ================= 主入口 B：Role（體制生物用） =================
        public static Mesh Build(ZooidParams zp, ChimeraRole role, OrganSettings cfg)
        {
            var b = new Buf();
            float S = zp.seed;
            bool isHead = role == ChimeraRole.Head;
            bool isLimb = role == ChimeraRole.Limb;
            bool isShell = role == ChimeraRole.Shell;
            float organAmt = cfg.organAmount, amount = cfg.appendageAmount;

            System.Func<int, float> rnd = n => ChimeraHash.Rnd(S, n);
            System.Func<int, Vector3> dirAt = n =>
            {
                float a = rnd(n) * Mathf.PI * 2f, y = rnd(n + 50) * 1.7f - 0.85f;
                float c = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                return new Vector3(Mathf.Cos(a) * c, y, Mathf.Sin(a) * c);
            };
            System.Func<Vector3, Vector3> basePt = d => d * 0.78f;

            // ===== 器官層 =====
            if (cfg.eyes)
            {
                int ne = Mathf.RoundToInt((1f + rnd(101) * 4f) * organAmt * (isHead ? 1.8f : 1f));
                if (isLimb) ne = Mathf.RoundToInt(ne * 0.4f);     // 腳上的眼睛要少，否則整隻讀成一團肉
                if (isShell) ne = Mathf.RoundToInt(ne * 0.3f);
                if (ne == 2) ne = 3;                              // 絕不給剛好兩顆：兩顆對稱=人臉基模=角色
                bool cluster = rnd(107) > 0.62f;
                for (int i = 0; i < ne; i++)
                {
                    Vector3 d;
                    if (cluster)
                    {
                        float a = rnd(109) * 6.28f + i * 0.30f;
                        float y = rnd(113) * 1.0f - 0.2f + i * 0.06f;
                        float c = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Min(0.99f, y * y)));
                        d = new Vector3(Mathf.Cos(a) * c, y, Mathf.Sin(a) * c).normalized;
                    }
                    else d = dirAt(i + 101);
                    float sz = (cluster ? 0.085f : 0.13f + rnd(i + 117) * 0.10f) * (isHead ? 1.5f : 1f);
                    Eye(b, d, basePt(d), sz, rnd(i + 121) * 6.28f);
                }
            }
            if (cfg.mouths && !isLimb && !isShell && (isHead || rnd(131) > 0.45f))
            {
                int nm = isHead ? 1 : Mathf.Max(1, Mathf.RoundToInt(rnd(133) * 2f * organAmt));
                for (int i = 0; i < nm; i++)
                {
                    Vector3 d = isHead ? new Vector3(0.1f, -1f, 0.1f).normalized : dirAt(i + 141);
                    Mouth(b, d, basePt(d), 0.20f + rnd(i + 143) * 0.14f,
                          rnd(i + 147) * 6.28f, 4 + Mathf.FloorToInt(rnd(i + 151) * 5f));
                }
            }
            if (cfg.headBuds && role == ChimeraRole.Trunk && rnd(161) > 0.55f)
            {
                int nh = Mathf.Max(1, Mathf.RoundToInt(rnd(163) * 2f * organAmt));
                for (int i = 0; i < nh; i++)
                {
                    Vector3 d = dirAt(i + 171);
                    HeadBud(b, d, basePt(d), 0.22f + rnd(i + 173) * 0.10f, S + i);
                }
            }
            if (cfg.limbs && !isHead && !isLimb && !isShell && rnd(191) > 0.5f)
            {
                int nl = Mathf.Max(1, Mathf.RoundToInt((1f + rnd(193) * 3f) * organAmt));
                for (int i = 0; i < nl; i++)
                {
                    Vector3 d = dirAt(i + 201);
                    Limb(b, d, basePt(d), 0.55f + rnd(i + 203) * 0.45f, S + i * 3f);
                }
            }

            // ===== 附肢層：依角色，不再依位置 =====
            if (!isHead && amount > 0.01f)
            {
                switch (role)
                {
                    case ChimeraRole.Trunk: AppFins(b, S, amount, rnd, dirAt, basePt); break;
                    case ChimeraRole.Drift: AppDrift(b, S, amount, rnd, dirAt, basePt); break;
                    case ChimeraRole.Tail: AppTail(b, S, amount, rnd, dirAt, basePt); break;
                    case ChimeraRole.Limb: AppBristles(b, amount, rnd, dirAt, basePt); break;
                    case ChimeraRole.Shell: break;    // 殼：無附肢，否則螺旋線讀不出來
                }
            }

            if (b.pos.Count == 0) return null;

            var mesh = new Mesh { name = "ChimeraOrgans" };
            if (b.pos.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(b.pos);
            mesh.SetUVs(1, b.uv1);          // TEXCOORD1: x = sway, y = type
            mesh.SetTriangles(b.tris, 0);
            mesh.RecalculateBounds();
            // 法線不需要：shader 用 ddx/ddy 求面法線，flat shading 自動正確
            return mesh;
        }
    }
}