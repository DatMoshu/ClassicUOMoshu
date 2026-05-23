// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — UI for IBuffParticleService.
// Master enable + per-archetype enable toggles + status readout.
//
// MIGRATION (ADR-012 §6 / playbook §Q): two-service constructor injection —
// IBuffParticleService + IParticleService.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
// BuffArchetype exists in both legacy (ClassicUO.Renderer.Renderer3D) and domain
// (ClassicUO.Renderer.Effects) namespaces. Alias the domain one — IBuffParticleService
// signatures take it; the legacy parallel enum stays unimported.
using BuffArchetype = ClassicUO.Renderer.Effects.BuffArchetype;
using IBuffParticleService = ClassicUO.Renderer.Effects.IBuffParticleService;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class BuffEffectsGump : Gump
    {
        public static BuffEffectsGump Instance;

        private readonly IBuffParticleService _buff;
        private readonly ClassicUO.Renderer.Effects.IParticleService _particle;

        private const int W = 320;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

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
        /// Convenience overload that resolves <see cref="IBuffParticleService"/> from the
        /// active renderer service container.
        /// </summary>
        public BuffEffectsGump(World world)
            : this(world, Renderer3DHost.Services.BuffParticles, Renderer3DHost.Services.Particle) { }

        public BuffEffectsGump(World world, IBuffParticleService buff,
                               ClassicUO.Renderer.Effects.IParticleService particle)
            : base(world, 0, 0)
        {
            _buff = buff ?? throw new ArgumentNullException(nameof(buff));
            _particle = particle ?? throw new ArgumentNullException(nameof(particle));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "BUFF  PARTICLE  FX",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2;

            // ---------- MASTER ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "MASTER", y);
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Enabled", _buff.Enabled,
                v => _buff.SetEnabled(v),
                "3D mode only", _buff.Require3DMode,
                v => _buff.SetRequire3DMode(v));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- ARCHETYPES ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "ARCHETYPES", y);
            y = AddArchPair(contentX, innerW, y, BuffArchetype.Fire,    BuffArchetype.Ice);
            y = AddArchPair(contentX, innerW, y, BuffArchetype.Holy,    BuffArchetype.Curse);
            y = AddArchPair(contentX, innerW, y, BuffArchetype.Poison,  BuffArchetype.Lightning);
            y = AddArchPair(contentX, innerW, y, BuffArchetype.Stat,    BuffArchetype.Defense);
            y = AddArchPair(contentX, innerW, y, BuffArchetype.Stealth, BuffArchetype.FormShift);
            y = AddArchPair(contentX, innerW, y, BuffArchetype.Wind,    BuffArchetype.Debuff);
            y = AddArchPair(contentX, innerW, y, BuffArchetype.Default, BuffArchetype.None);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- STATUS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "STATUS", y);
            _statusLabel = new Label("(no buffs active)", true, Debug3DStyle.HUE_VALUE, innerW, font: 1)
                { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H + 4;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 280;
            Y = 60;
            WantUpdateSize = false;
        }

        private int AddArchPair(int contentX, int innerW, int y, BuffArchetype a, BuffArchetype b)
        {
            string nameA = a.ToString();
            string nameB = b == BuffArchetype.None ? null : b.ToString();
            return Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                nameA,
                _buff.IsArchetypeEnabled(a),
                v => _buff.SetArchetypeEnabled(a, v),
                nameB,
                nameB != null && _buff.IsArchetypeEnabled(b),
                nameB != null ? (Action<bool>)(v => _buff.SetArchetypeEnabled(b, v)) : null);
        }

        public override void Update()
        {
            base.Update();
            if (_statusLabel != null && !_statusLabel.IsDisposed)
            {
                _statusLabel.Text =
                    $"buffs={_buff.LastActiveCount}  emits/tick={_buff.LastTickEmissions}  " +
                    $"alive={_particle.AliveParticles}";
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == Debug3DStyle.BTN_CLOSE) Dispose();
        }
    }
}
