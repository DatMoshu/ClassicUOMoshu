// 3DCUO PROTOTYPE — per-pixel wet & snow ground overlay (fx_2_0 / ps_3_0).
//
// Renders over already-textured land geometry (one quad per UO tile) to add:
//   - Wet technique:   damp tint everywhere + cellular-noise puddles with
//                      animated ripples and pre-fragment sky-sheen highlight.
//   - Snow technique:  perlin/flake noise alpha mask with a TRUE per-pixel
//                      slope mask (cross of ddx/ddy of WorldPos → world-space
//                      surface normal — the per-pixel equivalent of
//                      `dot(N, up)` in a triplanar shader without needing the
//                      vertex format to carry normals).
//
// Vertex format: float3 Position only. Land overlay VBO stores world-space
// positions directly (matching LandMesh3D's VertexPositionColorTexture layout
// for the base pass), so World matrix is usually identity (or scaled Y for
// HeightExaggeration).
//
// Inputs:
//   World/View/Projection — standard.
//   NoiseTex      — perlin (R = noise 0..1)            — snow patchiness, ripples
//   PuddleTex     — cellular/voronoi (R = cell value)  — puddle clusters
//   FlakeTex      — flake/sparkle pattern (R = mask)   — snow detail/sparkle
//   Time          — seconds; drives ripples + sparkle scroll
//   Intensity     — 0..1 master strength slider
//   NoiseScale    — world-units → uv multiplier (e.g. 1/256 → ~12 tiles per tile)
//   PuddleScale   — separate scale for puddle texture (cells)
//   FlakeScale    — finer scale for snow flakes
//   PuddleThresh  — cellular threshold above which a pixel becomes "puddle"

float4x4 World;
float4x4 View;
float4x4 Projection;

texture NoiseTex;
sampler NoiseSampler : register(s0) = sampler_state
{
    Texture = <NoiseTex>;
    MipFilter = LINEAR; MinFilter = LINEAR; MagFilter = LINEAR;
    AddressU = WRAP; AddressV = WRAP;
};

texture PuddleTex;
sampler PuddleSampler : register(s1) = sampler_state
{
    Texture = <PuddleTex>;
    MipFilter = LINEAR; MinFilter = LINEAR; MagFilter = LINEAR;
    AddressU = WRAP; AddressV = WRAP;
};

texture FlakeTex;
sampler FlakeSampler : register(s2) = sampler_state
{
    Texture = <FlakeTex>;
    MipFilter = LINEAR; MinFilter = LINEAR; MagFilter = LINEAR;
    AddressU = WRAP; AddressV = WRAP;
};

// Base atlas of the static being overlaid (leaf/trunk texture). Foliage
// techniques sample its alpha to avoid painting transparent pixels — without
// this the overlay would draw white/dark squares around every leaf cutout.
texture BaseAlphaTex;
sampler BaseAlphaSampler : register(s3) = sampler_state
{
    Texture = <BaseAlphaTex>;
    MipFilter = LINEAR; MinFilter = POINT; MagFilter = POINT;
    AddressU = CLAMP; AddressV = CLAMP;
};

float  Time;
float  Intensity;
float  NoiseScale;     // default ~ 1/256
float  PuddleScale;    // default ~ 1/180
float  FlakeScale;     // default ~ 1/80
float  PuddleThresh;   // default ~ 0.55
float  HeightBias;     // small world-Y offset to defeat z-fighting (e.g. 0.5)
// De-tiling: when > 0, PS_Snow rotates and offsets each "macro cell" of
// the snow noise so the eye can't pick out a repeating tile when zoomed
// out. 1.0 = full per-cell randomization. Cell size = SnowCellSize world units.
float  SnowDetile;     // default 1.0
float  SnowCellSize;   // default 220 world units (~10 UO tiles)
// Wet ground: master multiplier on puddle visibility (0 = damp only, no
// puddles; 1 = full puddles). Surfaced as the "Puddle amount" slider.
float  PuddleAmount;   // default 1.0

struct VS_IN
{
    float4 Position : POSITION0;
};

struct VS_OUT
{
    float4 Position : POSITION0;
    float3 WorldPos : TEXCOORD0;
};

VS_OUT VS_Main(VS_IN i)
{
    VS_OUT o;
    float4 wp = mul(i.Position, World);
    // Lift overlay slightly above the base land plane to defeat z-fighting.
    // Noise sampling uses XZ only so the lift is invisible in the result.
    wp.y += HeightBias;
    o.WorldPos = wp.xyz;
    o.Position = mul(mul(wp, View), Projection);
    return o;
}

// Per-pixel surface normal from screen-space derivatives of WorldPos. With
// ps_3_0 these are real partial derivatives — works on any geometry without
// shipping vertex normals. Equivalent to triplanar's `dot(N, up)` source.
float ComputeUpFactor(float3 wp)
{
    float3 dx = ddx(wp);
    float3 dy = ddy(wp);
    float3 N  = normalize(cross(dy, dx));   // sign chosen to match Y-up
    return saturate(abs(N.y));              // 1 = flat, 0 = vertical wall
}

// 2D hash → [0,1). Cheap value-noise hash — used to randomize the noise
// texture's phase + rotation per macro-cell so the eye can't latch on
// to the underlying texture period when the camera is zoomed out.
float Hash21(float2 p)
{
    return frac(sin(dot(floor(p), float2(127.1, 311.7))) * 43758.5453);
}

// Sample ground-layer noise with per-cell phase + rotation jitter. cell is
// the size (world units) of one randomization tile. Returns a single channel
// in [0,1]. Two adjacent cells overlap with a smooth blend so the seams
// between cells aren't visible.
float SampleDetiled(sampler s, float2 worldXZ, float texScale, float cell, float strength)
{
    if (strength <= 0.001 || cell <= 1.0)
        return tex2D(s, worldXZ * texScale).r;

    float2 cellId  = floor(worldXZ / cell);
    // 2x2 cell blend: sample four corners and lerp by fractional offset.
    float2 fr = frac(worldXZ / cell);
    float total = 0.0;
    float weightSum = 0.0;
    [unroll] for (int dy = 0; dy < 2; ++dy)
    [unroll] for (int dx = 0; dx < 2; ++dx)
    {
        float2 cid = cellId + float2(dx, dy);
        float h1   = Hash21(cid);
        float h2   = Hash21(cid + 71.7);
        float ang  = h1 * 6.28318 * strength;
        float2 off = float2(h1, h2) * 64.0 * strength;
        float ca = cos(ang), sa = sin(ang);
        float2 rotated = float2(worldXZ.x * ca - worldXZ.y * sa,
                                worldXZ.x * sa + worldXZ.y * ca);
        float2 uv = (rotated + off) * texScale;
        float w = (1.0 - abs(fr.x - dx)) * (1.0 - abs(fr.y - dy));
        total += tex2D(s, uv).r * w;
        weightSum += w;
    }
    return total / max(weightSum, 1e-4);
}

float4 PS_Snow(VS_OUT i) : COLOR
{
    // Per-macro-cell phase + rotation breaks the visible repeat of the
    // 512px noise atlas when the camera pulls out. SnowCellSize controls
    // the granularity (smaller = busier, larger = smoother low-frequency).
    float n0 = SampleDetiled(NoiseSampler, i.WorldPos.xz, NoiseScale, SnowCellSize,         SnowDetile);
    // Fine octave gets a different cell size so its randomization doesn't
    // align with the coarse one (which would just shift the visible period).
    float n1 = SampleDetiled(FlakeSampler, i.WorldPos.xz, FlakeScale, SnowCellSize * 0.43,  SnowDetile);
    float patches = saturate(n0 * 0.7 + n1 * 0.5);

    // Per-pixel slope mask — true triplanar-style.
    float upF = ComputeUpFactor(i.WorldPos);
    float slopeMask = smoothstep(0.55, 0.92, upF);

    // Snow accumulates where flat AND noise threshold passed.
    float snow = slopeMask * smoothstep(0.30, 0.85, patches);

    // Sparkle: sub-pixel scintillation that catches the eye when moving.
    float2 uvF = i.WorldPos.xz * FlakeScale;
    float sparkleNoise = tex2D(FlakeSampler, uvF * 4.0 + Time * 0.05).r;
    float sparkle = smoothstep(0.85, 1.0, sparkleNoise) * snow;
    float3 col = float3(0.99, 0.99, 1.02) + sparkle * 0.3;

    // Premultiplied output — matches MonoGame/FNA BlendState.AlphaBlend
    // (src=One, dst=InvSrcAlpha) so the result is lerp(dst, col, a).
    float a = saturate(snow * Intensity);
    return float4(col * a, a);
}

float4 PS_Wet(VS_OUT i) : COLOR
{
    float2 uvP = i.WorldPos.xz * PuddleScale;
    float2 uvN = i.WorldPos.xz * NoiseScale;

    // Cellular gives natural puddle-shaped patches; soft threshold.
    float cell = tex2D(PuddleSampler, uvP).r;
    float puddle = smoothstep(PuddleThresh, PuddleThresh + 0.18, cell);
    // Master "puddle amount" slider — 0 leaves only the background dampness,
    // 1 keeps full puddles (default). Lerps the puddle term, not damp.
    puddle *= saturate(PuddleAmount);

    // Background dampness everywhere (low alpha) + deep puddle (high alpha).
    float damp = 0.30;
    float strength = damp + puddle * 0.65;

    // Wet tint: lerp from cool damp gray to dark teal puddle.
    float3 dampCol   = float3(0.18, 0.22, 0.32);
    float3 puddleCol = float3(0.04, 0.08, 0.22);
    float3 tint = lerp(dampCol, puddleCol, puddle);

    // Animated ripples — two crossed sine waves modulated by perlin to break
    // up the regular pattern. Only visible on puddle pixels.
    float r1 = sin(uvP.x * 90.0 + Time * 3.5);
    float r2 = sin(uvP.y * 95.0 + Time * 4.2);
    float perlin = tex2D(NoiseSampler, uvN + Time * 0.02).r;
    float ripple = saturate((r1 * r2) * 0.5 + 0.5) * perlin * puddle;

    // Sky-color highlight where puddle is deepest — fakes a Fresnel-ish
    // mirror sheen without an actual reflection probe.
    float3 sky = float3(0.55, 0.72, 0.95);
    float shine = smoothstep(0.55, 1.0, puddle) * (0.55 + 0.45 * ripple);
    float3 col = tint + sky * shine * 0.55;

    // Small slope falloff so puddles don't appear on cliff faces.
    float upF = ComputeUpFactor(i.WorldPos);
    float flat = smoothstep(0.55, 0.92, upF);
    strength *= lerp(0.4, 1.0, flat);  // damp can still show on slopes

    // Premultiplied output for BlendState.AlphaBlend (src=One, dst=InvSrcAlpha).
    // Without this the color over-saturates as soon as alpha is non-zero.
    float a = saturate(strength * Intensity);
    return float4(col * a, a);
}

// ===== Foliage variants =====
// Used by Static3DRenderer to overlay snow/wet on tree leaves and trunks.
// Vertex format here is VertexPositionTexture (matches Static3DRenderer's
// per-texture batches), so we get UVs through to the pixel shader and use
// them to sample the base-leaf/trunk alpha — without that mask the overlay
// would paint the transparent corners of every leaf billboard.

struct VS_FoliageIN
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VS_FoliageOUT
{
    float4 Position : POSITION0;
    float3 WorldPos : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
};

VS_FoliageOUT VS_Foliage(VS_FoliageIN i)
{
    VS_FoliageOUT o;
    float4 wp = mul(i.Position, World);
    // No HeightBias — foliage isn't coplanar with the ground, no z-fighting risk.
    o.WorldPos = wp.xyz;
    o.Position = mul(mul(wp, View), Projection);
    o.TexCoord = i.TexCoord;
    return o;
}

float4 PS_SnowFoliage(VS_FoliageOUT i) : COLOR
{
    // Preserve the leaf/trunk cutout — drop transparent pixels entirely.
    float baseA = tex2D(BaseAlphaSampler, i.TexCoord).a;
    clip(baseA - 0.10);

    float2 uvN = i.WorldPos.xz * NoiseScale;
    float2 uvF = i.WorldPos.xz * FlakeScale;
    float n0 = tex2D(NoiseSampler, uvN).r;
    float n1 = tex2D(FlakeSampler, uvF).r;
    float patches = saturate(n0 * 0.7 + n1 * 0.5);

    // RELIABLE SNOW CAP — driven by sprite-space V coordinate so every
    // canopy/trunk gets a visible cap on top regardless of where it falls
    // in the world-cell noise. V=0 is the TOP of the sprite, V=1 is the
    // bottom (anchor at ground). smoothstep(0.55→0.05) means snow is full
    // on the top half, fades out around the middle of the sprite, and is
    // gone on the lower trunk where snow wouldn't realistically pile.
    float topMask = smoothstep(0.55, 0.05, i.TexCoord.y);
    // Noise still contributes a small jitter so adjacent trees don't all
    // have an identical clean-line cap.
    float patchJitter = smoothstep(0.20, 0.85, patches) * 0.25;
    float snow = saturate(topMask * 0.85 + patchJitter);

    float sparkleNoise = tex2D(FlakeSampler, uvF * 4.0 + Time * 0.05).r;
    float sparkle = smoothstep(0.85, 1.0, sparkleNoise) * snow;
    float3 col = float3(0.99, 0.99, 1.02) + sparkle * 0.3;

    // Mask by baseA so anti-aliased leaf edges fade out smoothly.
    float a = saturate(snow * Intensity * baseA);
    return float4(col * a, a);
}

// ===== Fall foliage =====
// Per-tree random autumn tint. Each tree (defined by its quantized world XZ
// position) gets a deterministic seed → picks a fall color (red / orange /
// yellow / gold / brown). Per-pixel noise adds a within-canopy color drift so
// leaves on the same tree aren't a flat single hue. Drawn as an alpha-blend
// pass after the base leaf draw so the underlying texture's silhouette and
// shading stay intact — we replace the *hue*, not the values.
//
// `FallTreeUnit` controls the worldspace cell size that hashes to a single
// tree (default ~ 1 tile = 22 world units). Higher → larger color clumps.
float FallTreeUnit;     // default ~ 22.0
float FallSaturation;   // 0..1 — how strongly autumn replaces base color (default 0.85)

float Hash11(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float3 PickFallColor(float seed)
{
    // Curated palette — five points, lerp between adjacent buckets so
    // neighbouring trees aren't always the same swatch.
    float3 c0 = float3(0.65, 0.13, 0.06);  // crimson red
    float3 c1 = float3(0.85, 0.38, 0.07);  // burnt orange
    float3 c2 = float3(0.93, 0.74, 0.12);  // bright yellow
    float3 c3 = float3(0.74, 0.50, 0.10);  // gold/amber
    float3 c4 = float3(0.48, 0.27, 0.09);  // umber brown
    float t = seed * 4.0;
    if (t < 1.0) return lerp(c0, c1, t);
    if (t < 2.0) return lerp(c1, c2, t - 1.0);
    if (t < 3.0) return lerp(c2, c3, t - 2.0);
    return            lerp(c3, c4, t - 3.0);
}

float4 PS_FallFoliage(VS_FoliageOUT i) : COLOR
{
    float baseA = tex2D(BaseAlphaSampler, i.TexCoord).a;
    clip(baseA - 0.10);

    // Quantise XZ position to a per-tree cell so an entire canopy shares one seed.
    float2 cell = floor(i.WorldPos.xz / max(FallTreeUnit, 1.0));
    float seed = Hash11(cell);
    float3 fallCol = PickFallColor(seed);

    // Per-pixel noise variation within the canopy — some leaves brighter,
    // some darker (mimics the natural patchiness of real fall foliage).
    float n = tex2D(NoiseSampler, i.WorldPos.xz * NoiseScale * 1.5).r;
    fallCol = lerp(fallCol * 0.65, fallCol * 1.15, n);

    // Strength = Intensity * FallSaturation * baseAlpha (cutout).
    float a = saturate(Intensity * FallSaturation * baseA);
    return float4(fallCol * a, a);
}

float4 PS_WetFoliage(VS_FoliageOUT i) : COLOR
{
    float baseA = tex2D(BaseAlphaSampler, i.TexCoord).a;
    clip(baseA - 0.10);

    // Constant cool damp tint — no puddles/ripples/sky-shine on leaves or
    // trunks (water runs off). Slight high-frequency noise breakup so the
    // tint isn't perfectly flat.
    float n = tex2D(NoiseSampler, i.WorldPos.xz * NoiseScale * 2.0).r;
    float strength = 0.40 + n * 0.15;

    float3 dampCol = float3(0.18, 0.22, 0.32);
    float a = saturate(strength * Intensity * baseA);
    return float4(dampCol * a, a);
}

technique SnowTechnique
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS_Main();
        PixelShader  = compile ps_3_0 PS_Snow();
    }
}

technique WetTechnique
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS_Main();
        PixelShader  = compile ps_3_0 PS_Wet();
    }
}

technique SnowFoliageTechnique
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS_Foliage();
        PixelShader  = compile ps_3_0 PS_SnowFoliage();
    }
}

technique WetFoliageTechnique
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS_Foliage();
        PixelShader  = compile ps_3_0 PS_WetFoliage();
    }
}

technique FallFoliageTechnique
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS_Foliage();
        PixelShader  = compile ps_3_0 PS_FallFoliage();
    }
}
