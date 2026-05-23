// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — fluent builder for emitting strings as 3D particles.
//
// Usage:
//   new ParticleStringBuilder("WE DID IT!")
//       .At(skyAnchor)
//       .CellSize(18f)
//       .Align(TextAlign.Center)
//       .Colors(new Color(255,240,180), new Color(255,140,40,0))
//       .Size(14f, 8f)
//       .Life(0.6f)
//       .Pinned()
//       .Emit();
//
//   // Or animate the text as falling sparks that pin on arrival:
//   builder.From(new Vector3(0, 200, 0), new Vector3(0, -50, 0)).Emit();
//
// The builder caches its glyph layout — reuse the same instance across
// frames for repeated emissions (e.g. twinkling text) and only call
// Invalidate() when the text/origin/cell changes. A builder is cheap; the
// layout cache prevents per-frame string→points work for large messages.

using System;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    internal enum TextAlign { Left, Center, Right }

    internal sealed class ParticleStringBuilder
    {
        // ----- Inputs -----
        private string _text = string.Empty;
        private Vector3 _origin = Vector3.Zero;
        private float _cellSize = 12f;
        private TextAlign _align = TextAlign.Left;

        private Color _colorStart = Color.White;
        private Color _colorEnd   = new Color(255, 255, 255, (byte)0);
        private float _sizeStart  = 8f;
        private float _sizeEnd    = 4f;
        private float _life       = 0.6f;

        // Per-particle motion. Defaults = pinned (no motion).
        private Vector3 _spawnOffset = Vector3.Zero;
        private Vector3 _velocity    = Vector3.Zero;
        private Vector3 _accel       = Vector3.Zero;
        private byte _flags = Particle3DSystem.F_PINNED;

        // Optional jitter applied per-particle to soften the bitmap grid.
        private float _jitter = 0f;
        private Random _rng = new Random(0xBEEF);

        // ----- Cache -----
        private Vector3[] _layout;
        private string _layoutText;
        private Vector3 _layoutOrigin;
        private float _layoutCell;
        private TextAlign _layoutAlign;

        public ParticleStringBuilder() { }
        public ParticleStringBuilder(string text) { _text = text ?? string.Empty; }

        // ----- Fluent setters -----

        public ParticleStringBuilder Text(string text)
        {
            if (text != _text) { _text = text ?? string.Empty; Invalidate(); }
            return this;
        }

        public ParticleStringBuilder At(Vector3 origin)
        {
            if (origin != _origin) { _origin = origin; Invalidate(); }
            return this;
        }

        public ParticleStringBuilder CellSize(float cellSize)
        {
            if (cellSize != _cellSize) { _cellSize = cellSize; Invalidate(); }
            return this;
        }

        public ParticleStringBuilder Align(TextAlign align)
        {
            if (align != _align) { _align = align; Invalidate(); }
            return this;
        }

        public ParticleStringBuilder Colors(Color start, Color end)
        {
            _colorStart = start; _colorEnd = end; return this;
        }

        public ParticleStringBuilder Size(float startSize, float endSize)
        {
            _sizeStart = startSize; _sizeEnd = endSize; return this;
        }

        public ParticleStringBuilder Life(float seconds)
        {
            _life = seconds; return this;
        }

        public ParticleStringBuilder Pinned()
        {
            _spawnOffset = Vector3.Zero;
            _velocity = Vector3.Zero;
            _accel = Vector3.Zero;
            _flags = Particle3DSystem.F_PINNED;
            return this;
        }

        public ParticleStringBuilder From(Vector3 spawnOffset, Vector3 velocity, Vector3 accel = default)
        {
            _spawnOffset = spawnOffset;
            _velocity = velocity;
            _accel = accel;
            _flags = 0; // moving — not pinned
            return this;
        }

        public ParticleStringBuilder Trail(bool on = true)
        {
            if (on) _flags |= Particle3DSystem.F_TRAIL;
            else    _flags &= unchecked((byte)~Particle3DSystem.F_TRAIL);
            return this;
        }

        public ParticleStringBuilder Jitter(float worldUnits)
        {
            _jitter = worldUnits; return this;
        }

        public ParticleStringBuilder Seed(int seed)
        {
            _rng = new Random(seed); return this;
        }

        public void Invalidate() { _layout = null; }

        // ----- Output -----

        /// World-units bounding width of the laid-out text.
        public float Width => Particle3DText.MeasureWidth(_text, _cellSize);

        /// Total particle count one Emit() call will spawn.
        public int ParticleCount
        {
            get { EnsureLayout(); return _layout.Length; }
        }

        public Vector3[] Points
        {
            get { EnsureLayout(); return _layout; }
        }

        /// Spawn one particle per lit glyph pixel using the configured style.
        /// Returns the number of particles successfully spawned (may be less
        /// than ParticleCount if the pool is full).
        public int Emit()
        {
            EnsureLayout();
            int spawned = 0;
            for (int i = 0; i < _layout.Length; i++)
            {
                Vector3 target = _layout[i];
                if (_jitter > 0f)
                {
                    target += new Vector3(
                        ((float)_rng.NextDouble() * 2f - 1f) * _jitter,
                        ((float)_rng.NextDouble() * 2f - 1f) * _jitter,
                        ((float)_rng.NextDouble() * 2f - 1f) * _jitter);
                }
                Vector3 spawn = target + _spawnOffset;
                int idx = Particle3DSystem.Spawn(
                    spawn, _velocity, _accel,
                    _life, _sizeStart, _sizeEnd,
                    _colorStart, _colorEnd, _flags);
                if (idx >= 0) spawned++;
            }
            return spawned;
        }

        private void EnsureLayout()
        {
            if (_layout != null
                && _layoutText == _text
                && _layoutOrigin == _origin
                && _layoutCell == _cellSize
                && _layoutAlign == _align)
            {
                return;
            }
            Vector3 originWithAlign = _origin;
            float w = Width;
            switch (_align)
            {
                case TextAlign.Center: originWithAlign.X -= w * 0.5f; break;
                case TextAlign.Right:  originWithAlign.X -= w;        break;
            }
            _layout = Particle3DText.Layout(_text, originWithAlign, _cellSize);
            _layoutText = _text;
            _layoutOrigin = _origin;
            _layoutCell = _cellSize;
            _layoutAlign = _align;
        }
    }
}
