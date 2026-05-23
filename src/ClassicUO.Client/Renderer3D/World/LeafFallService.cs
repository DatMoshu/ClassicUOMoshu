// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).
//
// Falling-leaf particle simulation + render. Subscribes to two events:
//   * WindUpdatedEvent — caches the latest VectorXZ for per-particle horizontal drift.
//   * SeasonChangedEvent — diagnostic only (logs last observed transition).
//
// Pulls continuous YearPhase from ISeasonService each tick because spawn rate changes
// continuously through the year, not just on coarse-season transitions.

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.World
{
    /// <summary>
    /// Production implementation of <see cref="ILeafFallService"/>. Pool-based; allocates
    /// at construction and never per-frame in the steady state.
    /// </summary>
    public sealed class LeafFallService : ILeafFallService, IFrameService, IDisposable
    {
        // Dependencies
        private readonly LeafFallServiceConfig _config;
        private readonly IRendererEventBus _bus;
        private readonly ISeasonService _season;
        private readonly ILeafSpawnSource _spawnSource;
        private readonly ILeafTextureProvider _textureProvider;
        private readonly Random _rng;
        private readonly IDisposable _windSubscription;
        private readonly IDisposable _seasonSubscription;

        // Particle pool — fixed-size arrays, never resized.
        private readonly Vector3[] _pos;
        private readonly Vector3[] _vel;
        private readonly float[] _life;
        private readonly float[] _maxLife;
        private readonly float[] _size;
        private readonly float[] _spin0;
        private readonly float[] _spinR;
        private readonly Color[] _color;
        private readonly bool[] _live;

        // Pool bookkeeping
        private int _alive;
        private int _highWater;
        private int _freeHint;
        private float _spawnAccumulator;
        private Vector3 _anchor;

        // Service state
        private bool _enabled;
        private bool _useManualSeason;
        private float _manualSeasonProgress = 0.70f; // autumn peak
        private Vector2 _windXZ;                     // updated by WindUpdatedEvent subscription
        private SeasonChangedEvent? _lastObservedSeasonChange;

        // Diagnostics
        private int _lastDrawnLeaves;
        private float _lastSpawnRate;

        // GPU resources (lazy — first Draw call constructs)
        private DynamicVertexBuffer _vb;
        private IndexBuffer _ib;
        private VertexPositionColorTexture[] _cpuVerts;
        private BasicEffect _effect;
        private bool _resourcesReady;

        public LeafFallService(
            LeafFallServiceConfig config,
            IRendererEventBus bus,
            ISeasonService season,
            ILeafSpawnSource spawnSource,
            ILeafTextureProvider textureProvider)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _season = season ?? throw new ArgumentNullException(nameof(season));
            _spawnSource = spawnSource ?? throw new ArgumentNullException(nameof(spawnSource));
            _textureProvider = textureProvider ?? throw new ArgumentNullException(nameof(textureProvider));

            int max = _config.MaxLeaves;
            if (max < 1) throw new ArgumentOutOfRangeException(nameof(config), "MaxLeaves must be >= 1");

            _pos = new Vector3[max];
            _vel = new Vector3[max];
            _life = new float[max];
            _maxLife = new float[max];
            _size = new float[max];
            _spin0 = new float[max];
            _spinR = new float[max];
            _color = new Color[max];
            _live = new bool[max];

            _enabled = _config.InitialEnabled;
            _rng = new Random(_config.RandomSeed);

            _windSubscription = _bus.Subscribe<WindUpdatedEvent>(OnWindUpdated);
            _seasonSubscription = _bus.Subscribe<SeasonChangedEvent>(OnSeasonChanged);
        }

        public void Dispose()
        {
            _windSubscription?.Dispose();
            _seasonSubscription?.Dispose();
            _vb?.Dispose();
            _ib?.Dispose();
            _effect?.Dispose();
        }

        private void OnWindUpdated(WindUpdatedEvent evt) => _windXZ = evt.VectorXZ;
        private void OnSeasonChanged(SeasonChangedEvent evt) => _lastObservedSeasonChange = evt;

        // ===== ILeafFallService =====

        public bool Enabled => _enabled;
        public int AliveLeaves => _alive;
        public int LastDrawnLeaves => _lastDrawnLeaves;
        public float LastSpawnRate => _lastSpawnRate;
        public bool UseManualSeason => _useManualSeason;
        public float ManualSeasonProgress => _manualSeasonProgress;

        public SeasonChangedEvent? LastObservedSeasonChange => _lastObservedSeasonChange;

        public void SetEnabled(bool enabled) => _enabled = enabled;
        public void SetUseManualSeason(bool manual) => _useManualSeason = manual;
        public void SetManualSeasonProgress(float progress)
            => _manualSeasonProgress = MathHelper.Clamp(progress, 0f, 1f);

        public void Configure(Vector3 playerAnchorWorld) => _anchor = playerAnchorWorld;

        public void Clear()
        {
            for (int i = 0; i < _highWater; i++) _live[i] = false;
            _alive = 0;
            _highWater = 0;
            _freeHint = 0;
            _spawnAccumulator = 0f;
        }

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            if (!_enabled) { _lastSpawnRate = 0f; return; }
            float dt = ctx.DeltaSeconds;
            if (dt > _config.MaxDeltaSeconds) dt = _config.MaxDeltaSeconds;

            float year = ResolveYearProgress();
            float rate = SpawnRateAt(year) * _config.MaxSpawnPerSecond;
            _lastSpawnRate = rate;

            _spawnAccumulator += rate * dt;
            int toSpawn = (int)_spawnAccumulator;
            _spawnAccumulator -= toSpawn;
            for (int i = 0; i < toSpawn; i++) SpawnOne(year);

            StepPhysics(dt);
        }

        private void StepPhysics(float dt)
        {
            Vector2 wind = _windXZ * _config.WindAdvectionScale;
            int lastLive = -1;
            for (int i = 0; i < _highWater; i++)
            {
                if (!_live[i]) continue;
                _life[i] += dt;
                if (_life[i] >= _maxLife[i])
                {
                    _live[i] = false;
                    _alive--;
                    continue;
                }
                float sway = MathF.Sin((_life[i] * _config.SwayFrequencyRad) + _spin0[i])
                             * _config.SwayAmplitude;
                _pos[i].X += (_vel[i].X + wind.X * _config.WindDriftFraction + sway) * dt;
                _pos[i].Y += _vel[i].Y * dt;
                _pos[i].Z += (_vel[i].Z + wind.Y * _config.WindDriftFraction) * dt;
                lastLive = i;
            }
            _highWater = lastLive + 1;
        }

        // ===== Spawn =====

        private float ResolveYearProgress()
        {
            if (_useManualSeason) return MathHelper.Clamp(_manualSeasonProgress, 0f, 1f);
            // ISeasonService.YearPhase is already in [0,1) (Progress wraps internally for showcase).
            return MathHelper.Clamp(_season.YearPhase, 0f, 1f);
        }

        private void SpawnOne(float year)
        {
            int idx = FindFreeSlot();
            if (idx < 0) return;

            Vector3 spawnPos = PickSpawnPosition();
            _pos[idx] = spawnPos;

            float fall = -55f - (float)_rng.NextDouble() * 25f;
            float drx = ((float)_rng.NextDouble() * 2f - 1f) * 12f;
            float drz = ((float)_rng.NextDouble() * 2f - 1f) * 12f;
            _vel[idx] = new Vector3(drx, fall, drz);

            _life[idx] = 0f;
            _maxLife[idx] = 6f + (float)_rng.NextDouble() * 4f;
            _size[idx] = 6f + (float)_rng.NextDouble() * 5f;
            _spin0[idx] = (float)_rng.NextDouble() * MathHelper.TwoPi;
            _spinR[idx] = ((float)_rng.NextDouble() * 2f - 1f) * 2.5f;
            _color[idx] = SeasonalColor(year, _rng);
            _live[idx] = true;
            _alive++;
            if (idx + 1 > _highWater) _highWater = idx + 1;
            _freeHint = (idx + 1) % _config.MaxLeaves;
        }

        private int FindFreeSlot()
        {
            int max = _config.MaxLeaves;
            int start = _freeHint;
            for (int n = 0; n < max; n++)
            {
                int i = (start + n) % max;
                if (!_live[i]) return i;
            }
            return -1;
        }

        private Vector3 PickSpawnPosition()
        {
            IReadOnlyList<LeafSpawnAnchor> trees = _spawnSource.GetVisibleAnchors();
            if (trees.Count > 0) return PickFromTreeAnchor(trees[_rng.Next(trees.Count)]);
            return PickFromPlayerAnchor();
        }

        private Vector3 PickFromTreeAnchor(LeafSpawnAnchor t)
        {
            float jR = MathF.Sqrt((float)_rng.NextDouble()) * t.HalfWidth * 0.85f;
            float jA = (float)_rng.NextDouble() * MathHelper.TwoPi;
            float dx = MathF.Cos(jA) * jR;
            float dz = MathF.Sin(jA) * jR;
            float vMin = t.Anchor.Y + t.HeightPx * 0.25f;
            float vMax = t.Anchor.Y + t.HeightPx;
            float py = vMin + (float)_rng.NextDouble() * (vMax - vMin);
            if (py > vMax) py = vMax;
            return new Vector3(t.Anchor.X + dx, py, t.Anchor.Z + dz);
        }

        private Vector3 PickFromPlayerAnchor()
        {
            float r = _config.SpawnRadius * MathF.Sqrt((float)_rng.NextDouble());
            float a = (float)_rng.NextDouble() * MathHelper.TwoPi;
            return _anchor + new Vector3(
                MathF.Cos(a) * r,
                _config.SpawnHeight + (float)_rng.NextDouble() * 60f,
                MathF.Sin(a) * r);
        }

        // ===== Pure-math helpers (public for tests) =====

        /// <summary>Spawn-rate multiplier in [0,1] as a function of year progress.</summary>
        public static float SpawnRateAt(float y)
        {
            if (y < 0.05f) return 0f;
            if (y < 0.20f) return 0.04f;
            if (y < 0.50f) return 0.02f;
            if (y < 0.65f) return Lerp(0.05f, 0.30f, (y - 0.50f) / 0.15f);
            if (y < 0.78f) return Lerp(0.30f, 0.70f, (y - 0.65f) / 0.13f);
            if (y < 0.84f) return 1.00f;
            if (y < 0.95f) return Lerp(0.10f, 0.0f, (y - 0.84f) / 0.11f);
            return 0f;
        }

        /// <summary>Per-leaf seasonal palette with random per-leaf jitter.</summary>
        public static Color SeasonalColor(float y, Random rng)
        {
            Color spring = new Color(140, 210, 90);
            Color summer = new Color(60, 140, 50);
            Color earlyA = new Color(220, 180, 60);
            Color midA   = new Color(220, 130, 50);
            Color lateA  = new Color(170, 60, 40);
            Color brown  = new Color(110, 70, 40);

            Color baseC;
            if      (y < 0.20f) baseC = Color.Lerp(spring, summer, (y - 0.05f) / 0.15f);
            else if (y < 0.50f) baseC = summer;
            else if (y < 0.60f) baseC = Color.Lerp(summer, earlyA, (y - 0.50f) / 0.10f);
            else if (y < 0.70f) baseC = Color.Lerp(earlyA, midA,   (y - 0.60f) / 0.10f);
            else if (y < 0.80f) baseC = Color.Lerp(midA,   lateA,  (y - 0.70f) / 0.10f);
            else if (y < 0.90f) baseC = Color.Lerp(lateA,  brown,  (y - 0.80f) / 0.10f);
            else                baseC = brown;

            int jr = rng.Next(-25, 26);
            int jg = rng.Next(-25, 26);
            int jb = rng.Next(-15, 16);
            return new Color(
                (byte)Math.Clamp(baseC.R + jr, 0, 255),
                (byte)Math.Clamp(baseC.G + jg, 0, 255),
                (byte)Math.Clamp(baseC.B + jb, 0, 255),
                (byte)255);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * MathHelper.Clamp(t, 0f, 1f);

        // ===== Draw (GPU-bound; not exercised by unit tests) =====

        public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
        {
            if (!_enabled || _alive == 0 || device == null) return;
            EnsureResources(device);
            Texture2D leafTex = _textureProvider.GetTexture(device);
            if (leafTex == null) return;

            (Vector3 right, Vector3 up) = ComputeBillboardBasis(view);

            int n = WriteVisibleQuads(right, up);
            if (n == 0) { _lastDrawnLeaves = 0; return; }
            _lastDrawnLeaves = n;

            _vb.SetData(_cpuVerts, 0, n * 4, SetDataOptions.Discard);
            DrawQuads(device, view, projection, leafTex, n);
        }

        private static (Vector3 right, Vector3 up) ComputeBillboardBasis(Matrix view)
        {
            Matrix invView = Matrix.Invert(view);
            Vector3 right = new Vector3(invView.M11, invView.M12, invView.M13);
            Vector3 up = new Vector3(invView.M21, invView.M22, invView.M23);
            if (right.LengthSquared() < 0.01f)
                return (new Vector3(0.7071f, 0f, -0.7071f), Vector3.UnitY);
            right.Normalize(); up.Normalize();
            return (right, up);
        }

        private int WriteVisibleQuads(Vector3 right, Vector3 up)
        {
            int n = 0;
            int max = _config.MaxLeaves;
            for (int i = 0; i < _highWater; i++)
            {
                if (!_live[i]) continue;
                float t = _life[i] / Math.Max(0.0001f, _maxLife[i]);
                if (t > 1f) t = 1f;
                float sz = _size[i] * 0.5f;
                byte alpha = (byte)Math.Clamp(255f * (1f - MathF.Max(0f, t - 0.80f) / 0.20f), 0f, 255f);
                Color c = new Color(_color[i].R, _color[i].G, _color[i].B, alpha);

                float ang = _spin0[i] + _life[i] * _spinR[i];
                float cs = MathF.Cos(ang);
                float sn = MathF.Sin(ang);
                Vector3 r = (right * cs + up * sn) * sz;
                Vector3 u = (-right * sn + up * cs) * sz;
                Vector3 p = _pos[i];
                int vi = n * 4;
                _cpuVerts[vi + 0] = new VertexPositionColorTexture(p - r - u, c, new Vector2(0, 0));
                _cpuVerts[vi + 1] = new VertexPositionColorTexture(p + r - u, c, new Vector2(1, 0));
                _cpuVerts[vi + 2] = new VertexPositionColorTexture(p + r + u, c, new Vector2(1, 1));
                _cpuVerts[vi + 3] = new VertexPositionColorTexture(p - r + u, c, new Vector2(0, 1));
                n++;
                if (n >= max) break;
            }
            return n;
        }

        private void DrawQuads(GraphicsDevice gd, Matrix view, Matrix proj, Texture2D leafTex, int n)
        {
            DepthStencilState prevDepth = gd.DepthStencilState;
            BlendState prevBlend = gd.BlendState;
            RasterizerState prevRaster = gd.RasterizerState;
            SamplerState prevSampler = gd.SamplerStates[0];

            gd.DepthStencilState = DepthStencilState.DepthRead;
            gd.BlendState = BlendState.NonPremultiplied;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.SamplerStates[0] = SamplerState.LinearClamp;

            _effect.World = Matrix.Identity;
            _effect.View = view;
            _effect.Projection = proj;
            _effect.VertexColorEnabled = true;
            _effect.LightingEnabled = false;
            _effect.FogEnabled = false;
            _effect.TextureEnabled = true;
            _effect.Texture = leafTex;
            _effect.Alpha = 1f;

            gd.SetVertexBuffer(_vb);
            gd.Indices = _ib;
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, n * 4, 0, n * 2);
            }

            _effect.Texture = null;
            _effect.TextureEnabled = false;
            gd.SetVertexBuffer(null);
            gd.DepthStencilState = prevDepth;
            gd.BlendState = prevBlend;
            gd.RasterizerState = prevRaster;
            gd.SamplerStates[0] = prevSampler;
        }

        private void EnsureResources(GraphicsDevice gd)
        {
            if (_resourcesReady) return;
            int max = _config.MaxLeaves;
            _cpuVerts = new VertexPositionColorTexture[max * 4];
            _vb = new DynamicVertexBuffer(gd, VertexPositionColorTexture.VertexDeclaration,
                max * 4, BufferUsage.WriteOnly);
            short[] idx = new short[max * 6];
            for (int i = 0; i < max; i++)
            {
                int v = i * 4, o = i * 6;
                idx[o + 0] = (short)(v + 0); idx[o + 1] = (short)(v + 1); idx[o + 2] = (short)(v + 2);
                idx[o + 3] = (short)(v + 0); idx[o + 4] = (short)(v + 2); idx[o + 5] = (short)(v + 3);
            }
            _ib = new IndexBuffer(gd, IndexElementSize.SixteenBits, max * 6, BufferUsage.WriteOnly);
            _ib.SetData(idx);
            _effect = new BasicEffect(gd);
            _resourcesReady = true;
        }
    }
}
