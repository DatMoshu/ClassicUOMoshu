// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using System;
using ClassicUO.Renderer.Renderer3D; // legacy SnowflakeTextureFactory / RainStreakTextureFactory
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Production implementation of <see cref="IParticleService"/>. Owns the SoA spawn
    /// pool, the spawn API, the lifecycle sweep, and the 4-pass batched billboard
    /// renderer. Implements <see cref="IFrameService"/> (per-frame Tick driven by
    /// <c>Renderer3DServices.Tick</c>) and <see cref="IDisposable"/> (GPU resources
    /// release on shutdown).
    /// </summary>
    /// <remarks>
    /// Allocation-free in steady state. The SoA arrays, vertex/index buffers, and CPU
    /// vertex scratch are all preallocated. Tick + Draw + EmitBatch are zero-alloc in
    /// the hot path — see <c>ParticleServiceTickTests.Tick_SteadyState_IsAllocationFree</c>
    /// for the regression assertion (playbook §E).
    /// </remarks>
    public sealed class ParticleService : IParticleService, IFrameService, IDisposable
    {
        /// <summary>Hard cap on simultaneously-alive particles. Mirrored by <see cref="Particle3DSystem.MaxParticles"/>.</summary>
        public const int MaxParticles = 8192;

        // ===== Flag bits — sync with Particle3DSystem.F_* (which are exposed publicly) =====
        internal const byte F_ALIVE        = 0x01;
        internal const byte F_PINNED       = 0x02;
        internal const byte F_TRAIL        = 0x04;
        internal const byte F_TEXTURED     = 0x08;
        internal const byte F_SPIN         = 0x10;
        internal const byte F_TEXTURED_ADD = 0x20;
        internal const byte F_STREAK       = 0x40;
        internal const byte F_FLASH        = 0x80;

        // ===== SoA storage — internal accessors expose the array refs to the legacy
        //       Tick + Draw paths without forcing a per-cell virtual dispatch =====
        internal readonly Vector3[] PosArr      = new Vector3[MaxParticles];
        internal readonly Vector3[] VelArr      = new Vector3[MaxParticles];
        internal readonly Vector3[] AccelArr    = new Vector3[MaxParticles];
        internal readonly float[]   LifeArr     = new float  [MaxParticles];
        internal readonly float[]   MaxLifeArr  = new float  [MaxParticles];
        internal readonly float[]   SizeArr     = new float  [MaxParticles];
        internal readonly float[]   SizeEndArr  = new float  [MaxParticles];
        internal readonly Color[]   ColStartArr = new Color  [MaxParticles];
        internal readonly Color[]   ColEndArr   = new Color  [MaxParticles];
        internal readonly byte[]    FlagsArr    = new byte   [MaxParticles];

        // ===== Counters — internal so the legacy Tick can mutate _highWater after its
        //       lifecycle sweep. Will become fully encapsulated once Tick migrates. =====
        internal int AliveCount;
        internal int HighWaterMutable;
        internal int FreeHint;

        // ===== Diagnostics =====
        internal int LastDrawnParticlesMutable;

        // ===== GPU resources (session 78 — lazily allocated on first Draw) =====
        // Migrated from Particle3DSystem private statics.
        private DynamicVertexBuffer _vb;
        private IndexBuffer _ib;
        private VertexPositionColorTexture[] _cpuVerts;
        private BasicEffect _effect;
        private bool _resourcesReady;

        // ===== Textures (session 78 — owned by service; legacy facade properties delegate) =====
        // External writers (Weather3DSystem, NukeShow, etc.) set these via the facade.
        internal Texture2D ParticleTexture;       // F_TEXTURED — alpha blend (e.g. snowflakes)
        internal Texture2D ParticleRainTexture;   // F_TEXTURED_ADD — additive (rain streaks)
        internal Texture2D ParticleFlashTexture;  // F_FLASH — additive (lightning bursts)
        internal float ParticleStreakAspect = 6f; // streak quads = up * aspect

        // ===== Runtime toggles =====
        private bool _enabled;
        private bool _verboseLog;
        private bool _disposed;

        public ParticleService(ParticleServiceConfig config)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));
            _enabled = config.InitialEnabled;
            _verboseLog = config.InitialVerboseLog;
        }

        // ===== IParticleService =====

        public bool Enabled => _enabled;
        public bool VerboseLog => _verboseLog;
        public void SetEnabled(bool enabled) => _enabled = enabled;
        public void SetVerboseLog(bool verbose) => _verboseLog = verbose;

        public int AliveParticles => AliveCount;
        public int LastDrawnParticles => LastDrawnParticlesMutable;
        public int HighWater => HighWaterMutable;
        int IParticleService.MaxParticles => MaxParticles;

        /// <summary>
        /// Drop every alive particle. Mirrors the legacy <c>Particle3DSystem.Clear</c>
        /// semantics byte-for-byte (8192-element flag zero + counter reset). Allocation-free.
        /// </summary>
        public void Clear()
        {
            // Only walk the high-water slice — most frames have <500 alive, and the loop
            // tail past _highWater holds last-frame's zeroed flags already.
            int hw = HighWaterMutable;
            for (int i = 0; i < hw; i++) FlagsArr[i] = 0;
            AliveCount = 0;
            HighWaterMutable = 0;
            FreeHint = 0;
        }

        // ===== Tick — moved from Particle3DSystem.Tick verbatim (session 77) =====
        // The cross-cutting calls that used to live here (WindManager.Tick,
        // Weather3DSystem.Update, LeafFallSystem.Tick) are intentionally OMITTED.
        // WindManager / LeafFallSystem are no-op facades already (their services tick
        // themselves via Renderer3DServices.Tick). Weather3DSystem.Update still has a
        // real body and is driven by the legacy Particle3DSystem.Tick facade until
        // weather sim migrates.

        public void Tick(in FrameTickContext ctx)
        {
            if (!_enabled) return;

            float dt = ctx.DeltaSeconds;
            double totalSeconds = ctx.TotalSeconds;

            int newHigh = 0;
            for (int i = 0; i < HighWaterMutable; i++)
            {
                if ((FlagsArr[i] & F_ALIVE) == 0) continue;

                LifeArr[i] += dt;
                if (LifeArr[i] >= MaxLifeArr[i])
                {
                    FlagsArr[i] = 0;
                    AliveCount--;
                    continue;
                }

                const byte F_PINNED = 0x02;
                const byte F_TRAIL = 0x04;

                if ((FlagsArr[i] & F_PINNED) == 0)
                {
                    VelArr[i] += AccelArr[i] * dt;
                    PosArr[i] += VelArr[i] * dt;
                }

                if ((FlagsArr[i] & F_TRAIL) != 0)
                {
                    // Spawn one trail sub-particle every other tick — cheap sparkle.
                    // Uses ctx.TotalSeconds in place of the legacy Stopwatch-derived
                    // _lastSeconds so the cadence is FrameClock-driven.
                    if ((i & 1) == ((int)(totalSeconds * 60) & 1))
                    {
                        Spawn(
                            PosArr[i],
                            VelArr[i] * -0.05f,
                            new Vector3(0f, -40f, 0f),
                            0.5f, 4f, 0f,
                            ColStartArr[i],
                            new Color(ColEndArr[i].R, ColEndArr[i].G, ColEndArr[i].B, (byte)0),
                            flags: 0);
                    }
                }

                newHigh = i + 1;
            }
            HighWaterMutable = newHigh;
        }

        // ===== Draw — moved from Particle3DSystem.Draw verbatim (session 78) =====
        // 4-pass batched billboard renderer. Owns the DynamicVertexBuffer +
        // IndexBuffer + BasicEffect; CPU vertex scratch is preallocated once.

        private enum ParticlePass { UntexturedAdditive, TexturedAlpha, TexturedAdditive, TexturedFlash }

        /// <summary>
        /// Draw all alive particles in 4 batched passes. Called from <c>GameScene</c>
        /// after the heightmap/multi/static/player passes so particles overlay the
        /// scene without being occluded by transparent foliage.
        /// </summary>
        public void Draw(GraphicsDevice gd, Matrix view, Matrix proj)
        {
            if (!_enabled || AliveCount == 0 || gd is null) return;

            EnsureResources(gd);

            // Camera-facing billboard basis. Inverse of the view matrix's
            // upper-left 3x3 gives world-space right (X) and up (Y).
            Matrix invView = Matrix.Invert(view);
            Vector3 right = new Vector3(invView.M11, invView.M12, invView.M13);
            Vector3 up    = new Vector3(invView.M21, invView.M22, invView.M23);
            if (right.LengthSquared() < 0.01f)
            {
                // Iso projection has Identity view; fall back to UO-iso aligned axes.
                right = new Vector3(0.7071f, 0f, -0.7071f);
                up = Vector3.UnitY;
            }
            else
            {
                right.Normalize();
                up.Normalize();
            }

            var prevDepth   = gd.DepthStencilState;
            var prevBlend   = gd.BlendState;
            var prevRaster  = gd.RasterizerState;
            var prevSampler = gd.SamplerStates[0];

            gd.DepthStencilState = DepthStencilState.DepthRead; // read but don't write
            gd.RasterizerState   = RasterizerState.CullNone;
            gd.SamplerStates[0]  = SamplerState.LinearClamp;

            _effect.World = Matrix.Identity;
            _effect.View = view;
            _effect.Projection = proj;
            _effect.VertexColorEnabled = true;
            _effect.LightingEnabled = false;
            _effect.FogEnabled = false;
            _effect.Alpha = 1f;

            // Pass 1: untextured additive (sand, embers, fog, fireworks…).
            int nUntex = EmitBatch(right, up, ParticlePass.UntexturedAdditive);
            if (nUntex > 0)
            {
                gd.BlendState = BlendState.Additive;
                _effect.TextureEnabled = false;
                _vb.SetData(_cpuVerts, 0, nUntex * 4, SetDataOptions.Discard);
                gd.SetVertexBuffer(_vb);
                gd.Indices = _ib;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, nUntex * 4, 0, nUntex * 2);
                }
            }

            // Pass 2: textured alpha-blend (snowflakes, etc).
            int nTex = (ParticleTexture != null) ? EmitBatch(right, up, ParticlePass.TexturedAlpha) : 0;
            if (nTex > 0)
            {
                gd.BlendState = BlendState.NonPremultiplied;
                _effect.TextureEnabled = true;
                _effect.Texture = ParticleTexture;
                _vb.SetData(_cpuVerts, 0, nTex * 4, SetDataOptions.Discard);
                gd.SetVertexBuffer(_vb);
                gd.Indices = _ib;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, nTex * 4, 0, nTex * 2);
                }
                _effect.Texture = null;
                _effect.TextureEnabled = false;
            }

            // Pass 3: textured additive (rain streaks).
            int nRain = (ParticleRainTexture != null) ? EmitBatch(right, up, ParticlePass.TexturedAdditive) : 0;
            if (nRain > 0)
            {
                gd.BlendState = BlendState.Additive;
                _effect.TextureEnabled = true;
                _effect.Texture = ParticleRainTexture;
                _vb.SetData(_cpuVerts, 0, nRain * 4, SetDataOptions.Discard);
                gd.SetVertexBuffer(_vb);
                gd.Indices = _ib;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, nRain * 4, 0, nRain * 2);
                }
                _effect.Texture = null;
                _effect.TextureEnabled = false;
            }

            // Pass 4: textured additive FLASH (lightning ground / sky burst).
            // Independent slot so it doesn't fight rain in Storm mode.
            int nFlash = (ParticleFlashTexture != null) ? EmitBatch(right, up, ParticlePass.TexturedFlash) : 0;
            if (nFlash > 0)
            {
                gd.BlendState = BlendState.Additive;
                _effect.TextureEnabled = true;
                _effect.Texture = ParticleFlashTexture;
                _vb.SetData(_cpuVerts, 0, nFlash * 4, SetDataOptions.Discard);
                gd.SetVertexBuffer(_vb);
                gd.Indices = _ib;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, nFlash * 4, 0, nFlash * 2);
                }
                _effect.Texture = null;
                _effect.TextureEnabled = false;
            }

            LastDrawnParticlesMutable = nUntex + nTex + nRain + nFlash;

            gd.SetVertexBuffer(null);
            gd.DepthStencilState = prevDepth;
            gd.BlendState        = prevBlend;
            gd.RasterizerState   = prevRaster;
            gd.SamplerStates[0]  = prevSampler;

            if (_verboseLog)
            {
                Console.WriteLine($"[Particle3D] alive={AliveCount} drawn={nUntex + nTex} (untex={nUntex} tex={nTex}) hw={HighWaterMutable}");
            }
        }

        // Emit quads for the requested pass into _cpuVerts. Returns the quad
        // count written to the buffer.
        private int EmitBatch(Vector3 right, Vector3 up, ParticlePass pass)
        {
            int n = 0;
            for (int i = 0; i < HighWaterMutable; i++)
            {
                if ((FlagsArr[i] & F_ALIVE) == 0) continue;
                byte f = FlagsArr[i];
                ParticlePass myPass = ((f & F_FLASH) != 0)        ? ParticlePass.TexturedFlash
                                    : ((f & F_TEXTURED) != 0)     ? ParticlePass.TexturedAlpha
                                    : ((f & F_TEXTURED_ADD) != 0) ? ParticlePass.TexturedAdditive
                                                                   : ParticlePass.UntexturedAdditive;
                if (myPass != pass) continue;

                float t = LifeArr[i] / Math.Max(0.0001f, MaxLifeArr[i]);
                if (t > 1f) t = 1f;
                float sz = MathHelper.Lerp(SizeArr[i], SizeEndArr[i], t) * 0.5f;
                Color c = Color.Lerp(ColStartArr[i], ColEndArr[i], t);

                Vector3 p = PosArr[i];
                Vector3 r = right * sz;
                Vector3 u = up * sz * ((f & F_STREAK) != 0 ? ParticleStreakAspect : 1f);

                // Streak particles (rain): long axis is WORLD-DOWN so the streak doesn't
                // shift with the camera. Width axis perpendicular to view so the ribbon
                // still faces the camera.
                if ((f & F_STREAK) != 0)
                {
                    Vector3 worldUp = Vector3.UnitY;
                    Vector3 fwd = Vector3.Cross(right, up); // camera forward
                    if (fwd.LengthSquared() < 1e-6f) fwd = -Vector3.UnitZ;
                    else fwd.Normalize();
                    Vector3 rWorld = Vector3.Cross(worldUp, fwd);
                    if (rWorld.LengthSquared() < 1e-6f) rWorld = right;
                    else rWorld.Normalize();
                    float halfLen = sz * ParticleStreakAspect;
                    r = rWorld * sz;
                    u = -worldUp * halfLen;
                }

                // Cheap per-particle yaw spin around the camera-forward axis. Stable
                // per-slot phase from the slot index so flakes don't all spin in lockstep.
                if ((FlagsArr[i] & F_SPIN) != 0)
                {
                    float ang = LifeArr[i] * 1.6f + (i * 0.137f);
                    float cs = MathF.Cos(ang);
                    float sn = MathF.Sin(ang);
                    Vector3 r2 = r * cs + u * sn;
                    Vector3 u2 = -r * sn + u * cs;
                    r = r2; u = u2;
                }

                int vi = n * 4;
                _cpuVerts[vi + 0].Position = p - r - u;
                _cpuVerts[vi + 1].Position = p + r - u;
                _cpuVerts[vi + 2].Position = p + r + u;
                _cpuVerts[vi + 3].Position = p - r + u;
                _cpuVerts[vi + 0].Color = c;
                _cpuVerts[vi + 1].Color = c;
                _cpuVerts[vi + 2].Color = c;
                _cpuVerts[vi + 3].Color = c;
                _cpuVerts[vi + 0].TextureCoordinate = new Vector2(0, 0);
                _cpuVerts[vi + 1].TextureCoordinate = new Vector2(1, 0);
                _cpuVerts[vi + 2].TextureCoordinate = new Vector2(1, 1);
                _cpuVerts[vi + 3].TextureCoordinate = new Vector2(0, 1);

                n++;
                if (n >= MaxParticles) break;
            }
            return n;
        }

        private void EnsureResources(GraphicsDevice gd)
        {
            if (_resourcesReady) return;

            _cpuVerts = new VertexPositionColorTexture[MaxParticles * 4];

            _vb = new DynamicVertexBuffer(
                gd,
                VertexPositionColorTexture.VertexDeclaration,
                MaxParticles * 4,
                BufferUsage.WriteOnly);

            // 8192 particles × 4 verts = 32 768 — exceeds 16-bit short range by one;
            // use 32-bit indices to be safe and to allow growth.
            var idx = new int[MaxParticles * 6];
            for (int i = 0; i < MaxParticles; i++)
            {
                int v = i * 4;
                int o = i * 6;
                idx[o + 0] = v + 0; idx[o + 1] = v + 1; idx[o + 2] = v + 2;
                idx[o + 3] = v + 0; idx[o + 4] = v + 2; idx[o + 5] = v + 3;
            }
            _ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, MaxParticles * 6, BufferUsage.WriteOnly);
            _ib.SetData(idx);

            _effect = new BasicEffect(gd);

            // Default textured-particle textures (procedural). Other systems can overwrite
            // either before drawing if they want a different sprite.
            ParticleTexture     ??= SnowflakeTextureFactory.Get(gd);
            ParticleRainTexture ??= RainStreakTextureFactory.Get(gd);

            _resourcesReady = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _vb?.Dispose();
            _ib?.Dispose();
            _effect?.Dispose();
            _vb = null;
            _ib = null;
            _effect = null;
            _cpuVerts = null;
            _resourcesReady = false;
        }

        // ===== Spawn — moved from Particle3DSystem.Spawn verbatim =====

        /// <summary>
        /// Spawn one particle. Returns the allocated slot index, or -1 when the pool is
        /// full. Allocation-free; just dirties the SoA arrays by index and increments
        /// the alive counter. Caller-supplied <paramref name="flags"/> are OR'd with
        /// <see cref="F_ALIVE"/> before being stored.
        /// </summary>
        internal int Spawn(
            Vector3 pos, Vector3 vel, Vector3 accel,
            float life, float size, float sizeEnd,
            Color colorStart, Color colorEnd, byte flags)
        {
            int start = FreeHint;
            for (int n = 0; n < MaxParticles; n++)
            {
                int i = (start + n) % MaxParticles;
                if ((FlagsArr[i] & F_ALIVE) == 0)
                {
                    PosArr[i] = pos;
                    VelArr[i] = vel;
                    AccelArr[i] = accel;
                    LifeArr[i] = 0f;
                    MaxLifeArr[i] = life;
                    SizeArr[i] = size;
                    SizeEndArr[i] = sizeEnd;
                    ColStartArr[i] = colorStart;
                    ColEndArr[i] = colorEnd;
                    FlagsArr[i] = (byte)(flags | F_ALIVE);
                    AliveCount++;
                    if (i + 1 > HighWaterMutable) HighWaterMutable = i + 1;
                    FreeHint = (i + 1) % MaxParticles;
                    return i;
                }
            }
            return -1;
        }
    }
}
