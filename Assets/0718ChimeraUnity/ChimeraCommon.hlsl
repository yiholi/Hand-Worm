#ifndef CHIMERA_COMMON_INCLUDED
#define CHIMERA_COMMON_INCLUDED

// ---------------- 3D simplex noise (Ashima, HLSL port) ----------------
float3 cmod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 cmod289(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 cpermute(float4 x) { return cmod289(((x * 34.0) + 1.0) * x); }
float4 ctaylorInvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

float csnoise(float3 v)
{
    const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
    const float4 D = float4(0.0, 0.5, 1.0, 2.0);
    float3 i  = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);
    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);
    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - D.yyy;
    i = cmod289(i);
    float4 p = cpermute(cpermute(cpermute(
                 i.z + float4(0.0, i1.z, i2.z, 1.0))
               + i.y + float4(0.0, i1.y, i2.y, 1.0))
               + i.x + float4(0.0, i1.x, i2.x, 1.0));
    float n_ = 0.142857142857;
    float3 ns = n_ * D.wyz - D.xzx;
    float4 j = p - 49.0 * floor(p * ns.z * ns.z);
    float4 x_ = floor(j * ns.z);
    float4 y_ = floor(j - 7.0 * x_);
    float4 x = x_ * ns.x + ns.yyyy;
    float4 y = y_ * ns.x + ns.yyyy;
    float4 h = 1.0 - abs(x) - abs(y);
    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);
    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -step(h, 0.0);
    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
    float3 p0 = float3(a0.xy, h.x);
    float3 p1 = float3(a0.zw, h.y);
    float3 p2 = float3(a1.xy, h.z);
    float3 p3 = float3(a1.zw, h.w);
    float4 norm = ctaylorInvSqrt(float4(dot(p0,p0), dot(p1,p1), dot(p2,p2), dot(p3,p3)));
    p0 *= norm.x; p1 *= norm.y; p2 *= norm.z; p3 *= norm.w;
    float4 m = max(0.6 - float4(dot(x0,x0), dot(x1,x1), dot(x2,x2), dot(x3,x3)), 0.0);
    m = m * m;
    return 42.0 * dot(m * m, float4(dot(p0,x0), dot(p1,x1), dot(p2,x2), dot(p3,x3)));
}

// ---------------- 本體位移 ----------------
// d 必須是正規化的物件空間方向（單位 icosphere 的頂點）
float3 ChimeraDisplace(float3 d, float seg, float radial, float warp, float taper,
                       float seed, float lobes, float squash, float pulse, float time,
                       out float shell)
{
    d = normalize(d);
    float t = d.y * 0.5 + 0.5;
    float r = 1.0;
    r += seg * 0.14 * sin(d.y * (3.0 + seg * 13.0) + seed);
    float th = atan2(d.z, d.x);
    r += radial * 0.26 * pow(abs(cos(th * lobes * 0.5)), 2.5) * (0.35 + 0.65 * (1.0 - t));
    float f = 1.3 + warp * 3.0;
    float n = csnoise(d * f + seed) + 0.5 * csnoise(d * f * 2.2 + seed * 2.0);
    r += warp * 0.34 * n;
    r *= lerp(1.0, lerp(0.45, 1.2, t), taper);
    r *= 1.0 + pulse * 0.07 * sin(time * 2.4 + seed);
    shell = n;
    float3 p = d * r;
    p.y *= squash;
    return p;
}

// ---------------- 著色 ----------------
// 面法線由世界座標的導數求得：位移多大都不會有法線錯誤，且自動 flat shading。
float3 ChimeraFlatNormal(float3 worldPos, float facet)
{
    float3 N = normalize(cross(ddy(worldPos), ddx(worldPos)));
    float q = lerp(44.0, 2.4, facet);
    float3 quant = normalize(floor(N * q + 0.5) / q + 1e-5);
    return normalize(lerp(N, quant, facet));
}

// vtype: 0 一般組織 / 1 眼球晶體 / 2 瞳孔·口腔（吸光）/ 3 齒·爪（骨白）
float4 ChimeraShade(float3 worldPos, float3 N, float shell, float vtype,
                    float irid, float hue, float dark, float glass)
{
    float3 V = normalize(_WorldSpaceCameraPos - worldPos);
    float fres = pow(1.0 - abs(dot(N, V)), 2.4);
    float3 L = normalize(float3(0.6, 0.9, 0.5));
    float dif = max(dot(N, L), 0.0);

    // cosine palette：比單頻 sin 更接近真實薄膜干涉
    float3 iridC = 0.5 + 0.5 * cos(6.2831 * float3(0.0, 0.33, 0.67)
                   + hue + fres * 3.4 + shell * 1.6 + dif * 1.2);
    float3 base = lerp(float3(0.07, 0.09, 0.11), float3(0.84, 0.82, 0.77),
                       0.12 + dif * 0.80) * (1.0 - dark * 0.40);

    float3 col; float a;
    if (vtype > 2.5)          // 齒／爪
    {
        col = float3(0.90, 0.88, 0.82) * (0.25 + dif * 0.85);
        a = 1.0;
    }
    else if (vtype > 1.5)     // 瞳孔／口腔：吸光，沒有高光白點
    {
        col = float3(0.015, 0.018, 0.025) + iridC * fres * irid * 0.30;
        a = 1.0;
    }
    else if (vtype > 0.5)     // 眼球晶體
    {
        col = base * 0.45 + iridC * fres * irid * 2.1 + pow(dif, 60.0) * 0.9;
        a = lerp(1.0, saturate(0.55 + fres * 0.8), glass);
    }
    else                      // 一般組織
    {
        col = base + iridC * fres * irid * 1.35 + pow(dif, 34.0) * 0.55;
        a = lerp(1.0, saturate(0.13 + fres * 0.95), glass);
    }
    return float4(col * lerp(1.0, a, glass), a);
}

#endif
