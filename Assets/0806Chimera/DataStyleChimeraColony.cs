using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// ★ 這是 ChimeraColony 的平行版本，不是取代。原本的 ChimeraColony.cs 保持不動。
    ///
    /// 與原版的差別只有一件事：整隻生物共用一張 collage basemap，每個節點只取其中一個
    /// UV 窗格（_UvRect），再由該節點自己的 _Seed 決定這一格怎麼壞。
    /// 一張圖被整個群體撕開來各自持有一塊，沒有任何一顆球拿得到完整的圖。
    ///
    /// ★★ 同一個 GameObject 上不要同時啟用這個與原本的 ChimeraColony。
    ///    兩者都會生成／清除子物件，會互相刪對方的 zooid。
    ///    zooid 前綴刻意改成 DZooid_，所以萬一真的同時掛著，至少不會互相誤刪，
    ///    但你會看到兩隻疊在一起。
    ///
    /// ★★ 節點數變動的重建：ChimeraBodyPlan.OnValidate() 只認得原版的 ChimeraColony，
    ///    抓不到這個元件。所以這裡改成自己在 LateUpdate 比對 Spine.Count —— 
    ///    拉 PlanBird 的 Wing Pairs 或 PlanOctopus 的 Arms 滑桿仍然會正確重建。
    [ExecuteAlways]
    [AddComponentMenu("Chimera/Colony - Data Style（basemap 碎片）")]
    public class DataStyleChimeraColony : MonoBehaviour
    {
        [Header("來源")]
        [Tooltip("參與者輸入的字串。同一個字串永遠長出同一隻群體。")]
        public string label = "緣分";

        [Tooltip("留空會自動抓同物件上的 VerletSpine 或 ChimeraBodyPlan")]
        public MonoBehaviour spineProviderBehaviour;

        [Header("材質（兩個都必須指定，否則不會生成）")]
        [Tooltip("請用 Chimera/BodyGlitch 這支 shader 的材質球。")]
        public Material bodyMaterial;
        public Material organMaterial;

        [Header("Basemap 碎片")]
        [Tooltip("整張圖切成幾欄。留 0 = 依節點數自動開方。\n" +
                 "★ 改這個會重建整隻群體（格線分配會整個換一次）。")]
        public int gridCols = 0;

        [Tooltip("損壞程度。0 = 乾淨地展示自己那一格；1 = 大量區塊撕裂與色散。\n" +
                 "即時生效，不觸發重建。")]
        [Range(0f, 1f)] public float glitchAmount = 0.35f;

        [Header("整體縮放")]
        [Tooltip("整隻群體的等比縮放。以頭端為錨點，位置與體型同時縮放。1 = 原尺寸。\n" +
                 "★ 只對 VerletSpine（管水母）有效。用 ChimeraBodyPlan 的體制請改調該元件上的 " +
                 "Creature Scale，並把這個值留在 1。")]
        [Range(0.02f, 3f)] public float colonyScale = 1f;

        [Header("形態")]
        [Range(0.3f, 2f)] public float zooidScale = 0.95f;
        [Range(0f, 1f)] public float facet = 0.30f;      // 照片素材上 facet 要比原版低
        [Range(0f, 1f)] public float iridescence = 0.6f;
        [Range(0f, 1f)] public float tendrilLength = 0.5f;
        [Range(0f, 1f)] public float glass = 1f;
        public bool swimPulse = true;

        [Header("器官／附肢")]
        public OrganSettings organs = new OrganSettings();

        [Header("重建")]
        public bool rebuildNow;

        [Header("診斷")]
        [Tooltip("用 unscaledDeltaTime 推進脊索。timeScale 被設成 0 時仍然會動——" +
                 "調參用的繞道，上機前記得關掉。")]
        public bool useUnscaledTime = true;

        // ★ 與原版不同的前綴，避免兩個元件互相清掉對方的子物件
        const string ZOOID_PREFIX = "DZooid_";

        ISpineProvider _spine;
        readonly List<Transform> _roots = new List<Transform>();
        readonly List<Renderer> _bodies = new List<Renderer>();
        readonly List<Renderer> _organRenderers = new List<Renderer>();
        readonly List<ZooidParams> _params = new List<ZooidParams>();
        readonly List<ChimeraRole> _roles = new List<ChimeraRole>();

        /// 每個節點負責的 UV 窗格：(offset.x, offset.y, size.x, size.y)。
        /// ★ 用節點 index 而不是空間位置決定格子，原因有兩個：
        ///    1. ResetState() 在建立當下把所有節點都設在 _head，Build 時拿不到有意義的空間分佈。
        ///    2. index 順序本身有語意（0 頭 / 1–4 軀幹 / 之後附肢），
        ///       所以圖是「照身體結構的順序」被撕開的，而且重建後完全穩定。
        readonly List<Vector4> _uvRects = new List<Vector4>();

        MaterialPropertyBlock _mpb;
        Mesh _bodyMesh;
        bool _needRebuildNextFrame;
        string _rebuildSig;

        static readonly int ID_Seg = Shader.PropertyToID("_Seg");
        static readonly int ID_Radial = Shader.PropertyToID("_Radial");
        static readonly int ID_Warp = Shader.PropertyToID("_Warp");
        static readonly int ID_Taper = Shader.PropertyToID("_Taper");
        static readonly int ID_Seed = Shader.PropertyToID("_Seed");
        static readonly int ID_Lobes = Shader.PropertyToID("_Lobes");
        static readonly int ID_Squash = Shader.PropertyToID("_Squash");
        static readonly int ID_Pulse = Shader.PropertyToID("_Pulse");
        static readonly int ID_Facet = Shader.PropertyToID("_Facet");
        static readonly int ID_Glass = Shader.PropertyToID("_Glass");
        static readonly int ID_Irid = Shader.PropertyToID("_Irid");
        static readonly int ID_Hue = Shader.PropertyToID("_Hue");
        static readonly int ID_Dark = Shader.PropertyToID("_Dark");
        static readonly int ID_Len = Shader.PropertyToID("_Len");
        static readonly int ID_Phase = Shader.PropertyToID("_Phase");
        static readonly int ID_UvRect = Shader.PropertyToID("_UvRect");
        static readonly int ID_Glitch = Shader.PropertyToID("_Glitch");

        void OnEnable()
        {
            rebuildNow = true;

            // 同物件上還掛著並且啟用中的原版，會跟這個元件搶著生成／清除子物件
            var legacy = GetComponent<ChimeraColony>();
            if (legacy != null && legacy.enabled)
                Debug.LogWarning("[Chimera] 同一個物件上同時啟用了 ChimeraColony 與 " +
                                 "DataStyleChimeraColony，會長出兩隻疊在一起。請停用其中一個。", this);
        }

        /// 只有「會改變幾何」的欄位變動才重建。
        /// gridCols 進簽章（它改變格線分配）；glitchAmount 不進（即時生效）。
        void OnValidate()
        {
            string sig = $"{label}|{organs.eyes}{organs.mouths}{organs.headBuds}{organs.limbs}" +
                         $"|{organs.organAmount}|{organs.appendageAmount}|{gridCols}";
            if (sig != _rebuildSig) { _rebuildSig = sig; rebuildNow = true; }
        }

        void OnDisable() { ClearZooids(); }

        ISpineProvider Spine
        {
            get
            {
                if (_spine != null) return _spine;
                if (spineProviderBehaviour != null) _spine = spineProviderBehaviour as ISpineProvider;
                if (_spine == null) _spine = GetComponent<ISpineProvider>();
                return _spine;
            }
        }

        /// provider 若是體制生物就拿得到，否則 null（走舊的 Zone 路徑）
        IBodyPlan Plan => Spine as IBodyPlan;

        static ChimeraRole ZoneToRole(Zone z)
        {
            switch (z)
            {
                case Zone.Head: return ChimeraRole.Head;
                case Zone.Nectosome: return ChimeraRole.Trunk;
                case Zone.Siphosome: return ChimeraRole.Drift;
                default: return ChimeraRole.Tail;
            }
        }

        static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// 刪掉所有 zooid —— 不依賴快取清單，直接掃實際的子物件。
        /// 只掃自己前綴的，不會動到原版 ChimeraColony 的 Zooid_。
        public void ClearZooids()
        {
            var kill = new List<GameObject>();
            foreach (Transform c in transform)
                if (c != null && c.name.StartsWith(ZOOID_PREFIX)) kill.Add(c.gameObject);

            foreach (var g in kill)
            {
                // 器官 mesh 是程序化生成的，要一起銷毀，否則會洩漏
                var mfs = g.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in mfs)
                    if (mf != null && mf.sharedMesh != null && mf.sharedMesh != _bodyMesh)
                        SafeDestroy(mf.sharedMesh);
                SafeDestroy(g);
            }
            _roots.Clear(); _bodies.Clear(); _organRenderers.Clear();
            _params.Clear(); _roles.Clear(); _uvRects.Clear();
        }

        public void Build()
        {
            rebuildNow = false;
            _spine = null;

            ClearZooids();

            if (Spine == null)
            {
                Debug.LogWarning("[Chimera] 找不到 ISpineProvider（把 VerletSpine 或某個 " +
                                 "ChimeraBodyPlan 拖進 Spine Provider Behaviour）", this);
                return;
            }
            if (bodyMaterial == null || organMaterial == null)
            {
                Debug.LogWarning("[Chimera] Body Material 或 Organ Material 未指定，" +
                                 "未指定的 renderer 會顯示洋紅色。", this);
                return;
            }

            if (_bodyMesh == null) _bodyMesh = IcoSphere.Create(3);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            var plan = Plan;
            int n = Spine.Count;
            // 體制元件的 OnEnable 可能還沒跑到（元件初始化順序不保證），下一幀再試
            if (n <= 0) { rebuildNow = true; return; }

            // 貼圖格線。gridCols 留 0 就開方——章魚 27 節點 → 6 欄 × 5 列 = 30 格，剛好夠且沒有空格。
            int cols = gridCols > 0 ? gridCols : Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));

            // 縮放錨點：頭端。（僅 VerletSpine 路徑用）
            Vector3 head = Spine.GetPoint(0);

            for (int i = 0; i < n; i++)
            {
                var zp = ChimeraHash.Make(label, i);
                var role = plan != null ? plan.GetRole(i) : ZoneToRole(ChimeraHash.ZoneOf(i, n));
                bool isHead = role == ChimeraRole.Head;

                var rootGO = new GameObject($"{ZOOID_PREFIX}{i:00}_{role}");
                // 程序化生成的物件不進 undo／不存進場景，避免 dangling 警告
                rootGO.hideFlags = HideFlags.DontSave;
                var root = rootGO.transform;
                root.SetParent(transform, false);

                var body = new GameObject("Body") { hideFlags = HideFlags.DontSave };
                body.transform.SetParent(root, false);
                body.AddComponent<MeshFilter>().sharedMesh = _bodyMesh;
                var br = body.AddComponent<MeshRenderer>();
                br.sharedMaterial = bodyMaterial;
                br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var organMesh = ChimeraMeshBuilder.Build(zp, role, organs);
                Renderer or = null;
                if (organMesh != null)
                {
                    var og = new GameObject("Organs") { hideFlags = HideFlags.DontSave };
                    og.transform.SetParent(root, false);
                    og.AddComponent<MeshFilter>().sharedMesh = organMesh;
                    or = og.AddComponent<MeshRenderer>();
                    or.sharedMaterial = organMaterial;
                    or.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                if (isHead) { zp.seg = 0.55f; zp.radial = 0.85f; zp.warp = 0.06f; zp.lobes = 2f; zp.squash = 1.7f; }

                // 建立當下就先擺到骨架上。沒有這行的話，新建的 zooid 在被擺位之前會停在
                // 父物件原點 (0,0,0)，看起來就是全部疊成一團。
                root.position = plan != null
                    ? Spine.GetPoint(i)
                    : head + (Spine.GetPoint(i) - head) * colonyScale;
                root.rotation = Quaternion.FromToRotation(Vector3.up, Spine.GetForward(i));

                // 這個節點負責整張圖的哪一格
                _uvRects.Add(new Vector4((i % cols) / (float)cols,
                                         (i / cols) / (float)rows,
                                         1f / cols, 1f / rows));

                _roots.Add(root); _bodies.Add(br); _organRenderers.Add(or);
                _params.Add(zp); _roles.Add(role);
            }
        }

        void LateUpdate()
        {
            // ★ 重建之後不要 return —— 同一幀就要繼續擺位。
            if (rebuildNow) Build();
            if (Spine == null || _roots.Count == 0) return;

            // ★ 節點數變了就重建。原版靠 ChimeraBodyPlan.OnValidate() 主動通知，
            //   但那段只認得 ChimeraColony，抓不到這個元件，所以改成自己比對。
            if (Spine.Count != _roots.Count) { rebuildNow = true; return; }

            // Time.deltaTime 在 timeScale = 0 時是 0，Tick 開頭的
            // if (dt <= 0f) return; 會讓骨架一步都不算。
            float dt = Application.isPlaying
                ? (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime)
                : 1f / 60f;

            // ★ 上限保護。Play 的第一幀（以及編譯、載入造成的卡頓幀）dt 可能是
            // 好幾百毫秒，骨架會被一次推進太多而甩開。
            dt = Mathf.Min(dt, 1f / 30f);
            Spine.Tick(dt);

            var plan = Plan;
            int n = Mathf.Min(_roots.Count, Spine.Count);
            float t = Application.isPlaying
                ? (useUnscaledTime ? Time.unscaledTime : Time.time)
                : 0f;
            Vector3 head = Spine.GetPoint(0);

            for (int i = 0; i < n; i++)
            {
                var root = _roots[i];
                if (root == null) { _needRebuildNextFrame = true; continue; }

                root.position = plan != null
                    ? Spine.GetPoint(i)
                    : head + (Spine.GetPoint(i) - head) * colonyScale;

                Vector3 fwd = Spine.GetForward(i);
                root.rotation = Quaternion.Slerp(root.rotation,
                    Quaternion.FromToRotation(Vector3.up, fwd), 1f - Mathf.Exp(-12f * dt));

                var zp = _params[i];
                bool isHead = _roles[i] == ChimeraRole.Head;
                float pulse = swimPulse ? 1f + 0.05f * Mathf.Sin(t * 2.4f + zp.seed) : 1f;
                float squashTerm = 0.85f + 0.3f * (zp.squash - 0.65f);

                float s;
                if (plan != null)
                {
                    // 體制生物：節點大小由骨架給（已含 creatureScale），不再依鏈上的位置遞減
                    s = zooidScale * plan.GetNodeRadius(i) * squashTerm * pulse;
                }
                else
                {
                    float zoneT = n > 1 ? (float)i / (n - 1) : 0f;
                    s = (isHead ? 2.0f : 1.0f) * zooidScale
                        * (0.55f + 0.7f * (1f - zoneT))
                        * squashTerm * 0.40f * pulse
                        * colonyScale;
                }
                root.localScale = Vector3.one * s;

                if (_bodies[i] != null)
                {
                    _mpb.Clear();
                    _mpb.SetFloat(ID_Seg, zp.seg);
                    _mpb.SetFloat(ID_Radial, zp.radial);
                    _mpb.SetFloat(ID_Warp, zp.warp);
                    _mpb.SetFloat(ID_Taper, zp.taper);
                    _mpb.SetFloat(ID_Seed, zp.seed);
                    _mpb.SetFloat(ID_Lobes, zp.lobes);
                    _mpb.SetFloat(ID_Squash, zp.squash);
                    _mpb.SetFloat(ID_Pulse, swimPulse ? 1f : 0f);
                    _mpb.SetFloat(ID_Facet, facet);
                    _mpb.SetFloat(ID_Glass, glass);
                    _mpb.SetFloat(ID_Irid, iridescence);
                    _mpb.SetFloat(ID_Hue, zp.hue);
                    // ★ 這一節點負責的貼圖窗格與損壞程度
                    _mpb.SetVector(ID_UvRect, _uvRects[i]);
                    _mpb.SetFloat(ID_Glitch, glitchAmount);
                    _mpb.SetFloat(ID_Dark, 0f);
                    _bodies[i].SetPropertyBlock(_mpb);
                }

                var or = _organRenderers[i];
                if (or != null)
                {
                    _mpb.Clear();
                    _mpb.SetFloat(ID_Phase, zp.seed);
                    _mpb.SetFloat(ID_Len, 0.4f + tendrilLength * 2.2f);
                    _mpb.SetFloat(ID_Facet, Mathf.Min(1f, facet * 0.7f));
                    _mpb.SetFloat(ID_Glass, glass);
                    _mpb.SetFloat(ID_Irid, iridescence);
                    _mpb.SetFloat(ID_Hue, zp.hue + 1.2f);
                    _mpb.SetFloat(ID_Dark, 0.25f);
                    or.SetPropertyBlock(_mpb);
                }
            }

            if (_needRebuildNextFrame) { _needRebuildNextFrame = false; rebuildNow = true; }
        }
    }
}