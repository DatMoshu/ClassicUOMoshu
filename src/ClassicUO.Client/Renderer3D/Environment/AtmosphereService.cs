// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

using System;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// State-owning implementation of <see cref="IAtmosphereService"/>. Holds the target
    /// colors + lerp speed + pulse amount/hz directly (no gateway bridge — state has fully
    /// moved off <c>World3DRenderer</c>). Reads + writes the active BG/Fog colors through
    /// the injected <see cref="IEnvironmentService"/>.
    /// </summary>
    public sealed class AtmosphereService : IAtmosphereService
    {
        private readonly IEnvironmentService _environment;

        // Initial values mirror the legacy World3DRenderer defaults.
        private Color _bgTarget   = Color.Black;
        private Color _fogTarget  = new Color(70, 130, 180, 255);
        private float _lerpSpeed  = 0.5f;   // ~2 seconds to ease
        private float _pulseAmt   = 0f;
        private float _pulseHz    = 0.20f;

        private float _pulsePhase;

        public AtmosphereService(IEnvironmentService environment)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        public Color BackgroundColorTarget => _bgTarget;
        public Color FogColorTarget => _fogTarget;
        public float AtmosphereLerpSpeed => _lerpSpeed;
        public float AtmospherePulseAmount => _pulseAmt;
        public float AtmospherePulseHz => _pulseHz;

        public void SetBackgroundColorTarget(Color color) => _bgTarget = color;
        public void SetFogColorTarget(Color color) => _fogTarget = color;
        public void SetAtmosphereLerpSpeed(float speed) => _lerpSpeed = speed;
        public void SetAtmospherePulseAmount(float amount) => _pulseAmt = amount;
        public void SetAtmospherePulseHz(float hz) => _pulseHz = hz;

        public void Tick(float dt)
        {
            float k = MathF.Min(1f, dt * _lerpSpeed);

            var bg  = LerpColor(_environment.BackgroundColor, _bgTarget,  k);
            var fog = LerpColor(_environment.FogColor,        _fogTarget, k);

            if (_pulseAmt > 0.001f)
            {
                _pulsePhase += dt * _pulseHz * MathHelper.TwoPi;
                if (_pulsePhase > MathHelper.TwoPi) _pulsePhase -= MathHelper.TwoPi;
                float s = (MathF.Sin(_pulsePhase) * 0.5f + 0.5f) * _pulseAmt;
                int r = (int)MathHelper.Clamp(bg.R + s * 60f, 0f, 255f);
                bg = new Color((byte)r, bg.G, bg.B, bg.A);
            }

            _environment.SetBackgroundColor(bg);
            _environment.SetFogColor(fog);
        }

        private static Color LerpColor(Color a, Color b, float k)
        {
            return new Color(
                (byte)(a.R + (b.R - a.R) * k),
                (byte)(a.G + (b.G - a.G) * k),
                (byte)(a.B + (b.B - a.B) * k),
                (byte)(a.A + (b.A - a.A) * k));
        }
    }
}
