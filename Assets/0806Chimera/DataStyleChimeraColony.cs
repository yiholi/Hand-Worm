using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// ★ 這是 ChimeraColony 的平行版本，不是取代。原本的 ChimeraColony.cs 保持不動。
    ///
    /// 整隻生物共用一張 collage basemap，每個節點只取其中一個 UV 窗格（_UvRect），
    /// 再由該節點自己的 _Seed 決定這一格怎麼壞。
    ///
    /// ★★ MaterialPropertyBlock 只推「每個節點真的不一樣」的值。
    ///    _Facet / _Irid / _Glass / _Glitch / _Amp 這些整隻生物共用同一個值，
    ///    用 MPB 推它們沒有任何好處，唯一效果是擋掉材質球上的滑桿。
    ///    現在它們歸材質球所有，你在材質球上拉隨時生效，不需要任何開關。
    ///
    /// ★★ 同一個 GameObject 上不要同時啟用這個與原本的 ChimeraColony。
    ///    zooid 前綴刻意改成 DZooid_，所以萬一同時掛著至少不會互相誤刪。
    [ExecuteAlways]
    [AddComponentMenu("Chimera/Colony - Data Style（basemap 碎片）")]
    public class DataStyleChimeraColony : MonoBehaviour
    {
        /// 窗格怎麼決定。
        /// Grid    = 整張圖均分成 n 格。每格都很小，素材會被切碎，看不出是什麼動物。
        /// Subject = 一格對一隻動物，格子可以互相重疊。要讓觀眾認出素材就用這個。
        public enum WindowMode { Grid, Subject }

        [Header("來源")]
        [Tooltip("參與者輸入的字串。同一個字串永遠長出同一隻群體。")]
        public string label = "緣分";

        [Tooltip("留空會自動抓同物件上的 VerletSpine 或 ChimeraBodyPlan")]
        public MonoBehaviour spineProviderBehaviour;

        [Header("材質（兩個都必須指定，否則不會生成）")]
        [Tooltip("請用 Chimera/BodyGlitch 這支 shader 的材質球。\n\n" +
                 "★ 外觀參數現在幾乎全部在材質球上調：Glitch Amount / Glitch Rate / Burst / " +
                 "Block Count / Tear / Chroma Split / Channel Blowout / Posterise / " +
                 "Projection Scale / Displacement Amp / Facet / Rim Iridescence / Hue Shift Mix。\n" +
                 "這個元件只負責「每顆球哪裡不一樣」。")]
        public Material bodyMaterial;
        public Material organMaterial;

        [Header("編輯期")]
        [Tooltip("持續更新（呼吸會動、改參數立刻重畫）。上機前可以關掉，不影響 build。")]
        public bool livePreview = true;

        [Tooltip("★ 打開時連 _Seg / _Radial / _Warp / _Taper / _Lobes / _Squash / _Hue 也不推，\n" +
                 "27 顆球會共用材質球上的形態值，方便你單獨觀察某一組參數的效果。\n" +
                 "_Seed 與 _UvRect 永遠會推，否則每顆球會長得一模一樣。")]
        public bool freezePerNodeShape = false;

        [Header("Basemap 碎片")]
        [Tooltip("★ 決定每顆球看到整張圖的哪一塊。\n\n" +
                 "Grid：均分。27 個節點就是 1/30 張圖，大約 340px —— 一條魚有 800px 寬，" +
                 "所以每顆球只會拿到一片魚鰭，認不出是什麼動物。\n\n" +
                 "Subject：一格對一隻動物，允許重疊。要讓觀眾認出素材就用這個。")]
        public WindowMode windowMode = WindowMode.Subject;

        [Tooltip("Grid 模式：整張圖切成幾欄。留 0 = 依節點數自動開方。")]
        public int gridCols = 0;

        [Tooltip("Grid 模式：把每一格以中心為錨點放大這個倍率。\n" +
                 "1 = 剛好不重疊。2 = 每格涵蓋四倍面積，鄰格內容互相重疊。")]
        [Range(1f, 6f)] public float gridWindowScale = 1f;

        [Tooltip("Subject 模式：每一隻動物在圖上的位置。\n\n" +
                 "★ 座標是「正規化的圖片座標」，原點在左上角，跟 Photoshop 一致：\n" +
                 "   X / Y = 左上角位置（0～1），W / H = 寬高（0～1）。\n" +
                 "   程式會自己換算成 Unity 的 UV（原點左下）。\n\n" +
                 "預設值是照 FADataChimera.png 目測量出來的，請一邊看 Scene 視窗一邊微調。\n" +
                 "節點比動物多時會重複使用，同一隻動物出現在好幾顆球上、各自被裁得不一樣。")]
        public List<Rect> subjectWindows = new List<Rect>
        {
            new Rect(0.00f, 0.00f, 0.23f, 0.37f),   // shark
            new Rect(0.17f, 0.00f, 0.36f, 0.26f),   // blue tang
            new Rect(0.53f, 0.00f, 0.47f, 0.36f),   // butterflyfish
            new Rect(0.46f, 0.23f, 0.30f, 0.24f),   // angelfish
            new Rect(0.19f, 0.18f, 0.26f, 0.21f),   // pigeon
            new Rect(0.12f, 0.31f, 0.17f, 0.16f),   // ant
            new Rect(0.26f, 0.40f, 0.30f, 0.26f),   // clownfish
            new Rect(0.52f, 0.47f, 0.48f, 0.29f),   // dolphin
            new Rect(0.70f, 0.27f, 0.30f, 0.26f),   // daisies
            new Rect(0.00f, 0.35f, 0.42f, 0.45f),   // monarch wing
            new Rect(0.32f, 0.62f, 0.29f, 0.34f),   // beetle
            new Rect(0.60f, 0.70f, 0.36f, 0.30f),   // seagull
        };

        [Header("整體縮放")]
        [Tooltip("整隻群體的等比縮放。以頭端為錨點。1 = 原尺寸。\n" +
                 "★ 只對 VerletSpine（管水母）有效。用 ChimeraBodyPlan 的體制請改調該元件上的 " +
                 "Creature Scale，並把這個值留在 1。")]
        [Range(0.02f, 3f)] public float colonyScale = 1f;

        [Header("形態")]
        [Range(0.3f, 2f)] public float zooidScale = 0.95f;
        public bool swimPulse = true;

        [Tooltip("器官附肢的長度（推給 organ 材質的 _Len）。")]
        [Range(0f, 1f)] public float tendrilLength = 0.5f;

        [Header("器官／附肢")]
        public OrganSettings organs = new OrganSettings();

        [Header("重建")]
        [Tooltip("勾一下就重建。勾完會自動彈回，那是正常的。")]
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

        MaterialPropertyBlock _mpb;
        Mesh _bodyMesh;
        bool _needRebuildNextFrame;
        string _rebuildSig;
        float _editorPrevTime;

        // ★ 只保留真正逐節點的那幾個。_Facet / _Irid / _Glass / _Glitch / _Amp
        //   刻意不在這裡，它們歸材質球所有。
        static readonly int ID_Seg = Shader.PropertyToID("_Seg");
        static readonly int ID_Radial = Shader.PropertyToID("_Radial");
        static readonly int ID_Warp = Shader.PropertyToID("_Warp");
        static readonly int ID_Taper = Shader.PropertyToID("_Taper");
        static readonly int ID_Seed = Shader.PropertyToID("_Seed");
        static readonly int ID_Lobes = Shader.PropertyToID("_Lobes");
        static readonly int ID_Squash = Shader.PropertyToID("_Squash");
        static readonly int ID_Pulse = Shader.PropertyToID("_Pulse");
        static readonly int ID_Hue = Shader.PropertyToID("_Hue");
        static readonly int ID_UvRect = Shader.PropertyToID("_UvRect");
        // organ 材質（舊 shader）用的
        static readonly int ID_Len = Shader.PropertyToID("_Len");
        static readonly int ID_Phase = Shader.PropertyToID("_Phase");

        void OnEnable()
        {
            rebuildNow = true;

            var legacy = GetComponent<ChimeraColony>();
            if (legacy != null && legacy.enabled)
                Debug.LogWarning("[Chimera] 同一個物件上同時啟用了 ChimeraColony 與 " +
                                 "DataStyleChimeraColony，會長出兩隻疊在一起。請停用其中一個。", this);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += EditorTick;
            _editorPrevTime = Time.realtimeSinceStartup;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
#endif
            ClearZooids();
        }

#if UNITY_EDITOR
        /// ★ 編輯期的心跳。
        /// 沒有這段的話，[ExecuteAlways] 的 LateUpdate 只在場景被弄髒時跑一次，
        /// 結果是呼吸不動、拉滑桿要點一下 Scene 視窗才看得到變化。
        ///
        /// ★ 材質球上的時間動畫（_Time.y）另外需要 Scene 視窗工具列的
        ///   Always Refresh 打開，否則 shader 的時間不會前進。
        void EditorTick()
        {
            if (this == null || !livePreview || Application.isPlaying) return;
            LateUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
#endif

        /// 只有「會改變幾何或窗格分配」的欄位變動才重建。
        void OnValidate()
        {
            string sig = $"{label}|{organs.eyes}{organs.mouths}{organs.headBuds}{organs.limbs}" +
                         $"|{organs.organAmount}|{organs.appendageAmount}" +
                         $"|{(bodyMaterial == null ? 0 : bodyMaterial.GetInstanceID())}" +
                         $"|{(organMaterial == null ? 0 : organMaterial.GetInstanceID())}";
            if (sig != _rebuildSig) { _rebuildSig = sig; rebuildNow = true; }
        }

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

        /// FNV-1a(label + index)。同一個 label 永遠得到同一組窗格分配，重建後不會變。
        static uint Hash(string s, int i)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (s != null) foreach (char c in s) { h ^= c; h *= 16777619u; }
                h ^= (uint)(i + 1); h *= 16777619u;
                return h;
            }
        }

        /// 第 i 個節點該看整張圖的哪一塊。回傳 Unity UV 空間的 (offset.xy, size.zw)。
        /// ★ 每幀重算，所以在 Inspector 微調 subjectWindows 會立刻看到結果，
        ///   不需要重建整隻。成本是幾十次算術，可以忽略。
        Vector4 ComputeWindow(int i, int n)
        {
            if (windowMode == WindowMode.Subject && subjectWindows != null && subjectWindows.Count > 0)
            {
                var r = subjectWindows[(int)(Hash(label, i) % (uint)subjectWindows.Count)];

                // ★ Y 軸翻轉。subjectWindows 用左上原點（跟 Photoshop 一致，方便你量），
                //   Unity 的 UV 原點在左下。
                float w = Mathf.Clamp(r.width, 0.01f, 1f);
                float h = Mathf.Clamp(r.height, 0.01f, 1f);
                float x = Mathf.Clamp(r.x, 0f, 1f - w);
                float yTop = Mathf.Clamp(r.y, 0f, 1f - h);
                return new Vector4(x, 1f - (yTop + h), w, h);
            }

            int cols = gridCols > 0 ? gridCols : Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));
            float cw = 1f / cols, ch = 1f / rows;
            float cx = (i % cols) * cw + cw * 0.5f;
            float cy = (i / cols) * ch + ch * 0.5f;

            // 以格子中心為錨點放大 → 鄰格內容互相重疊
            float k = Mathf.Max(1f, gridWindowScale);
            float sw = Mathf.Min(1f, cw * k), sh = Mathf.Min(1f, ch * k);
            return new Vector4(Mathf.Clamp(cx - sw * 0.5f, 0f, 1f - sw),
                               Mathf.Clamp(cy - sh * 0.5f, 0f, 1f - sh),
                               sw, sh);
        }

        static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// 刪掉所有 zooid —— 只掃自己前綴的，不會動到原版 ChimeraColony 的 Zooid_。
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
            _params.Clear(); _roles.Clear();
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

                _roots.Add(root); _bodies.Add(br); _organRenderers.Add(or);
                _params.Add(zp); _roles.Add(role);
            }
        }

        void LateUpdate()
        {
            // ★ 重建之後不要 return —— 同一幀就要繼續擺位。
            if (rebuildNow) Build();
            if (Spine == null || _roots.Count == 0) return;

            // ★ 節點數變了就重建。ChimeraBodyPlan.OnValidate() 只認得原版的 ChimeraColony，
            //   抓不到這個元件，所以改成自己比對。
            if (Spine.Count != _roots.Count) { rebuildNow = true; return; }

            float dt, t;
            if (Application.isPlaying)
            {
                dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                t = useUnscaledTime ? Time.unscaledTime : Time.time;
            }
            else
            {
                // ★ 編輯期用真實時間差，而不是固定的 1/60。固定值配上
                //   EditorApplication.update 的不規則頻率，編輯器裡的移動速度
                //   會跟 Play 模式完全不一樣。
                float now = Time.realtimeSinceStartup;
                dt = Mathf.Clamp(now - _editorPrevTime, 0f, 1f / 30f);
                _editorPrevTime = now;
                t = now;
            }

            dt = Mathf.Min(dt, 1f / 30f);
            if (dt <= 0f) dt = 1f / 60f;
            Spine.Tick(dt);

            var plan = Plan;
            int n = Mathf.Min(_roots.Count, Spine.Count);
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
                    // 體制生物：節點大小由骨架給（已含 creatureScale）
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

                    // ★ 這兩個永遠要推：它們是「每顆球必須不一樣」的唯一來源。
                    _mpb.SetFloat(ID_Seed, zp.seed);
                    _mpb.SetVector(ID_UvRect, ComputeWindow(i, n));

                    // 形態：也是逐節點的，但可以暫時凍結來觀察材質球的效果
                    if (!freezePerNodeShape)
                    {
                        _mpb.SetFloat(ID_Seg, zp.seg);
                        _mpb.SetFloat(ID_Radial, zp.radial);
                        _mpb.SetFloat(ID_Warp, zp.warp);
                        _mpb.SetFloat(ID_Taper, zp.taper);
                        _mpb.SetFloat(ID_Lobes, zp.lobes);
                        _mpb.SetFloat(ID_Squash, zp.squash);
                        _mpb.SetFloat(ID_Hue, zp.hue);
                    }
                    _mpb.SetFloat(ID_Pulse, swimPulse ? 1f : 0f);

                    // ★ 以下刻意不推，全部交給材質球，所以那些滑桿隨時有效：
                    //   _Glitch / _GlitchRate / _Burst / _Blocks / _Tear / _Chroma /
                    //   _Blowout / _Quantize / _ProjScale / _Amp / _Facet / _Irid /
                    //   _RimPower / _HueMix / _Dark / _Glass

                    _bodies[i].SetPropertyBlock(_mpb);
                }

                var or = _organRenderers[i];
                if (or != null)
                {
                    _mpb.Clear();
                    _mpb.SetFloat(ID_Phase, zp.seed);
                    _mpb.SetFloat(ID_Len, 0.4f + tendrilLength * 2.2f);
                    _mpb.SetFloat(ID_Hue, zp.hue + 1.2f);
                    or.SetPropertyBlock(_mpb);
                }
            }

            if (_needRebuildNextFrame) { _needRebuildNextFrame = false; rebuildNow = true; }
        }
    }
}