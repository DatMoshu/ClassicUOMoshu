// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO PROTOTYPE — material/sampler debug overrides used by both
// Player3DRenderer and AttachmentRenderer to isolate the "white meshes"
// bug. Toggle these at runtime via MaterialDebugGump.

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    public enum SamplerKind
    {
        PointWrap = 0,
        LinearWrap = 1,
        PointClamp = 2,
        LinearClamp = 3,
        AnisotropicWrap = 4,
    }

    public enum SamplerSlotMode
    {
        Slot0 = 0,
        Slot1 = 1,
        BothSlots = 2,
        None = 3, // do not touch sampler state at all (let pass.Apply control it)
    }

    public enum TestPatternKind
    {
        None = 0,
        SolidRed = 1,
        SolidGreen = 2,
        SolidBlue = 3,
        Checker4x4 = 4,
        UVGradient = 5,
    }

    public static class MaterialDebug
    {
        // When != None, every submesh draws with a procedurally-generated test
        // texture instead of its loaded PNG. If colors appear, binding works
        // and the loaded PNGs are the culprit. If still white, sampler/binding
        // is the culprit.
        public static TestPatternKind TestPattern = TestPatternKind.None;

        // Which pixel-shader sampler slot(s) to write our SamplerKind into.
        public static SamplerSlotMode SlotMode = SamplerSlotMode.Slot0;

        // Filter to apply at the chosen slot(s).
        public static SamplerKind Sampler = SamplerKind.PointWrap;

        // Force HasDiffuse uniform value for ablation testing. -1 = leave alone.
        public static int ForceHasDiffuse = -1;

        // Multiplier applied to baseColorFactor at draw time. Default 1.0 = no
        // change. Push to 5.0 to amplify dim sampling; push to 0.0 to confirm
        // the lighting term alone.
        public static float FactorBoost = 1.0f;

        // Force-bind the chosen submesh texture (or test pattern) directly to
        // GraphicsDevice.Textures[N] AFTER pass.Apply(). Bypasses the
        // EffectParameter / sampler-register pipeline entirely. -1 = off.
        // Cycle 0..3 to find which slot the shader actually samples from.
        public static int ForceTextureSlot = -1;

        // When true, each submesh gets a deterministic random color via
        // FallbackColor + HasDiffuse=0 (no texture sampling). If the rendered
        // mesh shows different colors per piece, the FallbackColor pipeline
        // works and the bug is isolated to tex2D sampling.
        public static bool RandomPerSubmesh = false;

        // When true, AttachmentRenderer paints a HUD-overlay quad showing the
        // first loaded attachment texture as a 256x256 sprite. Uses SpriteBatch,
        // which is a completely separate texture-binding path from our skinning
        // shader. If the quad shows real palette content, the texture is on the
        // GPU; if it's white, the upload itself failed.
        public static bool ShowTextureHudQuad = false;

        // Deterministic color from a string hash. Same input → same color across
        // frames so the user can visually identify which piece is which.
        public static Vector3 ColorFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return Vector3.One;
            int h = 0;
            unchecked { foreach (var c in name) h = h * 31 + c; }
            // Map to 3 bright channels in [0.3, 1.0] so nothing's too dark to see.
            float r = 0.3f + ((h         & 0xFF) / 255f) * 0.7f;
            float g = 0.3f + ((h >>  8   & 0xFF) / 255f) * 0.7f;
            float b = 0.3f + ((h >> 16   & 0xFF) / 255f) * 0.7f;
            return new Vector3(r, g, b);
        }

        private static Texture2D _solidRed, _solidGreen, _solidBlue, _checker, _uvGradient;

        public static Texture2D GetTestPattern(GraphicsDevice gd)
        {
            switch (TestPattern)
            {
                case TestPatternKind.SolidRed:    return _solidRed    ??= MakeSolid(gd, new Color(255, 32, 32));
                case TestPatternKind.SolidGreen:  return _solidGreen  ??= MakeSolid(gd, new Color(32, 255, 32));
                case TestPatternKind.SolidBlue:   return _solidBlue   ??= MakeSolid(gd, new Color(32, 32, 255));
                case TestPatternKind.Checker4x4:  return _checker     ??= MakeChecker(gd);
                case TestPatternKind.UVGradient:  return _uvGradient  ??= MakeUVGradient(gd);
                default:                          return null;
            }
        }

        private static Texture2D MakeSolid(GraphicsDevice gd, Color c)
        {
            var t = new Texture2D(gd, 4, 4, false, SurfaceFormat.Color);
            var data = new Color[16];
            for (int i = 0; i < 16; i++) data[i] = c;
            t.SetData(data);
            return t;
        }

        private static Texture2D MakeChecker(GraphicsDevice gd)
        {
            var t = new Texture2D(gd, 4, 4, false, SurfaceFormat.Color);
            var data = new Color[16];
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    data[y * 4 + x] = ((x + y) & 1) == 0 ? Color.Magenta : Color.Yellow;
            t.SetData(data);
            return t;
        }

        private static Texture2D MakeUVGradient(GraphicsDevice gd)
        {
            const int N = 32;
            var t = new Texture2D(gd, N, N, false, SurfaceFormat.Color);
            var data = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    data[y * N + x] = new Color((byte)(x * 255 / (N - 1)), (byte)(y * 255 / (N - 1)), 0);
            t.SetData(data);
            return t;
        }

        public static SamplerState GetSamplerState() => Sampler switch
        {
            SamplerKind.PointWrap        => SamplerState.PointWrap,
            SamplerKind.LinearWrap       => SamplerState.LinearWrap,
            SamplerKind.PointClamp       => SamplerState.PointClamp,
            SamplerKind.LinearClamp      => SamplerState.LinearClamp,
            SamplerKind.AnisotropicWrap  => SamplerState.AnisotropicWrap,
            _                            => SamplerState.PointWrap,
        };

        // Apply the configured sampler state to GraphicsDevice. Call AFTER
        // pass.Apply() and BEFORE DrawIndexedPrimitives.
        public static void ApplySamplerState(GraphicsDevice gd)
        {
            if (SlotMode == SamplerSlotMode.None) return;
            var state = GetSamplerState();
            switch (SlotMode)
            {
                case SamplerSlotMode.Slot0:
                    gd.SamplerStates[0] = state;
                    break;
                case SamplerSlotMode.Slot1:
                    gd.SamplerStates[1] = state;
                    break;
                case SamplerSlotMode.BothSlots:
                    gd.SamplerStates[0] = state;
                    gd.SamplerStates[1] = state;
                    break;
            }
        }

        // Force-bind the texture to a specific GraphicsDevice.Textures[] slot
        // AFTER pass.Apply(). Used to discover which slot the shader actually
        // samples from (bypasses MojoShader register translation guesswork).
        public static void ForceBindTexture(GraphicsDevice gd, Texture2D tex)
        {
            if (ForceTextureSlot < 0 || tex == null) return;
            int s = ForceTextureSlot;
            if (s < 0 || s > 15) return;
            gd.Textures[s] = tex;
            gd.SamplerStates[s] = GetSamplerState();
        }

        // Choose the texture a draw call should use for a given submesh.
        // Honors TestPattern; falls back to the submesh's own texture.
        public static Texture2D ChooseTexture(GraphicsDevice gd, Texture2D submeshTexture)
        {
            if (TestPattern != TestPatternKind.None)
            {
                var t = GetTestPattern(gd);
                if (t != null) return t;
            }
            return submeshTexture;
        }

        // Read first texture's pixel data and log corner + center colors.
        // Used by the gump's "Sample Pixels" button.
        public static string SampleTexturePixels(Texture2D tex)
        {
            if (tex == null) return "(null texture)";
            try
            {
                int w = tex.Width, h = tex.Height;
                if (tex.Format != SurfaceFormat.Color)
                    return $"unsupported format {tex.Format} ({w}x{h})";
                var data = new Color[w * h];
                tex.GetData(data);
                Color tl = data[0];
                Color tr = data[w - 1];
                Color bl = data[(h - 1) * w];
                Color br = data[(h - 1) * w + w - 1];
                Color cc = data[(h / 2) * w + (w / 2)];
                return $"{w}x{h} TL=({tl.R},{tl.G},{tl.B},{tl.A}) TR=({tr.R},{tr.G},{tr.B},{tr.A}) " +
                       $"BL=({bl.R},{bl.G},{bl.B},{bl.A}) BR=({br.R},{br.G},{br.B},{br.A}) C=({cc.R},{cc.G},{cc.B},{cc.A})";
            }
            catch (Exception ex)
            {
                return $"GetData failed: {ex.GetType().Name}: {ex.Message}";
            }
        }
    }
}
