using UnityEngine;

// 掛在公版 prefab 上。把一組 SegmentParams 套用到這一節。
// 幾何(scale)在 transform;表面 + 位移全部透過 MaterialPropertyBlock 餵給 shader。
// 一份共享材質,per-instance 外觀,不 leak 材質實例。
[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(MeshFilter))] // 加入這個來確保我們可以控制與替換網格模型
public class Segment : MonoBehaviour
{
    [Header("【可替換的模型清單】")]
    // 建立一個陣列，讓你在 Unity 面板可以拖入多個模型
    public Mesh[] availableShapes; 

    // shader property 名稱要與 SegmentDisplaced.shader 完全一致
    static readonly int BaseColorId    = Shader.PropertyToID("_BaseColor");
    static readonly int DisplaceAmpId  = Shader.PropertyToID("_DisplaceAmp");
    static readonly int NoiseFreqId    = Shader.PropertyToID("_NoiseFreq");
    static readonly int SeedId         = Shader.PropertyToID("_Seed");
    static readonly int ErosionId      = Shader.PropertyToID("_Erosion");
    static readonly int IridescenceId  = Shader.PropertyToID("_Iridescence");

    MaterialPropertyBlock mpb;
    Renderer rend;
    MeshFilter meshFilter; // 宣告一個變數來抓取並控制網格模型

    public SegmentParams Params { get; private set; }
    public int ArrivalIndex { get; private set; }

    public void Apply(SegmentParams p, SegmentData d)
    {
        Params = p;
        ArrivalIndex = d.arrivalIndex;

        // 取得身上的組件，確保可以修改材質與模型
        if (rend == null) rend = GetComponent<Renderer>();
        if (mpb == null)  mpb  = new MaterialPropertyBlock();
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

        // 隨機抽換模型的邏輯
        // 如果你有在面板裡放入超過 0 個模型，就會執行這裡
        if (availableShapes != null && availableShapes.Length > 0)
        {
            // 使用這節身體專屬的種子 (d.seed) 的絕對值來除以模型總數取餘數
            // 這樣可以確保「隨機抽換」，而且「同一個種子永遠抽到同一個模型」
            int shapeIndex = Mathf.Abs(d.seed) % availableShapes.Length;

            // 換上面板裡抽中的那個模型
            meshFilter.mesh = availableShapes[shapeIndex];
        }

        // ---- 幾何:非等比,拉成不同比例的橢球 ----
        // 這裡恢復成原本最單純的縮放方式，不再乘上額外的倍數
        transform.localScale = new Vector3(p.girth, p.girth, p.segmentLength);

        // ---- 表面 ----
        float sat = Mathf.Clamp01(0.7f - p.erosion * 0.4f);
        float val = Mathf.Clamp01(1.0f - p.erosion * 0.5f);
        Color baseCol = Color.HSVToRGB(p.hueShift, sat, val);

        // 突起數量 → noise 頻率(越多突起 = 越高頻的疙瘩)
        float noiseFreq = 1.5f + p.protrusionCount * 0.6f;

        // seed → noise 位移,讓每個人的凹凸圖樣都不同(即使參數相近)
        float seedOffset = (Mathf.Abs(d.seed) % 100000) * 0.001f;

        // 將所有計算好的數值送到 Shader 裡面進行渲染
        rend.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId,   baseCol);
        mpb.SetFloat(DisplaceAmpId, p.displaceAmp * 0.15f); // 0.15 = 位移的整體強度,自己調
        mpb.SetFloat(NoiseFreqId,   noiseFreq);
        mpb.SetFloat(SeedId,        seedOffset);
        mpb.SetFloat(ErosionId,     p.erosion);
        mpb.SetFloat(IridescenceId, p.iridescence);
        rend.SetPropertyBlock(mpb);
    }
}