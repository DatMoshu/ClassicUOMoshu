// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — wrapper around our custom texture-based skinning shader
// (TextureSkinning.fxc). Replaces FNA's stock SkinnedEffect to lift the 72-bone
// constant-register cap to MaxBones = 1024 (constrained by texture width).
//
// Bones are packed into a 1xN R32G32B32A32_FLOAT texture, 4 texels per bone
// (one texel per matrix row).

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    public sealed class TextureSkinnedEffect : Effect
    {
        public const int MaxBones = 1024;
        private const int TexelsPerBone = 4;
        private const int TexWidth = MaxBones * TexelsPerBone;

        private readonly EffectParameter _pWorld;
        private readonly EffectParameter _pView;
        private readonly EffectParameter _pProjection;
        private readonly EffectParameter _pBoneTex;
        private readonly EffectParameter _pBoneTexInvWidth;
        private readonly EffectParameter _pDiffuseTex;
        private readonly EffectParameter _pHasDiffuse;
        private readonly EffectParameter _pFallback;
        private readonly EffectParameter _pLightDir;

        private readonly Texture2D _boneTexture;
        // Single persistent diffuse atlas — set as the DiffuseTex parameter
        // ONCE in the constructor. Subsequent submeshes update its CONTENT via
        // SetData (per `SetDiffusePixels`), never replacing the texture
        // reference. This works around an FNA/MojoShader DesktopGL behavior
        // where `EffectParameter.SetValue(otherTexture)` updates the parameter
        // value but does NOT re-bind the underlying GL texture handle, so the
        // shader keeps sampling whatever was bound at construction time.
        // Synty palette atlases are 32×32 — the shared atlas matches that size.
        public const int DiffuseAtlasSize = 32;
        private readonly Texture2D _diffuseAtlas;
        private readonly Color[] _whitePixels;        // 32×32 of Color.White, cached for null/fallback path
        private readonly Vector4[] _boneRows = new Vector4[TexWidth];

        public Matrix World { set => _pWorld.SetValue(value); }
        public Matrix View { set => _pView.SetValue(value); }
        public Matrix Projection { set => _pProjection.SetValue(value); }

        // BACKWARD-COMPAT: existing renderers call DiffuseTexture = sub.Texture.
        // We can't use SetValue (FNA bug), so we GetData → SetData via the
        // shared atlas. PROTOTYPE: round-trips per draw, fine for ≤30 submeshes.
        // For null input, fills atlas with white.
        public Texture2D DiffuseTexture
        {
            set
            {
                if (value == null)
                {
                    _diffuseAtlas.SetData(_whitePixels);
                }
                else if (value.Width == DiffuseAtlasSize && value.Height == DiffuseAtlasSize &&
                         value.Format == SurfaceFormat.Color)
                {
                    var buf = _scratch32;
                    value.GetData(buf);
                    _diffuseAtlas.SetData(buf);
                }
                else
                {
                    // Different size or format — fall back to white so we don't
                    // crash; size-mismatched textures need a separate code path
                    // we don't have yet. PROTOTYPE: log once per session.
                    if (!_warnedSize)
                    {
                        _warnedSize = true;
                        Console.WriteLine($"[3DCUO] TextureSkinnedEffect: ignoring {value.Width}x{value.Height} {value.Format} — atlas is {DiffuseAtlasSize}x{DiffuseAtlasSize} Color");
                    }
                    _diffuseAtlas.SetData(_whitePixels);
                }
                _pHasDiffuse.SetValue(1f);
            }
        }

        // Direct pixel push — avoids the GetData round-trip. Caller must supply
        // exactly DiffuseAtlasSize × DiffuseAtlasSize Colors (length 1024).
        public void SetDiffusePixels(Color[] pixels)
        {
            _diffuseAtlas.SetData(pixels ?? _whitePixels);
            _pHasDiffuse.SetValue(1f);
        }

        private readonly Color[] _scratch32 = new Color[DiffuseAtlasSize * DiffuseAtlasSize];
        private static bool _warnedSize;

        public Vector3 FallbackColor { set => _pFallback.SetValue(value); }
        public Vector3 LightDirection { set => _pLightDir.SetValue(value); }
        // Diagnostic override: bypass the DiffuseTexture setter's auto-1.0 and
        // forcibly write any value to HasDiffuse (e.g. 0 to test the fallback
        // branch, 1 to confirm the texture branch). Used by MaterialDebugGump.
        public void SetHasDiffuseForce(float v) => _pHasDiffuse.SetValue(v);

        public TextureSkinnedEffect(GraphicsDevice gd, byte[] fxc) : base(gd, fxc)
        {
            _pWorld = Parameters["World"];
            _pView = Parameters["View"];
            _pProjection = Parameters["Projection"];
            _pBoneTex = Parameters["BoneTex"];
            _pBoneTexInvWidth = Parameters["BoneTexInvWidth"];
            // Try every plausible name MojoShader might use for the diffuse
            // texture binding. Native fxc.exe + FNA combo has been observed to
            // expose this parameter under the texture name, the sampler name,
            // or even a synthesized "Sampler+Texture" key depending on shader
            // syntax. Log every parameter we see for diagnosis.
            _pDiffuseTex = Parameters["DiffuseTex"]
                          ?? Parameters["DiffuseSampler"]
                          ?? Parameters["DiffuseSampler+Texture"]
                          ?? Parameters["DiffuseSampler+DiffuseTex"];
            if (_pDiffuseTex == null)
            {
                Console.WriteLine("[3DCUO] TextureSkinnedEffect: WARNING — diffuse texture parameter NOT FOUND. All parameters:");
                foreach (var p in Parameters)
                    Console.WriteLine($"[3DCUO]   '{p.Name}' class={p.ParameterClass} type={p.ParameterType}");
            }
            _pHasDiffuse = Parameters["HasDiffuse"];
            _pFallback = Parameters["FallbackColor"];
            _pLightDir = Parameters["LightDir"];

            _boneTexture = new Texture2D(gd, TexWidth, 1, false, SurfaceFormat.Vector4);
            _pBoneTex.SetValue(_boneTexture);
            _pBoneTexInvWidth.SetValue(1f / TexWidth);

            // Allocate the persistent diffuse atlas + cached white pixels.
            // SetValue is called ONCE here. From this point on, only the
            // texture's CONTENT is updated (via SetData), never its identity.
            _diffuseAtlas = new Texture2D(gd, DiffuseAtlasSize, DiffuseAtlasSize, false, SurfaceFormat.Color);
            _whitePixels = new Color[DiffuseAtlasSize * DiffuseAtlasSize];
            for (int i = 0; i < _whitePixels.Length; i++) _whitePixels[i] = Color.White;
            _diffuseAtlas.SetData(_whitePixels);
            _pDiffuseTex?.SetValue(_diffuseAtlas);
            _pHasDiffuse.SetValue(1f);

            _pFallback.SetValue(new Vector3(0.95f, 0.85f, 0.7f));
            _pLightDir.SetValue(new Vector3(0f, 1f, 1f));

            // Register so LightingState can broadcast a single per-frame light
            // direction to every live skinned-effect instance without each
            // renderer needing to know about LightingState individually.
            lock (_allInstancesLock) _allInstances.Add(this);
        }

        // Per-frame fan-out from LightingState.CurrentLightDir(). Effect
        // instances that have been disposed will throw on SetValue; guard.
        private static readonly List<TextureSkinnedEffect> _allInstances = new();
        private static readonly object _allInstancesLock = new();
        public static void BroadcastLightDirection(Vector3 dir)
        {
            lock (_allInstancesLock)
            {
                for (int i = _allInstances.Count - 1; i >= 0; i--)
                {
                    var inst = _allInstances[i];
                    if (inst == null || inst.IsDisposed) { _allInstances.RemoveAt(i); continue; }
                    try { inst._pLightDir.SetValue(dir); }
                    catch { _allInstances.RemoveAt(i); }
                }
            }
        }

        public void SetBoneTransforms(Matrix[] bones)
        {
            int n = System.Math.Min(bones.Length, MaxBones);
            for (int i = 0; i < n; i++)
            {
                ref var m = ref bones[i];
                int o = i * TexelsPerBone;
                _boneRows[o + 0] = new Vector4(m.M11, m.M12, m.M13, m.M14);
                _boneRows[o + 1] = new Vector4(m.M21, m.M22, m.M23, m.M24);
                _boneRows[o + 2] = new Vector4(m.M31, m.M32, m.M33, m.M34);
                _boneRows[o + 3] = new Vector4(m.M41, m.M42, m.M43, m.M44);
            }
            // Identity-pad any unused bone slots so vertices weighted to non-existent
            // bones don't fly to the origin.
            for (int i = n; i < MaxBones; i++)
            {
                int o = i * TexelsPerBone;
                _boneRows[o + 0] = new Vector4(1, 0, 0, 0);
                _boneRows[o + 1] = new Vector4(0, 1, 0, 0);
                _boneRows[o + 2] = new Vector4(0, 0, 1, 0);
                _boneRows[o + 3] = new Vector4(0, 0, 0, 1);
            }
            _boneTexture.SetData(_boneRows);
        }

        public static TextureSkinnedEffect LoadFromFile(GraphicsDevice gd, string fxcPath)
        {
            var bytes = File.ReadAllBytes(fxcPath);
            return new TextureSkinnedEffect(gd, bytes);
        }
    }
}
