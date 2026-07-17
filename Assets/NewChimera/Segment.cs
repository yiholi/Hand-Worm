using UnityEngine;

// 掛在公版 prefab 上。把一組 SegmentParams 套用到這一節。
// 幾何(scale)在 transform;表面 + 位移全部透過 MaterialPropertyBlock 餵給 shader。
// 一份共享材質,per-instance 外觀,不 leak 材質實例。
[RequireComponent(typeof(Renderer))]
public class Segment : MonoBehaviour
{
    // shader property 名稱要與 SegmentDisplaced.shader 完全一致
    static readonly int BaseColorId    = Shader.PropertyToID("_BaseColor");
    static readonly int DisplaceAmpId  = Shader.PropertyToID("_DisplaceAmp");
    static readonly int NoiseFreqId    = Shader.PropertyToID("_NoiseFreq");
    static readonly int SeedId         = Shader.PropertyToID("_Seed");
    static readonly int ErosionId      = Shader.PropertyToID("_Erosion");
    static readonly int IridescenceId  = Shader.PropertyToID("_Iridescence");

    MaterialPropertyBlock mpb;
    Renderer rend;

    public SegmentParams Params { get; private set; }
    public int ArrivalIndex { get; private set; }

    public void Apply(SegmentParams p, SegmentData d)
    {
        Params = p;
        ArrivalIndex = d.arrivalIndex;

        if (rend == null) rend = GetComponent<Renderer>();
        if (mpb == null)  mpb  = new MaterialPropertyBlock();

        // ---- 幾何:非等比,拉成不同比例的橢球 ----
        transform.localScale = new Vector3(p.girth, p.girth, p.segmentLength);

        // ---- 表面 ----
        float sat = Mathf.Clamp01(0.7f - p.erosion * 0.4f);
        float val = Mathf.Clamp01(1.0f - p.erosion * 0.5f);
        Color baseCol = Color.HSVToRGB(p.hueShift, sat, val);

        // 突起數量 → noise 頻率(越多突起 = 越高頻的疙瘩)
        float noiseFreq = 1.5f + p.protrusionCount * 0.6f;

        // seed → noise 位移,讓每個人的凹凸圖樣都不同(即使參數相近)
        float seedOffset = (Mathf.Abs(d.seed) % 100000) * 0.001f;

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