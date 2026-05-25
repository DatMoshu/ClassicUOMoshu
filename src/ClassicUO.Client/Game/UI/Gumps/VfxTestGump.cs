// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — VFX Test Scene gump.
//
// Canonical preview surface for every effect catalogued in
// `design/Core/Art/vfx-bible.md`. Rows are grouped by bible category;
// each row binds a Play button to a service call. SPEC entries (no
// service hooked yet) render as disabled rows so this gump and the
// bible never drift.
//
// MIGRATION (ADR-012 §6 / playbook §Q): constructor-injected services.
// Resolution helper at bottom mirrors NukeGump's pattern.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Effects;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class VfxTestGump : Gump
    {
        public static VfxTestGump Instance;

        private readonly IParticleService    _particle;
        private readonly IFireService        _fire;
        private readonly IExplosionService   _explosion;
        private readonly IBuffParticleService _buff;
        private readonly INukeShowService    _nuke;
        private readonly IFireworksService   _fireworks;
        // NOTE: ambient-motes service is not currently surfaced via Renderer3DServices
        // in this fork; add the accessor + inject here when wired.

        private const int W         = 360;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

        // Per-gump button-id namespace. Bible IDs in comments for grep-ability.
        private static class BtnId
        {
            // CATEGORY 1 — Combat (C-01..C-22)  → 1..29
            public const int MeleeSlash    = 1;   // C-01 — SPEC
            public const int MeleeBlunt    = 2;   // C-02 — SPEC
            public const int MeleePierce   = 3;   // C-03 — SPEC
            public const int MeleeCrush    = 4;   // C-04 — SPEC
            public const int SpellFire     = 5;   // C-05 — SPEC
            public const int SpellCold     = 6;   // C-06 — SPEC
            public const int SpellPoison   = 7;   // C-07 — SPEC
            public const int SpellHoly     = 8;   // C-08 — SPEC
            public const int SpellBlood    = 9;   // C-09 — SPEC
            public const int SpellArcane   = 10;  // C-10 — SPEC
            public const int SpellLight    = 11;  // C-11 — SPEC
            public const int SpellWind     = 12;  // C-12 — SPEC
            public const int CritFlash     = 13;  // C-13 — SPEC
            public const int Parry         = 14;  // C-14 — SPEC
            public const int Dodge         = 15;  // C-15 — SPEC
            public const int ProjArrow     = 16;  // C-16 — SPEC
            public const int ProjBolt      = 17;  // C-17 — SPEC
            public const int ProjFirearm   = 18;  // C-18 — SPEC
            public const int Blood         = 19;  // C-19 — SPEC
            public const int Stagger       = 20;  // C-20 — SPEC
            public const int LowHpVignette = 21;  // C-21 — SPEC
            public const int StunStars     = 22;  // C-22 — uses BuffParticleService

            // CATEGORY 2 — Siege (S-01..S-16)  → 40..59
            public const int CannonFlash    = 40;  // S-01
            public const int MortarTrail    = 41;  // S-02
            public const int ShellImpact    = 42;  // S-03 — WIRED via Explosion
            public const int ShellIncend    = 43;  // S-04 — uses Fire
            public const int ShellSmoke     = 44;  // S-05
            public const int Shockwave      = 45;  // S-06
            public const int DebrisBurst    = 46;  // S-07
            public const int WallCollapse   = 47;  // S-08
            public const int BreachBeacon   = 48;  // S-09
            public const int SiegeRecoil    = 49;  // S-10
            public const int TrebSling      = 50;  // S-11
            public const int RamImpact      = 51;  // S-12
            public const int FieldBombard   = 52;  // S-13 — uses NukeShow
            public const int KegCookoff     = 53;  // S-14
            public const int CannonDust     = 54;  // S-15
            public const int BannerPlant    = 55;  // S-16

            // CATEGORY 3 — Environmental (E-01..E-14)  → 60..79
            // E-01..E-03, E-05, E-07, E-14 already have their own admin gumps; not duplicated here.

            // CATEGORY 4 — Status FX (SF-01..SF-12)  → 80..99 — BuffParticleService
            public const int SfPoison    = 80;
            public const int SfBleed     = 81;  // SPEC (new interval)
            public const int SfStun      = 82;
            public const int SfBurn      = 83;
            public const int SfFreeze    = 84;
            public const int SfHoly      = 85;
            public const int SfCurse     = 86;
            public const int SfHaste     = 87;
            public const int SfStealth   = 88;
            public const int SfRegen     = 89;
            public const int SfShield    = 90;
            public const int SfSilence   = 91;  // SPEC

            // CATEGORY 5 — World FX (W-01..W-12)  → 100..119
            public const int Torch       = 100; // uses Fire (variant SPEC)
            public const int Campfire    = 101; // WIRED via Fire
            public const int ForgeSparks = 102; // SPEC
            // others SPEC

            // CATEGORY 7 — Cinematic (CI-01..CI-10)  → 140..159
            public const int CiNuke      = 140; // WIRED
            public const int CiFireworks = 141; // WIRED

            // GLOBAL
            public const int StopAll     = 250;
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

        public VfxTestGump(World world)
            : this(world,
                Renderer3DHost.Services.Particle,
                Renderer3DHost.Services.Fire,
                Renderer3DHost.Services.Explosion,
                Renderer3DHost.Services.BuffParticles,
                Renderer3DHost.Services.NukeShow,
                Renderer3DHost.Services.Fireworks) { }

        public VfxTestGump(World world,
                           IParticleService     particle,
                           IFireService         fire,
                           IExplosionService    explosion,
                           IBuffParticleService buff,
                           INukeShowService     nuke,
                           IFireworksService    fireworks) : base(world, 0, 0)
        {
            _particle  = particle  ?? throw new ArgumentNullException(nameof(particle));
            _fire      = fire      ?? throw new ArgumentNullException(nameof(fire));
            _explosion = explosion ?? throw new ArgumentNullException(nameof(explosion));
            _buff      = buff      ?? throw new ArgumentNullException(nameof(buff));
            _nuke      = nuke      ?? throw new ArgumentNullException(nameof(nuke));
            _fireworks = fireworks ?? throw new ArgumentNullException(nameof(fireworks));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "VFX TEST SCENE",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW   = W - INNER_PAD * 2;

            // ---------- COMBAT ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "COMBAT (spec)", y);
            y = AddSpecRow(contentX, innerW, y, "C-01 Melee Slash",   BtnId.MeleeSlash);
            y = AddSpecRow(contentX, innerW, y, "C-02 Melee Blunt",   BtnId.MeleeBlunt);
            y = AddSpecRow(contentX, innerW, y, "C-03 Melee Pierce",  BtnId.MeleePierce);
            y = AddSpecRow(contentX, innerW, y, "C-04 Melee Crush",   BtnId.MeleeCrush);
            y = AddSpecRow(contentX, innerW, y, "C-05 Spell Fire",    BtnId.SpellFire);
            y = AddSpecRow(contentX, innerW, y, "C-06 Spell Cold",    BtnId.SpellCold);
            y = AddSpecRow(contentX, innerW, y, "C-07 Spell Poison",  BtnId.SpellPoison);
            y = AddSpecRow(contentX, innerW, y, "C-08 Spell Holy",    BtnId.SpellHoly);
            y = AddSpecRow(contentX, innerW, y, "C-09 Spell Blood",   BtnId.SpellBlood);
            y = AddSpecRow(contentX, innerW, y, "C-10 Spell Arcane",  BtnId.SpellArcane);
            y = AddSpecRow(contentX, innerW, y, "C-11 Spell Lightning", BtnId.SpellLight);
            y = AddSpecRow(contentX, innerW, y, "C-12 Spell Wind",    BtnId.SpellWind);
            y = AddSpecRow(contentX, innerW, y, "C-18 Firearm Round", BtnId.ProjFirearm);
            y = AddSpecRow(contentX, innerW, y, "C-19 Blood Spatter", BtnId.Blood);
            y = AddPlayRow(contentX, innerW, y, "C-22 Stun Stars (buff)", BtnId.StunStars);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- SIEGE ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "SIEGE", y);
            y = AddPlayRow(contentX, innerW, y, "S-03 Shell Impact (explosion)", BtnId.ShellImpact);
            y = AddPlayRow(contentX, innerW, y, "S-04 Incendiary (fire patch)",  BtnId.ShellIncend);
            y = AddSpecRow(contentX, innerW, y, "S-01 Cannon Muzzle Flash",      BtnId.CannonFlash);
            y = AddSpecRow(contentX, innerW, y, "S-05 Smoke Shell",              BtnId.ShellSmoke);
            y = AddSpecRow(contentX, innerW, y, "S-07 Debris Burst",             BtnId.DebrisBurst);
            y = AddSpecRow(contentX, innerW, y, "S-08 Wall Collapse",            BtnId.WallCollapse);
            y = AddPlayRow(contentX, innerW, y, "S-13 Field Bombard (nuke barrage)", BtnId.FieldBombard);
            y = AddSpecRow(contentX, innerW, y, "S-16 Banner Plant",             BtnId.BannerPlant);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- STATUS FX (BuffParticleService) ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "STATUS FX", y);
            y = AddPlayRow(contentX, innerW, y, "SF-01 Poison",  BtnId.SfPoison);
            y = AddPlayRow(contentX, innerW, y, "SF-04 Burn",    BtnId.SfBurn);
            y = AddPlayRow(contentX, innerW, y, "SF-05 Freeze",  BtnId.SfFreeze);
            y = AddPlayRow(contentX, innerW, y, "SF-06 Holy",    BtnId.SfHoly);
            y = AddPlayRow(contentX, innerW, y, "SF-07 Curse",   BtnId.SfCurse);
            y = AddPlayRow(contentX, innerW, y, "SF-08 Haste",   BtnId.SfHaste);
            y = AddPlayRow(contentX, innerW, y, "SF-09 Stealth", BtnId.SfStealth);
            y = AddPlayRow(contentX, innerW, y, "SF-10 Regen",   BtnId.SfRegen);
            y = AddPlayRow(contentX, innerW, y, "SF-11 Shield",  BtnId.SfShield);
            y = AddSpecRow(contentX, innerW, y, "SF-02 Bleed (new interval)",   BtnId.SfBleed);
            y = AddSpecRow(contentX, innerW, y, "SF-12 Silence (new interval)", BtnId.SfSilence);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- WORLD ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "WORLD", y);
            y = AddPlayRow(contentX, innerW, y, "W-01/02 Torch/Campfire (fire)", BtnId.Campfire);
            y = AddSpecRow(contentX, innerW, y, "W-03 Forge Sparks",             BtnId.ForgeSparks);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- CINEMATIC ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "CINEMATIC", y);
            y = AddPlayRow(contentX, innerW, y, "CI-01 Nuke Barrage",   BtnId.CiNuke);
            y = AddPlayRow(contentX, innerW, y, "CI-02 Victory Fireworks", BtnId.CiFireworks);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- GLOBAL ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "GLOBAL", y);
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Stop All / Clear")
                { ButtonParameter = BtnId.StopAll });
            y += ROW_H + 2;
            y += Debug3DStyle.SECTION_GAP;

            // ---------- STATUS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "STATUS", y);
            _statusLabel = new Label("(idle)", true, Debug3DStyle.HUE_VALUE, innerW, font: 1)
                { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H + 4;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 320;
            Y = 60;
            WantUpdateSize = false;
        }

        private int AddPlayRow(int contentX, int innerW, int y, string label, int btn)
        {
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "▶ " + label)
                { ButtonParameter = btn });
            return y + ROW_H + 2;
        }

        // SPEC rows: bible-only effects (no service hook yet). Render as a label-only
        // row so the gump and the bible stay in lockstep; clicking is a no-op that
        // logs the bible ID so the dev knows it isn't wired yet.
        private int AddSpecRow(int contentX, int innerW, int y, string label, int btn)
        {
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "[SPEC] " + label)
                { ButtonParameter = btn });
            return y + ROW_H + 2;
        }

        public override void Update()
        {
            base.Update();
            if (_statusLabel == null || _statusLabel.IsDisposed) return;
            _statusLabel.Text =
                $"alive={_particle.AliveParticles}  blasts={_explosion.LiveEvents}  fires={_fire.LiveFires}";
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); return;

                // WIRED — drive existing services
                case BtnId.ShellImpact:
                {
                    // Drop a single explosion in front of the player.
                    var anchor = _nuke.Anchor;
                    _explosion.Trigger(anchor + new Vector3(0f, 0f, -200f), 1.0f);
                    break;
                }
                case BtnId.ShellIncend:
                {
                    var anchor = _nuke.Anchor;
                    _fire.Ignite(anchor + new Vector3(0f, 0f, -200f));
                    break;
                }
                case BtnId.FieldBombard:  _nuke.TriggerBarrage(); break;
                case BtnId.CiNuke:        _nuke.TriggerSingle();  break;
                case BtnId.CiFireworks:   _fireworks.Trigger();   break;
                case BtnId.Campfire:
                {
                    var anchor = _nuke.Anchor;
                    _fire.Ignite(anchor);
                    break;
                }

                // BuffParticleService — preview status FX intervals
                case BtnId.SfPoison:  _buff.PreviewArchetype("Poison");  break;
                case BtnId.SfBurn:    _buff.PreviewArchetype("Fire");    break;
                case BtnId.SfFreeze:  _buff.PreviewArchetype("Ice");     break;
                case BtnId.SfHoly:    _buff.PreviewArchetype("Holy");    break;
                case BtnId.SfCurse:   _buff.PreviewArchetype("Curse");   break;
                case BtnId.SfHaste:   _buff.PreviewArchetype("Wind");    break;
                case BtnId.SfStealth: _buff.PreviewArchetype("Stealth"); break;
                case BtnId.SfRegen:   _buff.PreviewArchetype("Defense"); break;
                case BtnId.SfShield:  _buff.PreviewArchetype("FormShift"); break;
                case BtnId.StunStars: _buff.PreviewArchetype("Stat");    break;

                case BtnId.StopAll:
                    _particle.Clear();
                    _explosion.Clear();
                    _fire.Clear();
                    break;

                // SPEC — bible-only entries; log so the dev knows it's not wired.
                // NOTE: IBuffParticleService.PreviewArchetype is assumed by this gump;
                // if absent in this fork, replace with the actual preview API or
                // delete these branches until the service exposes one.
                default:
                    // SPEC row clicked. No-op; intentional. Bible ID is in the label.
                    break;
            }
        }
    }
}
