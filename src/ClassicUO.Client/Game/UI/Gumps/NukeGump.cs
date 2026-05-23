// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — UI for the nuke / D-Day barrage particle effect.
//
// MIGRATION (ADR-012 §6 / playbook §Q): four-service constructor injection —
// INukeShowService + IExplosionService + IFireService + IParticleService.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Effects;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class NukeGump : Gump
    {
        public static NukeGump Instance;

        private readonly INukeShowService _nuke;
        private readonly IExplosionService _explosion;
        private readonly IFireService _fire;
        private readonly IParticleService _particle;

        private const int W = 320;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

        private static class BtnId
        {
            public const int Single   = 1;
            public const int Barrage  = 2;
            public const int Stop     = 3;
            public const int Clear    = 4;
            public const int IgniteAt = 5;  // test: drop a fire patch in front of player
        }

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[]    _borders;
        private Label     _statusLabel;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        /// <summary>
        /// Convenience overload that resolves all three services from the active renderer
        /// service container.
        /// </summary>
        public NukeGump(World world)
            : this(world,
                Renderer3DHost.Services.NukeShow,
                Renderer3DHost.Services.Explosion,
                Renderer3DHost.Services.Fire,
                Renderer3DHost.Services.Particle) { }

        public NukeGump(World world,
                        INukeShowService nuke,
                        IExplosionService explosion,
                        IFireService fire,
                        IParticleService particle) : base(world, 0, 0)
        {
            _nuke = nuke ?? throw new ArgumentNullException(nameof(nuke));
            _explosion = explosion ?? throw new ArgumentNullException(nameof(explosion));
            _fire = fire ?? throw new ArgumentNullException(nameof(fire));
            _particle = particle ?? throw new ArgumentNullException(nameof(particle));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "NUKE  /  D-DAY",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW   = W - INNER_PAD * 2;

            // ---------- TOGGLES ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "TOGGLES", y);
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Particles enabled", _particle.Enabled,
                v => _particle.SetEnabled(v),
                "D-Day audio",        _nuke.PlayDDayAudio,
                v => _nuke.SetPlayDDayAudio(v));
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Verbose log", _nuke.VerboseLog,
                v => _nuke.SetVerboseLog(v),
                "(reserved)",  false,
                v => { });
            y += Debug3DStyle.SECTION_GAP;

            // ---------- TUNING ----------
            // Sliders below let the player fine-tune nuke size/density without
            // a recompile. AddSlider is int-only; scale is encoded as %x100.
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "TUNING", y);
            const int LBL_W = 110;
            y = Debug3DStyle.AddSlider(this, contentX, innerW, LBL_W,
                "Scale (×100)", 50, 400, (int)(_nuke.NukeScale * 100f), y,
                v => _nuke.SetNukeScale(v / 100f), resetValue: 200);
            y = Debug3DStyle.AddSlider(this, contentX, innerW, LBL_W,
                "Bombs",        1, 24, _nuke.BarrageCount, y,
                v => _nuke.SetBarrageCount(v), resetValue: 6);
            y = Debug3DStyle.AddSlider(this, contentX, innerW, LBL_W,
                "Ring radius",  200, 2000, (int)_nuke.BarrageRadius, y,
                v => _nuke.SetBarrageRadius(v), resetValue: 900);
            y = Debug3DStyle.AddSlider(this, contentX, innerW, LBL_W,
                "Stagger (×10)", 1, 50, (int)(_nuke.Stagger * 10f), y,
                v => _nuke.SetStagger(v / 10f), resetValue: 13);
            y = Debug3DStyle.AddSlider(this, contentX, innerW, LBL_W,
                "Blast radius", 100, 2000, (int)_nuke.BlastRadius, y,
                v => _nuke.SetBlastRadius(v), resetValue: 600);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- ACTIONS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "ACTIONS", y);
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Single nuke (front)")
                { ButtonParameter = BtnId.Single });
            y += ROW_H + 2;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "D-Day barrage (ring)")
                { ButtonParameter = BtnId.Barrage });
            y += ROW_H + 2;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Test fire patch")
                { ButtonParameter = BtnId.IgniteAt });
            y += ROW_H + 2;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Stop barrage")
                { ButtonParameter = BtnId.Stop });
            y += ROW_H + 2;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Clear all particles")
                { ButtonParameter = BtnId.Clear });
            y += ROW_H + 2;
            y += Debug3DStyle.SECTION_GAP;

            // ---------- INFO ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "INFO", y);
            Add(new Label(
                "Audio: drop a WAV at  Sounds/dday.wav",
                true, Debug3DStyle.HUE_VALUE, innerW, font: 1)
                { X = contentX, Y = y });
            y += ROW_H + 4;

            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "STATUS", y);
            _statusLabel = new Label("(idle)", true, Debug3DStyle.HUE_VALUE, innerW, font: 1)
                { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H + 4;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 280;
            Y = 60;
            WantUpdateSize = false;
        }

        public override void Update()
        {
            base.Update();
            if (_statusLabel == null || _statusLabel.IsDisposed) return;
            string status = _nuke.IsRunning
                ? $"t={_nuke.CurrentTime:0.0}s  remaining={_nuke.RemainingDetonations}  alive={_particle.AliveParticles}  blasts={_explosion.LiveEvents}  fires={_fire.LiveFires}"
                : $"(idle)  alive={_particle.AliveParticles}  blasts={_explosion.LiveEvents}  fires={_fire.LiveFires}";
            _statusLabel.Text = status;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose();              break;
                case BtnId.Single:           _nuke.TriggerSingle();  break;
                case BtnId.Barrage:          _nuke.TriggerBarrage(); break;
                case BtnId.Stop:             _nuke.Stop();           break;
                case BtnId.Clear:
                    _particle.Clear();
                    _explosion.Clear();
                    _fire.Clear();
                    break;
                case BtnId.IgniteAt:
                {
                    // Drop a single fire patch a short distance in front of the
                    // player anchor so we can see the spread without nuking.
                    var anchor = _nuke.Anchor;
                    _fire.Ignite(anchor + new Vector3(0f, 0f, -200f));
                    break;
                }
            }
        }
    }
}
