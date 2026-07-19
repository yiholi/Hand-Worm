using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// 群體本體。負責：生成 zooid、依 ISpineProvider 擺位、把參數推進 shader。
    /// ★ 完全不負責「往哪裡走」——那是 spine provider 的事。
    [ExecuteAlways]
    public class ChimeraColony : MonoBehaviour
    {
        [Header("來源")]
        [Tooltip("參與者輸入的字串。同一個字串永遠長出同一隻群體。")]
        public string label = "緣分";

        [Tooltip("留空會自動抓同物件上的 VerletSpine")]
        public MonoBehaviour spineProviderBehaviour;

        [Header("材質（兩個都必須指定，否則不會生成）")]
        public Material bodyMaterial;
        public Material organMaterial;

        [Header("形態")]
        [Range(0.3f, 2f)] public float zooidScale = 0.95f;
        [Range(0f, 1f)] public float facet = 0.45f;
        [Range(0f, 1f)] public float iridescence = 0.8f;
        [Range(0f, 1f)] public float tendrilLength = 0.5f;
        [Range(0f, 1f)] public float glass = 1f;
        public bool swimPulse = true;

        [Header("器官／附肢")]
        public OrganSettings organs = new OrganSettings();

        [Header("重建")]
        public bool rebuildNow;

        const string ZOOID_PREFIX = "Zooid_";

        ISpineProvider _spine;
        readonly List<Transform> _roots = new List<Transform>();
        readonly List<Renderer> _bodies = new List<Renderer>();
        readonly List<Renderer> _organRenderers = new List<Renderer>();
        readonly List<ZooidParams> _params = new List<ZooidParams>();
        MaterialPropertyBlock _mpb;
        Mesh _bodyMesh;
        bool _needRebuildNextFrame;

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

        void OnEnable() { rebuildNow = true; }
        void OnValidate() { rebuildNow = true; }
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

        static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// 刪掉所有 zooid —— 不依賴快取清單，直接掃實際的子物件。
        /// 之前建到一半噴例外留下的孤兒也會一併清掉。
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
            _roots.Clear(); _bodies.Clear(); _organRenderers.Clear(); _params.Clear();
        }

        public void Build()
        {
            rebuildNow = false;
            _spine = null;

            ClearZooids();

            if (Spine == null)
            {
                Debug.LogWarning("[Chimera] 找不到 ISpineProvider（把 VerletSpine 拖進 Spine Provider Behaviour）", this);
                return;
            }
            if (bodyMaterial == null || organMaterial == null)
            {
                Debug.LogWarning("[Chimera] Body Material 或 Organ Material 未指定，未指定的 renderer 會顯示洋紅色。", this);
                return;
            }

            if (_bodyMesh == null) _bodyMesh = IcoSphere.Create(3);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            int n = Spine.Count;
            for (int i = 0; i < n; i++)
            {
                var zp = ChimeraHash.Make(label, i);
                var zone = ChimeraHash.ZoneOf(i, n);
                bool isHead = zone == Zone.Head;

                var rootGO = new GameObject($"{ZOOID_PREFIX}{i:00}_{zone}");
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

                var organMesh = ChimeraMeshBuilder.Build(zp, zone, organs);
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

                // 建立當下就先擺到脊索上。
                // 沒有這行的話，新建的 zooid 在被擺位之前會停在父物件原點 (0,0,0)，
                // 只要有任何一幀沒走到擺位程式碼，看起來就是全部疊成一團。
                root.position = Spine.GetPoint(i);
                root.rotation = Quaternion.FromToRotation(Vector3.up, Spine.GetForward(i));

                _roots.Add(root); _bodies.Add(br); _organRenderers.Add(or); _params.Add(zp);
            }
        }

        void LateUpdate()
        {
            // ★ 重建之後不要 return —— 同一幀就要繼續擺位。
            // 之前寫成 { Build(); return; }，一旦 rebuildNow 每幀都被設回 true，
            // 就會每幀重建、每幀提前返回，永遠走不到下面的擺位，全部停在原點。
            if (rebuildNow) Build();
            if (Spine == null || _roots.Count == 0) return;

            float dt = Application.isPlaying ? Time.deltaTime : 1f / 60f;
            Spine.Tick(dt);

            int n = Mathf.Min(_roots.Count, Spine.Count);
            float t = Application.isPlaying ? Time.time : 0f;

            for (int i = 0; i < n; i++)
            {
                var root = _roots[i];
                if (root == null) { _needRebuildNextFrame = true; continue; }   // 子物件被刪 → 下一幀重建，但這幀其他節照常擺位

                root.position = Spine.GetPoint(i);
                Vector3 fwd = Spine.GetForward(i);
                root.rotation = Quaternion.Slerp(root.rotation,
                    Quaternion.FromToRotation(Vector3.up, fwd), 1f - Mathf.Exp(-12f * dt));

                var zp = _params[i];
                bool isHead = i == 0;
                float zoneT = n > 1 ? (float)i / (n - 1) : 0f;
                float pulse = swimPulse ? 1f + 0.05f * Mathf.Sin(t * 2.4f + zp.seed) : 1f;
                float s = (isHead ? 2.0f : 1.0f) * zooidScale
                          * (0.55f + 0.7f * (1f - zoneT))
                          * (0.85f + 0.3f * (zp.squash - 0.65f)) * 0.40f * pulse;
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