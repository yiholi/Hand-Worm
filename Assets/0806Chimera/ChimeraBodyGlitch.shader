Shader "Chimera/BodyGlitch"
{
    // Opaque zooid body shader for Quest 3 / URP / OpenXR Single Pass Instanced.
    //
    // ASCII ONLY on purpose. A previous version used CJK text and full-width
    // punctuation in comments and ShaderLab reported
    // "Parse error: unexpected $undefined" with line -1, which is the classic
    // signature of the ShaderLab layer choking on a character it does not know.
    // Keep this file pure ASCII.
    //
    // Mechanism: the whole creature shares one collage basemap. Each zooid
    // samples only the window given by _UvRect, then its own _Seed decides how
    // that window gets corrupted. One image torn across the colony.
    //
    // VR safety: glitch inputs are object-space position and _Seed only.
    // No screen position, no view direction, so both eyes compute the same
    // result and stereo fusion holds. Facet normal uses ddx/ddy, which is
    // geometry derived and therefore also eye consistent.
    //
    // Property names match the MaterialPropertyBlock pushed by
    // DataStyleChimeraColony.

    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}

        _UvRect    ("UV Window offset xy size zw", Vector) = (0, 0, 1, 1)
        _ProjScale ("Projection Scale", Range(0.1, 2)) = 0.5

        _Glitch   ("Glitch Amount", Range(0, 1)) = 0.35
        _Blocks   ("Block Count", Range(2, 64)) = 14
        _Tear     ("Tear", Range(0, 0.5)) = 0.12
        _Chroma   ("Chroma Split", Range(0, 0.1)) = 0.012
        _Blowout  ("Channel Blowout", Range(0, 1)) = 0.35
        _Quantize ("Posterise", Range(0, 1)) = 0.25

        _Seg    ("Segment", Range(0, 1)) = 0.4
        _Radial ("Radial", Range(0, 1)) = 0.5
        _Warp   ("Warp", Range(0, 1)) = 0.2
        _Taper  ("Taper", Range(0, 1)) = 0.3
        _Lobes  ("Lobes", Range(0, 8)) = 3
        _Squash ("Squash", Range(0, 3)) = 1
        _Seed   ("Seed", Float) = 0
        _Pulse  ("Pulse On", Range(0, 1)) = 1
        _Amp    ("Displacement Amp", Range(0, 0.6)) = 0.22

        _Facet    ("Facet", Range(0, 1)) = 0.3
        _Irid     ("Rim Iridescence", Range(0, 2)) = 0.6
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _Hue      ("Hue Shift", Float) = 0
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
                float _Pulse;
                float _Amp;
                float _Facet;
                float _Irid;
                float _RimPower;
                float _Hue;
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

                // Object-space planar projection.
                float2 local = saturate(IN.basePosOS.xy * _ProjScale + 0.5);

                // Block hash.
                float2 blk = floor(local * _Blocks) / _Blocks;
                float h = Hash21(blk, _Seed);
                float hit = step(1.0 - _Glitch, h);

                // Block quantise: snap in-cell coords to the block corner,
                // which gives the DCT blocking look.
                float2 q = lerp(local, blk, _Quantize * hit);

                // Land inside this node's own window.
                float2 uv = _UvRect.xy + q * _UvRect.zw;

                // Tear in FULL-IMAGE space so sampling deliberately escapes the
                // node's own cell and bleeds in a neighbour's content. Crossing
                // the fragment boundary is part of the effect, not a bug.
                float row = floor(local.y * _Blocks);
                float hr = Hash21(float2(row, 7.0), _Seed + 3.0);
                uv.x += (h - 0.5) * _Tear * hit;
                uv.x += (hr - 0.5) * _Tear * 0.5 * step(1.0 - _Glitch * 0.6, hr);

                // Three channels offset slightly for chromatic split.
                // Explicit LOD 0: uv jumps at block edges, and letting the GPU
                // derive mips from that blows up the derivative and produces a
                // shimmering aliased seam along every block boundary.
                float cd = (h - 0.5) * _Chroma * hit;
                half3 col;
                col.r = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv + float2(cd, 0.0), 0).r;
                col.g = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 0).g;
                col.b = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv - float2(cd, 0.0), 0).b;

                // Channel blowout. Pure arithmetic, essentially free.
                float bo = step(1.0 - _Glitch * 0.4, Hash21(blk, _Seed + 19.0)) * _Blowout;
                col = lerp(col, saturate(col.brg * 1.6), bo);

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
