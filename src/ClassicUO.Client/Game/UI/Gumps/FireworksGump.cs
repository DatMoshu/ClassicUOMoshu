// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — UI for the 3D fireworks show.
// Toggles enable, loop, and the climax text; offers a "Trigger Now" button.
//
// MIGRATION (ADR-012 §6 / playbook §Q): constructor-injected IFireworksService.
// Particle3DSystem reads stay on the legacy facade — that system isn't migrated yet.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Effects;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class FireworksGump : Gump
    {
        public static FireworksGump Instance;

        private readonly IFireworksService _fireworks;
        private readonly IParticleService _particle;

        private const int W = 320;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

        private static class BtnId
        {
            public const int TriggerNow = 1;
            public const int Stop       = 2;
            public const int Clear      = 3;
        }

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;
        private Label _statusLabel;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        /// <summary>
        /// Convenience overload that resolves <see cref="IFireworksService"/> from the
        /// active renderer service container.
        /// </summary>
        public FireworksGump(World world)
            : this(world, Renderer3DHost.Services.Fireworks, Renderer3DHost.Services.Particle) { }

        public FireworksGump(World world, IFireworksService fireworks, IParticleService particle)
            : base(world, 0, 0)
        {
            _fireworks = fireworks ?? throw new ArgumentNullException(nameof(fireworks));
            _particle = particle ?? throw new ArgumentNullException(nameof(particle));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "FIREWORKS  SHOW",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2;

            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "TOGGLES", y);
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Particles enabled", _particle.Enabled,
                v => _particle.SetEnabled(v),
                "Verbose log", _particle.VerboseLog,
                v => _particle.SetVerboseLog(v));

            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Show running", _fireworks.Enabled,
                v => { if (v) _fireworks.Trigger(); else _fireworks.Stop(); },
                "Loop", _fireworks.Loop,
                v => _fireworks.SetLoop(v));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- CLIMAX TEXT ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "CLIMAX TEXT", y);
            // Label + editable text box. Box max ~32 chars; bitmap font supports
            // A–Z, 0–9, and basic punctuation (unsupported chars render as a
            // placeholder box rather than throwing).
            int labelW = 60;
            Add(new Label("Text:", true, Debug3DStyle.HUE_LABEL_WHITE, labelW, font: 1)
                { X = contentX, Y = y + 4 });

            int boxX = contentX + labelW;
            int boxW = innerW - labelW - 4;
            // Frame the box so it reads as an editable field.
            Add(new AlphaBlendControl(0.55f) { X = boxX - 2, Y = y, Width = boxW + 4, Height = 22 });
            Add(new Line(boxX - 2,         y,      boxW + 4, 1, Debug3DStyle.BORDER_RGBA));
            Add(new Line(boxX - 2,         y + 21, boxW + 4, 1, Debug3DStyle.BORDER_RGBA));
            Add(new Line(boxX - 2,         y,      1, 22,        Debug3DStyle.BORDER_RGBA));
            Add(new Line(boxX + boxW + 1,  y,      1, 22,        Debug3DStyle.BORDER_RGBA));

            var textBox = new StbTextBox(1, max_char_count: 32, maxWidth: boxW,
                isunicode: true, hue: Debug3DStyle.HUE_VALUE)
            {
                X = boxX,
                Y = y + 2,
                Width = boxW,
                Height = 18
            };
            Add(textBox);
            textBox.SetText(_fireworks.ClimaxText);
            // SetClimaxText invalidates layout internally — no separate hook needed.
            textBox.TextChanged += (s, e) =>
                _fireworks.SetClimaxText(textBox.Text ?? string.Empty);
            y += 22 + 4;
            y += Debug3DStyle.SECTION_GAP;

            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "ACTIONS", y);
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Trigger Now")
                { ButtonParameter = BtnId.TriggerNow });
            y += ROW_H + 2;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Stop")
                { ButtonParameter = BtnId.Stop });
            y += ROW_H + 2;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Clear all particles")
                { ButtonParameter = BtnId.Clear });
            y += ROW_H + 2;
            y += Debug3DStyle.SECTION_GAP;

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
            string status;
            if (_fireworks.Enabled)
            {
                status = $"t={_fireworks.CurrentTime:0.0}s  alive={_particle.AliveParticles}  drawn={_particle.LastDrawnParticles}";
            }
            else
            {
                status = $"(idle)  alive={_particle.AliveParticles}";
            }
            _statusLabel.Text = status;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); break;
                case BtnId.TriggerNow: _fireworks.Trigger(); break;
                case BtnId.Stop:       _fireworks.Stop(); break;
                case BtnId.Clear:      _particle.Clear(); break;
            }
        }
    }
}
