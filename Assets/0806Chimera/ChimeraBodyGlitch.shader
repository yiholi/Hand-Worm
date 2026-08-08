Shader "Chimera/BodyGlitch"
{
    // Opaque zooid body shader for Quest 3 / URP / OpenXR Single Pass Instanced.
    //
    // ASCII ONLY on purpose. ShaderLab reported "Parse error: unexpected
    // $undefined" with line -1 on an earlier version that had CJK text and
    // full-width punctuation in its comments. Keep this file pure ASCII.
    //
    // Mechanism: the whole creature shares one collage basemap. Each zooid
    // samples only the window given by _UvRect, then its own _Seed decides how
    // that window gets corrupted. One image torn across the colony.
    //
    // AUTHORING FIX, and the reason this file was rewritten:
    // an earlier version multiplied Tear, Chroma, Blowout and Posterise all by
    // one shared gate, hit = step(1 - _Glitch, h) * burst. With a low _Glitch
    // that gate is zero across most of the surface, so dragging any of those
    // four sliders was multiplying by zero and appeared to do nothing.
    // Now every effect has its OWN gate off its OWN hash, so each slider
    // responds independently. Chroma and Posterise also keep a permanent base
    // fraction so they always show a response while being dragged.
    //
    // Time: corruption is driven by floor(_Time.y * _GlitchRate), so time is
    // quantised into discrete steps. Continuous time makes the surface flow and
    // reads as water; discrete steps read as dropped frames, and low-frequency
    // jumps are far more comfortable in a headset than continuous shimmer.
    //
    // VR safety: glitch inputs are object-space position, _Seed, and time.
    // No screen position, no view direction, so both eyes compute the same
    // result and stereo fusion holds. Facet normal uses ddx/ddy, which is
    // geometry derived and therefore also eye consistent.

    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}

        _UvRect    ("UV Window offset xy size zw", Vector) = (0, 0, 1, 1)
        _ProjScale ("Projection Scale", Range(0.1, 2)) = 0.5

        // Master. Fraction of blocks eligible for corruption.
        _Glitch     ("Glitch Amount", Range(0, 1)) = 0.3
        _GlitchRate ("Glitch Rate steps per sec", Range(0, 24)) = 4
        _Burst      ("Burst fraction of time corrupt", Range(0, 1)) = 0.5
        _Drift      ("Drift", Range(0, 0.1)) = 0
        _Blocks     ("Block Count", Range(2, 64)) = 10

        // Each of these now has its own independent gate.
        _Tear     ("Tear", Range(0, 0.5)) = 0.14
        _Chroma   ("Chroma Split", Range(0, 0.1)) = 0.02
        _Blowout  ("Channel Blowout", Range(0, 1)) = 0.2
        _Quantize ("Posterise", Range(0, 1)) = 0.15

        // Per-node values. Pushed by MaterialPropertyBlock at runtime, so the
        // sliders here only affect the preview sphere and the authoring mode.
        _Seg    ("Segment (per node)", Range(0, 1)) = 0.4
        _Radial ("Radial (per node)", Range(0, 1)) = 0.45
        _Warp   ("Warp (per node)", Range(0, 1)) = 0.15
        _Taper  ("Taper (per node)", Range(0, 1)) = 0.3
        _Lobes  ("Lobes (per node)", Range(0, 8)) = 3
        _Squash ("Squash (per node)", Range(0, 3)) = 1
        _Seed   ("Seed (per node)", Float) = 0
        _Hue    ("Hue Shift (per node)", Float) = 0
        _Pulse  ("Pulse On", Range(0, 1)) = 1

        // Uniform across the whole creature, so these are owned by the material
        // and never pushed per node. Dragging them always works.
        _Amp      ("Displacement Amp", Range(0, 0.6)) = 0.12
        _Facet    ("Facet", Range(0, 1)) = 0.1
        _Irid     ("Rim Iridescence", Range(0, 2)) = 0.45
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _HueMix   ("Hue Shift Mix", Range(0, 1)) = 0.1
        _Dark     ("Darken", Range(0, 1)) = 0
        _Glass    ("Unused MPB compat", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _UvRect;
                float _ProjScale;
                float _Glitch;
                float _GlitchRate;
                float _Burst;
                float _Drift;
                float _Blocks;
                float _Tear;
                float _Chroma;
                float _Blowout;
                float _Quantize;
                float _Seg;
                float _Radial;
                float _Warp;
                float _Taper;
                float _Lobes;
                float _Squash;
                float _Seed;
                float _Hue;
                float _Pulse;
                float _Amp;
                float _Facet;
                float _Irid;
                float _RimPower;
                float _HueMix;
                float _Dark;
                float _Glass;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 basePosOS : TEXCOORD2;   // undisplaced OS position, used for texture coords
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Displacement. All sin combinations. No texture lookups, no noise tables.
            float3 Displace(float3 p, float3 n)
            {
                float seg = sin(p.y * 9.0 + _Seed * 6.283) * _Seg;

                float ang = atan2(p.z, p.x);
                float rad = sin(ang * max(1.0, _Lobes) + _Seed * 3.1) * _Radial;

                float warp = (sin(p.x * 4.3 + _Seed * 11.0)
                            + sin(p.y * 3.7 + _Seed * 7.0)
                            + sin(p.z * 5.1 + _Seed * 13.0)) * 0.333 * _Warp;

                float breathe = _Pulse * 0.05 * sin(_Time.y * 2.4 + _Seed * 6.283);

                p += n * ((seg + rad + warp) * _Amp + breathe);

                p.xz *= 1.0 - saturate(_Taper) * saturate(p.y * 0.5 + 0.5) * 0.6;
                p.y *= lerp(1.0, _Squash, 0.5);
                return p;
            }

            // Hue rotation. _HueMix defaults low on purpose: this collage is
            // recognisable because of its original colours. Rotate too far and
            // a fish stops reading as a fish.
            float3 HueShift(float3 c, float a)
            {
                float3 k = float3(0.57735, 0.57735, 0.57735);
                float cs = cos(a);
                float sn = sin(a);
                return c * cs + cross(k, c) * sn + k * dot(k, c) * (1.0 - cs);
            }

            float Hash21(float2 p, float s)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233)) + s * 37.0) * 43758.5453);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 baseOS = IN.positionOS.xyz;
                float3 nOS = normalize(IN.normalOS);
                float3 dispOS = Displace(baseOS, nOS);

                // Texture coords use the UNDISPLACED position, otherwise the
                // image swims across the surface while the zooid breathes.
                OUT.basePosOS = baseOS;
                OUT.positionWS = TransformObjectToWorld(dispOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(nOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Time quantised into discrete steps. floor() so corruption
                // JUMPS between states instead of smearing between them.
                float tq = floor(_Time.y * _GlitchRate);

                // Burst envelope over time. Guarded so that _GlitchRate = 0 or
                // _Burst = 1 gives burst = 1 rather than silently killing every
                // effect, which is exactly the trap the old version fell into.
                float burstH = Hash21(float2(tq, 41.0), _Seed + 5.0);
                float burst = (_GlitchRate <= 0.001 || _Burst >= 0.999)
                            ? 1.0
                            : step(1.0 - _Burst, burstH);

                // Object-space planar projection, with optional slow drift.
                float2 local = IN.basePosOS.xy * _ProjScale + 0.5;
                local.x += _Drift * _Time.y;
                local = saturate(frac(local));

                float2 blk = floor(local * _Blocks) / _Blocks;
                float ts = tq * 0.137;

                // INDEPENDENT GATES. Each effect draws its own hash, so the
                // corrupted blocks differ per effect and each slider produces a
                // visible change on its own even when the others are at zero.
                float gTear  = step(1.0 - _Glitch, Hash21(blk, _Seed + ts + 1.0)) * burst;
                float gChrom = step(1.0 - _Glitch, Hash21(blk, _Seed + ts + 2.0)) * burst;
                float gBlow  = step(1.0 - _Glitch, Hash21(blk, _Seed + ts + 3.0)) * burst;
                float gQuant = step(1.0 - _Glitch, Hash21(blk, _Seed + ts + 4.0)) * burst;
                float hTear  = Hash21(blk, _Seed + ts + 11.0);

                // Posterise keeps a base fraction so the slider always responds.
                float2 q = lerp(local, blk, _Quantize * (0.35 + 0.65 * gQuant));

                float2 uv = _UvRect.xy + q * _UvRect.zw;

                // Tear in FULL-IMAGE space so sampling deliberately escapes the
                // node's own cell and bleeds in a neighbour's content. Crossing
                // the fragment boundary is part of the effect, not a bug.
                float row = floor(local.y * _Blocks);
                float hRow = Hash21(float2(row, 7.0), _Seed + ts + 13.0);
                uv.x += (hTear - 0.5) * _Tear * gTear;
                uv.x += (hRow - 0.5) * _Tear * 0.5 * step(1.0 - _Glitch * 0.6, hRow) * burst;

                // Chroma also keeps a base fraction, same reason as Posterise.
                float cd = (hTear - 0.5) * _Chroma * (0.35 + 0.65 * gChrom);

                // Explicit LOD 0: uv jumps at block edges, and letting the GPU
                // derive mips from that blows up the derivative and produces a
                // shimmering aliased seam along every block boundary.
                half3 col;
                col.r = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv + float2(cd, 0.0), 0).r;
                col.g = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 0).g;
                col.b = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv - float2(cd, 0.0), 0).b;

                // Channel blowout. Pure arithmetic, essentially free.
                col = lerp(col, saturate(col.brg * 1.6), _Blowout * gBlow);

                col = HueShift(col, _Hue * _HueMix);
                col *= 1.0 - _Dark;

                // Facet normal from screen derivatives of the displaced world
                // position. Geometry derived, so consistent across both eyes.
                float3 flatN = normalize(cross(ddy(IN.positionWS), ddx(IN.positionWS)));
                float3 N = normalize(lerp(normalize(IN.normalWS), flatN, saturate(_Facet)));
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                Light mainLight = GetMainLight();
                float ndl = saturate(dot(N, mainLight.direction)) * 0.5 + 0.5;   // half lambert

                half3 ambient = half3(0.32, 0.36, 0.42);   // constant ambient, no SH lookup
                half3 lit = col * (mainLight.color * ndl + ambient);

                // Rim iridescence. Going opaque makes the sphere look dry if the
                // basemap is all there is, so the old glass fresnel character is
                // kept as an additive rim on top of the texture.
                float fres = pow(saturate(1.0 - saturate(dot(N, V))), _RimPower);
                half3 irid = half3(0.5 + 0.5 * sin(_Seed * 2.0 + float3(0.0, 2.1, 4.2) + fres * 6.0));
                lit += irid * fres * _Irid;

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
