// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — wrapper for GroundOverlay.fx (per-pixel wet & snow ground
// overlay). Loads the compiled .fxc + three noise textures from the build
// output's Shaders/ and Textures/Ground/ folders.
//
// Two techniques: WetTechnique, SnowTechnique. Caller selects the technique
// then calls a Draw method on LandMesh3D — the shader does triplanar-style
// per-pixel slope masking from worldspace ddx/ddy, so no normals are needed
// in the vertex format (LandMesh3D ships VertexPosition only for overlay).

using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    public sealed class GroundOverlayEffect : Effect
    {
        private readonly EffectParameter _pWorld;
        private readonly EffectParameter _pView;
        private readonly EffectParameter _pProjection;
        private readonly EffectParameter _pNoiseTex;
        private readonly EffectParameter _pPuddleTex;
        private readonly EffectParameter _pFlakeTex;
        private readonly EffectParameter _pTime;
        private readonly EffectParameter _pIntensity;
        private readonly EffectParameter _pNoiseScale;
        private readonly EffectParameter _pPuddleScale;
        private readonly EffectParameter _pFlakeScale;
        private readonly EffectParameter _pPuddleThresh;
        private readonly EffectParameter _pHeightBias;
        private readonly EffectParameter _pBaseAlphaTex;

        private readonly EffectTechnique _wetTechnique;
        private readonly EffectTechnique _snowTechnique;
        private readonly EffectTechnique _wetFoliageTechnique;
        private readonly EffectTechnique _snowFoliageTechnique;
        private readonly EffectTechnique _fallFoliageTechnique;
        private readonly EffectParameter _pFallTreeUnit;
        private readonly EffectParameter _pFallSaturation;
        private readonly EffectParameter _pSnowDetile;
        private readonly EffectParameter _pSnowCellSize;
        private readonly EffectParameter _pPuddleAmount;

        // Tunables — exposed as static so debug UI can scrub them live.
        public static float NoiseScale   = 1f / 256f;   // ~12 tiles per noise repeat
        public static float PuddleScale  = 1f / 360f;   // larger cells for puddles
        public static float FlakeScale   = 1f / 90f;    // finer flake detail
        public static float PuddleThresh = 0.55f;       // raise → fewer puddles
        public static float HeightBias   = 0.6f;        // small world-Y lift to defeat z-fighting
        public static float FallTreeUnit = 22f;          // worldspace size of one tree color cell
        public static float FallSaturation = 0.85f;      // 0..1 — how strongly autumn replaces base hue
        // De-tile randomization for the snow ground pattern. 0 = legacy
        // straight tile (visible repeats when zoomed out), 1 = full per-cell
        // rotation+offset jitter (default — eliminates the visible period).
        public static float SnowDetile   = 1f;
        public static float SnowCellSize = 220f;          // world units per macro-cell (~10 UO tiles)
        // Master "puddle amount" multiplier on PS_Wet. 0 leaves only damp
        // tinting, 1 shows full puddles (default).
        public static float PuddleAmount = 1f;

        public Matrix World      { set => _pWorld.SetValue(value); }
        public Matrix View       { set => _pView.SetValue(value); }
        public Matrix Projection { set => _pProjection.SetValue(value); }
        public float  Time       { set => _pTime.SetValue(value); }
        public float  Intensity  { set => _pIntensity.SetValue(value); }

        public void UseWet()  => CurrentTechnique = _wetTechnique;
        public void UseSnow() => CurrentTechnique = _snowTechnique;
        public void UseWetFoliage()  => CurrentTechnique = _wetFoliageTechnique;
        public void UseSnowFoliage() => CurrentTechnique = _snowFoliageTechnique;
        public void UseFallFoliage() => CurrentTechnique = _fallFoliageTechnique;

        // Bind the base atlas (leaf/trunk texture) so the foliage techniques
        // can sample its alpha for the cutout mask. Caller must set this before
        // every per-texture overlay draw.
        public Texture2D BaseAlphaTex { set => _pBaseAlphaTex?.SetValue(value); }

        public GroundOverlayEffect(GraphicsDevice gd, byte[] fxc,
            Texture2D noiseTex, Texture2D puddleTex, Texture2D flakeTex)
            : base(gd, fxc)
        {
            _pWorld        = Parameters["World"];
            _pView         = Parameters["View"];
            _pProjection   = Parameters["Projection"];
            _pNoiseTex     = Parameters["NoiseTex"];
            _pPuddleTex    = Parameters["PuddleTex"];
            _pFlakeTex     = Parameters["FlakeTex"];
            _pTime         = Parameters["Time"];
            _pIntensity    = Parameters["Intensity"];
            _pNoiseScale   = Parameters["NoiseScale"];
            _pPuddleScale  = Parameters["PuddleScale"];
            _pFlakeScale   = Parameters["FlakeScale"];
            _pPuddleThresh = Parameters["PuddleThresh"];
            _pHeightBias   = Parameters["HeightBias"];
            _pBaseAlphaTex = Parameters["BaseAlphaTex"];
            _pFallTreeUnit = Parameters["FallTreeUnit"];
            _pFallSaturation = Parameters["FallSaturation"];
            _pSnowDetile   = Parameters["SnowDetile"];
            _pSnowCellSize = Parameters["SnowCellSize"];
            _pPuddleAmount = Parameters["PuddleAmount"];

            _pNoiseTex?.SetValue(noiseTex);
            _pPuddleTex?.SetValue(puddleTex);
            _pFlakeTex?.SetValue(flakeTex);
            ApplyTunables();

            _wetTechnique         = Techniques["WetTechnique"];
            _snowTechnique        = Techniques["SnowTechnique"];
            _wetFoliageTechnique  = Techniques["WetFoliageTechnique"];
            _snowFoliageTechnique = Techniques["SnowFoliageTechnique"];
            _fallFoliageTechnique = Techniques["FallFoliageTechnique"];
            CurrentTechnique = _snowTechnique; // default
        }

        // Re-push the static tunables — call before each draw if they may have
        // changed (debug sliders).
        public void ApplyTunables()
        {
            _pNoiseScale?.SetValue(NoiseScale);
            _pPuddleScale?.SetValue(PuddleScale);
            _pFlakeScale?.SetValue(FlakeScale);
            _pPuddleThresh?.SetValue(PuddleThresh);
            _pHeightBias?.SetValue(HeightBias);
            _pFallTreeUnit?.SetValue(FallTreeUnit);
            _pFallSaturation?.SetValue(FallSaturation);
            _pSnowDetile?.SetValue(SnowDetile);
            _pSnowCellSize?.SetValue(SnowCellSize);
            _pPuddleAmount?.SetValue(PuddleAmount);
        }

        public static GroundOverlayEffect TryLoad(GraphicsDevice gd)
        {
            try
            {
                string fxcPath    = Path.Combine(AppContext.BaseDirectory, "Shaders", "GroundOverlay.fxc");
                string noisePath  = Path.Combine(AppContext.BaseDirectory, "Textures", "Ground", "ground_perlin.png");
                string puddlePath = Path.Combine(AppContext.BaseDirectory, "Textures", "Ground", "ground_cellular.png");
                string flakePath  = Path.Combine(AppContext.BaseDirectory, "Textures", "Ground", "ground_flakes.png");
                if (!File.Exists(fxcPath))    { Console.WriteLine($"[3DCUO][GroundOverlay] missing {fxcPath}");    return null; }
                if (!File.Exists(noisePath))  { Console.WriteLine($"[3DCUO][GroundOverlay] missing {noisePath}");  return null; }
                if (!File.Exists(puddlePath)) { Console.WriteLine($"[3DCUO][GroundOverlay] missing {puddlePath}"); return null; }
                if (!File.Exists(flakePath))  { Console.WriteLine($"[3DCUO][GroundOverlay] missing {flakePath}");  return null; }

                Texture2D noise  = LoadPng(gd, noisePath);
                Texture2D puddle = LoadPng(gd, puddlePath);
                Texture2D flake  = LoadPng(gd, flakePath);
                byte[] fxc = File.ReadAllBytes(fxcPath);
                Console.WriteLine("[3DCUO][GroundOverlay] effect + textures loaded.");
                return new GroundOverlayEffect(gd, fxc, noise, puddle, flake);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO][GroundOverlay] load failed: {ex.Message}");
                return null;
            }
        }

        private static Texture2D LoadPng(GraphicsDevice gd, string path)
        {
            using var fs = File.OpenRead(path);
            return Texture2D.FromStream(gd, fs);
        }
    }
}
