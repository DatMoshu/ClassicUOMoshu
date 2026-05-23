// 3DCUO PROTOTYPE — texture-based linear-blend skinning shader.
// Bones are packed into a 1D float4 texture (4 texels per bone, one per matrix
// row). vs_3_0's tex2Dlod gives us VTF, which lifts the 72-bone constant-register
// limit of FNA's stock SkinnedEffect to ~1024 bones (texture-width-bound).
//
// Vertex format expected: Position (Vec3), Normal (Vec3), TexCoord (Vec2),
// BlendIndices (Byte4 — read as float4 0..255), BlendWeights (Vec4).

float4x4 World;
float4x4 View;
float4x4 Projection;

// IMPORTANT: DiffuseSampler is declared FIRST so it gets the canonical s0
// pixel-shader sampler register (matching FNA BasicEffect's layout, which is
// proven to work with our textures). BoneSampler goes to s2 — only used in VS,
// so no conflict with PS sampling. Earlier attempts using s1 for DiffuseSampler
// silently sampled white due to a MojoShader translation issue with texture/
// sampler pairs at non-zero pixel-shader register indices.

// Diffuse — texture/sampler pair pattern at register s0 (canonical PS slot).
// sampler2D direct syntax was tried but FNA didn't expose the parameter
// under any usable name in the .fxc compiled by native fxc.exe.
texture DiffuseTex;
sampler DiffuseSampler : register(s0) = sampler_state
{
    Texture   = <DiffuseTex>;
    MipFilter = NONE;
    MinFilter = POINT;
    MagFilter = POINT;
    AddressU  = WRAP;
    AddressV  = WRAP;
};

// Bone palette texture — vertex-shader-only. Bones packed as
// texel(bone*4 + row) = matrix row r as float4(M_r1,M_r2,M_r3,M_r4).
// Pin to s2 to leave s0 free for the diffuse sampler and avoid any chance of
// register collision.
texture BoneTex;
sampler BoneSampler : register(s2) = sampler_state
{
    Texture = <BoneTex>;
    MipFilter = NONE;
    MinFilter = POINT;
    MagFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};
float BoneTexInvWidth;     // 1 / textureWidth — used to convert texel index to UV
float HasDiffuse;          // 0 or 1
float3 FallbackColor;      // used when HasDiffuse == 0

float3 LightDir;           // world-space directional light

struct VS_IN
{
    float4 Position     : POSITION0;
    float3 Normal       : NORMAL0;
    float2 TexCoord     : TEXCOORD0;
    float4 BlendIndices : BLENDINDICES0;
    float4 BlendWeights : BLENDWEIGHT0;
};

struct PS_IN
{
    float4 Position : POSITION0;
    float3 Normal   : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
};

float4x4 LoadBone(float idx)
{
    // idx is in [0, MaxBones); bone occupies 4 contiguous texels starting at idx*4.
    float u0 = (idx * 4.0 + 0.5) * BoneTexInvWidth;
    float du = BoneTexInvWidth;
    float4 r0 = tex2Dlod(BoneSampler, float4(u0,        0, 0, 0));
    float4 r1 = tex2Dlod(BoneSampler, float4(u0 + du,   0, 0, 0));
    float4 r2 = tex2Dlod(BoneSampler, float4(u0 + 2*du, 0, 0, 0));
    float4 r3 = tex2Dlod(BoneSampler, float4(u0 + 3*du, 0, 0, 0));
    return float4x4(r0, r1, r2, r3);
}

PS_IN VS(VS_IN i)
{
    float4x4 m =
        LoadBone(i.BlendIndices.x) * i.BlendWeights.x +
        LoadBone(i.BlendIndices.y) * i.BlendWeights.y +
        LoadBone(i.BlendIndices.z) * i.BlendWeights.z +
        LoadBone(i.BlendIndices.w) * i.BlendWeights.w;

    float4 skinnedPos = mul(i.Position, m);
    float3 skinnedNrm = mul(float4(i.Normal, 0.0), m).xyz;

    PS_IN o;
    o.Position = mul(mul(mul(skinnedPos, World), View), Projection);
    o.Normal   = skinnedNrm;
    o.TexCoord = i.TexCoord;
    return o;
}

float4 PS(PS_IN i) : COLOR0
{
    float ndl = saturate(dot(normalize(i.Normal), normalize(LightDir)));
    float lit = 0.45 + 0.55 * ndl;

    float3 base;
    if (HasDiffuse > 0.5)
    {
        // Texture * factor (FallbackColor doubles as baseColorFactor.rgb).
        float4 t = tex2D(DiffuseSampler, i.TexCoord);
        base = t.rgb * FallbackColor;
    }
    else
    {
        base = FallbackColor;
    }
    return float4(base * lit, 1.0);
}

technique Default
{
    pass P0
    {
        // MIXED SHADER MODELS — empirically required for FNA/MojoShader on
        // DesktopGL with this effect:
        //   * VS at vs_3_0 — needed for tex2Dlod() against the bone-palette
        //     texture, which lifts FNA SkinnedEffect's 72-bone limit.
        //   * PS at ps_2_0 — MojoShader's SM3 → GLSL translation botches
        //     pixel-shader texture sampling, so the diffuse comes back white.
        //     ps_2_0 samples correctly, matching FNA BasicEffect's profile.
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_2_0 PS();
    }
}
