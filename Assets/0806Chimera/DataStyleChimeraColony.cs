using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    [ExecuteAlways]
    [AddComponentMenu("Chimera/Colony - Data Style（basemap 碎片）")]
    public class DataStyleChimeraColony : MonoBehaviour
    {
        // ★ 修改：新增了 SubjectByPart (依部位指定) 模式
        public enum WindowMode { Grid, SubjectRandom, SubjectByPart }
        
        public enum TimeLabelMode { None, OnStart, EveryMinute, EverySecond }

        [Header("來源")]
        [Tooltip("您原本可以手動輸入的字串。")]
        public string label = "緣分";
        
        [Header("時間資料 (Data 時間)")]
        [Tooltip("選擇如何將 Label 自動替換為目前的系統時間。")]
        public TimeLabelMode timeLabelMode = TimeLabelMode.None;
        private string _startupTimeLabel = "";

        public MonoBehaviour spineProviderBehaviour;

        [Header("材質（兩個都必須指定，否則不會生成）")]
        public Material bodyMaterial;
        public Material organMaterial;

        [Header("編輯期")]
        public bool livePreview = true;
        public bool freezePerNodeShape = false;

        [Header("Basemap 碎片")]
        // ★ 預設幫你切換到我們新寫的「依部位指定」模式
        public WindowMode windowMode = WindowMode.SubjectByPart;
        public int gridCols = 0;
        [Range(1f, 6f)] public float gridWindowScale = 1f;

        [Header("隨機模式 (SubjectRandom) 共用圖庫")]
        [Tooltip("這是原本的模式，所有的球都會從這個清單裡隨機抽圖。")]
        public List<Rect> subjectWindows = new List<Rect>
        {
            new Rect(0.00f, 0.00f, 0.23f, 0.37f),
            new Rect(0.17f, 0.00f, 0.36f, 0.26f),
            new Rect(0.53f, 0.00f, 0.47f, 0.36f),
            new Rect(0.46f, 0.23f, 0.30f, 0.24f),
        };

        // =========================================================
        // ★ 新增：專屬部位圖庫區塊 (SubjectByPart 模式專用)
        // 在這裡，你可以為不同的部位，指定它們專屬的 Basemap 裁切區域。
        // 當每分鐘發生突變時，該部位的球就只會從自己專屬的清單裡隨機重組！
        // =========================================================
        [Header("依部位指定模式 (SubjectByPart) 專屬圖庫")]
        
        [Tooltip("分配給『頭部 (Head)』的專屬圖片區域清單")]
        public List<Rect> headWindows = new List<Rect> { 
            new Rect(0.00f, 0.00f, 0.23f, 0.37f) // 預設放一張，你可以自己按 + 號增加
        };
        
        [Tooltip("分配給『軀幹 (Trunk)』的專屬圖片區域清單")]
        public List<Rect> trunkWindows = new List<Rect> { 
            new Rect(0.17f, 0.00f, 0.36f, 0.26f) 
        };
        
        [Tooltip("分配給『觸手/附肢 (Drift)』的專屬圖片區域清單")]
        public List<Rect> driftWindows = new List<Rect> { 
            new Rect(0.53f, 0.00f, 0.47f, 0.36f) 
        };
        
        [Tooltip("分配給『尾巴 (Tail)』的專屬圖片區域清單")]
        public List<Rect> tailWindows = new List<Rect> { 
            new Rect(0.46f, 0.23f, 0.30f, 0.24f) 
        };
        // =========================================================

        [Header("整體縮放")]
        [Range(0.02f, 3f)] public float colonyScale = 1f;

        [Header("形態")]
        [Range(0.3f, 2f)] public float zooidScale = 0.95f;
        public bool swimPulse = true;
        
        [Tooltip("器官附肢的長度（推給 organ 材質的 _Len）。")]
        [Range(0f, 1f)] public float tendrilLength = 0.5f;

        [Header("器官／附肢")]
        public OrganSettings organs = new OrganSettings();

        [Header("重建")]
        public bool rebuildNow;

        [Header("診斷")]
        public bool useUnscaledTime = true;

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
        Vector4[] _cachedWindows;

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
        static readonly int ID_Len = Shader.PropertyToID("_Len");
        static readonly int ID_Phase = Shader.PropertyToID("_Phase");

        void OnEnable()
        {
            rebuildNow = true;

            var legacy = GetComponent<ChimeraColony>();
            if (legacy != null && legacy.enabled)
                Debug.LogWarning("[Chimera] 同時啟用了兩個 Colony 元件，請停用其中一個。", this);

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
        void EditorTick()
        {
            if (this == null || !livePreview || Application.isPlaying) return;
            LateUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
#endif

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

        // ★ 核心修改：計算每顆球的裁切區域時，現在會讀取它的「部位角色 (role)」
        Vector4 ComputeWindow(int i, int n, ChimeraRole role)
        {
            if (windowMode == WindowMode.SubjectByPart)
            {
                // 先預設一個圖庫
                List<Rect> currentPool = subjectWindows;
                
                // 根據這顆球的部位，切換成它專屬的圖庫
                if (role == ChimeraRole.Head && headWindows != null && headWindows.Count > 0) 
                    currentPool = headWindows;
                else if (role == ChimeraRole.Trunk && trunkWindows != null && trunkWindows.Count > 0) 
                    currentPool = trunkWindows;
                else if (role == ChimeraRole.Drift && driftWindows != null && driftWindows.Count > 0) 
                    currentPool = driftWindows;
                else if (role == ChimeraRole.Tail && tailWindows != null && tailWindows.Count > 0) 
                    currentPool = tailWindows;

                // 如果專屬圖庫是空的，就拿預設圖庫來墊背
                if (currentPool == null || currentPool.Count == 0) 
                    currentPool = subjectWindows;

                if (currentPool != null && currentPool.Count > 0)
                {
                    // 在指定的專屬圖庫中，用 Hash (包含會變動的時間標籤) 隨機抽出一張
                    var r = currentPool[(int)(Hash(label, i) % (uint)currentPool.Count)];
                    float w = Mathf.Clamp(r.width, 0.01f, 1f);
                    float h = Mathf.Clamp(r.height, 0.01f, 1f);
                    float x = Mathf.Clamp(r.x, 0f, 1f - w);
                    float yTop = Mathf.Clamp(r.y, 0f, 1f - h);
                    return new Vector4(x, 1f - (yTop + h), w, h);
                }
            }
            else if (windowMode == WindowMode.SubjectRandom && subjectWindows != null && subjectWindows.Count > 0)
            {
                // 原本的隨機模式
                var r = subjectWindows[(int)(Hash(label, i) % (uint)subjectWindows.Count)];
                float w = Mathf.Clamp(r.width, 0.01f, 1f);
                float h = Mathf.Clamp(r.height, 0.01f, 1f);
                float x = Mathf.Clamp(r.x, 0f, 1f - w);
                float yTop = Mathf.Clamp(r.y, 0f, 1f - h);
                return new Vector4(x, 1f - (yTop + h), w, h);
            }

            // Grid 模式 (如果沒被選中，這段依然會保留以防萬一)
            int cols = gridCols > 0 ? gridCols : Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));
            float cw = 1f / cols, ch = 1f / rows;
            float cx = (i % cols) * cw + cw * 0.5f;
            float cy = (i / cols) * ch + ch * 0.5f;

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

        public void ClearZooids()
        {
            var kill = new List<GameObject>();
            foreach (Transform c in transform)
                if (c != null && c.name.StartsWith(ZOOID_PREFIX)) kill.Add(c.gameObject);

            foreach (var g in kill)
            {
                var mfs = g.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in mfs)
                    if (mf != null && mf.sharedMesh != null && mf.sharedMesh != _bodyMesh)
                        SafeDestroy(mf.sharedMesh);
                SafeDestroy(g);
            }
            _roots.Clear(); _bodies.Clear(); _organRenderers.Clear();
            _params.Clear(); _roles.Clear();
            _cachedWindows = null;
        }

        public void Build()
        {
            rebuildNow = false;
            _spine = null;

            ClearZooids();

            if (Spine == null || bodyMaterial == null || organMaterial == null) return;

            if (_bodyMesh == null) _bodyMesh = IcoSphere.Create(3);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            var plan = Plan;
            int n = Spine.Count;
            if (n <= 0) { rebuildNow = true; return; }

            Vector3 head = Spine.GetPoint(0);
            _cachedWindows = new Vector4[n];

            for (int i = 0; i < n; i++)
            {
                var zp = ChimeraHash.Make(label, i);
                var role = plan != null ? plan.GetRole(i) : ZoneToRole(ChimeraHash.ZoneOf(i, n));
                bool isHead = role == ChimeraRole.Head;

                var rootGO = new GameObject($"{ZOOID_PREFIX}{i:00}_{role}");
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

                root.position = plan != null
                    ? Spine.GetPoint(i)
                    : head + (Spine.GetPoint(i) - head) * colonyScale;
                root.rotation = Quaternion.FromToRotation(Vector3.up, Spine.GetForward(i));

                _roots.Add(root); _bodies.Add(br); _organRenderers.Add(or);
                _params.Add(zp); _roles.Add(role);
                
                // ★ 將角色的資訊 (role) 傳給函式去計算裁切圖
                _cachedWindows[i] = ComputeWindow(i, n, role);
            }
        }

        void LateUpdate()
        {
            if (timeLabelMode != TimeLabelMode.None)
            {
                string newLabel = label;
                System.DateTime now = System.DateTime.Now;

                if (timeLabelMode == TimeLabelMode.OnStart)
                {
                    if (Application.isPlaying)
                    {
                        if (string.IsNullOrEmpty(_startupTimeLabel))
                            _startupTimeLabel = now.ToString("yyyy-MM-dd HH:mm:ss");
                        newLabel = _startupTimeLabel;
                    }
                }
                else if (timeLabelMode == TimeLabelMode.EveryMinute)
                {
                    newLabel = now.ToString("yyyy-MM-dd HH:mm");
                }
                else if (timeLabelMode == TimeLabelMode.EverySecond)
                {
                    newLabel = now.ToString("yyyy-MM-dd HH:mm:ss");
                }

                if (label != newLabel)
                {
                    label = newLabel;
                    rebuildNow = true;
                }
            }


            if (rebuildNow) Build();
            if (Spine == null || _roots.Count == 0) return;

            if (Spine.Count != _roots.Count) { rebuildNow = true; return; }

            float dt, t;
            if (Application.isPlaying)
            {
                dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                t = useUnscaledTime ? Time.unscaledTime : Time.time;
            }
            else
            {
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

                // 取得快取的 UV 窗格 
                Vector4 targetWindow = (_cachedWindows != null && i < _cachedWindows.Length) ? _cachedWindows[i] : ComputeWindow(i, n, _roles[i]);

                if (_bodies[i] != null)
                {
                    _mpb.Clear();
                    _mpb.SetFloat(ID_Seed, zp.seed);
                    _mpb.SetVector(ID_UvRect, targetWindow);

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

                    _bodies[i].SetPropertyBlock(_mpb);
                }

                var or = _organRenderers[i];
                if (or != null)
                {
                    _mpb.Clear();
                    
                    _mpb.SetVector(ID_UvRect, targetWindow);
                    _mpb.SetFloat(ID_Phase, zp.seed); 
                    _mpb.SetFloat(ID_Seed, zp.seed);  
                    
                    _mpb.SetFloat(ID_Len, 0.4f + tendrilLength * 2.2f);
                    
                    _mpb.SetFloat(ID_Hue, zp.hue + 1.2f);
                    or.SetPropertyBlock(_mpb);
                }
            }

            if (_needRebuildNextFrame) { _needRebuildNextFrame = false; rebuildNow = true; }
        }
    }
}