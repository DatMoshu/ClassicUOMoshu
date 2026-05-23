// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — dedicated form for 2D/3D mobile rendering controls.
//
// Per migration plan: the strategic, supported render path is "MobilesIn3D"
// (3D player rendered into a per-mobile RT, blitted into the 2D batcher at
// the existing depth). That feature ships and stays; the world/multi 3D
// experiments live elsewhere (Debug3DGump). Splitting the gumps keeps the
// supported and experimental UIs visually + cognitively separate.
//
// Opened from chat with `[mobile3d`. Hosts:
//   - Mode selector (single source of truth — RenderModeController)
//   - RT sliders (size, foot margin, Y anchor)
//   - 2D-sprite-overlay A/B toggle
//   - Quick-fire animation triggers (Idle/Run/Hit/Attack)
//   - Buttons to open the satellite mobile-3D gumps (PlayerMounts, ModelInfo,
//     EquipmentTagger, PlayerMaterial)
//
// Throwaway proto code; no settings persistence. Production rewrite happens
// during migration to client/ClassicUO.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Mobiles;
using RenderMode = ClassicUO.Renderer.Mobiles.RenderMode;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class MobileRender3DGump : Gump
    {
        public static MobileRender3DGump Instance;

        private const int W = 420;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;

        private const ushort HUE_LABEL = Debug3DStyle.HUE_LABEL_WHITE;
        private const ushort HUE_OK    = Debug3DStyle.HUE_OK;

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;
        private Label _modeLabel;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        // The mode buttons + reload buttons mutate static state that the
        // already-built Checkbox/Slider controls don't observe. Easiest fix:
        // re-spawn the gump with current values.
        private readonly IPlayer3DService _player;
        private readonly IRenderModeService _renderMode;

        private void Reopen()
        {
            var world = World;
            Dispose();
            var g = new MobileRender3DGump(world, _player, _renderMode);
            Instance = g;
            Managers.UIManager.Add(g);
        }

        public MobileRender3DGump(World world)
            : this(world, Renderer3DHost.Services.Player3D, Renderer3DHost.Services.RenderMode) { }

        public MobileRender3DGump(World world, IPlayer3DService player, IRenderModeService renderMode)
            : base(world, 0, 0)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _renderMode = renderMode ?? throw new ArgumentNullException(nameof(renderMode));
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "3D MOBILES",
                out _outerBg, out _innerBg, out _borders);
            // Honour the inner-panel border art margin (CONTENT_INSET) so
            // wide buttons like "RT debug..." don't clip the wood frame.
            int contentX = Debug3DStyle.GetContentX();
            int innerW   = Debug3DStyle.GetContentWidth(W);

            // ---------- RENDER MODE ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "RENDER MODE", y);
            int modeBtnW = (innerW - 30) / 3;
            Add(new NiceButton(contentX + 0*(modeBtnW+10), y, modeBtnW, 22, ButtonAction.Activate, "Classic 2D")  { ButtonParameter = 1 });
            Add(new NiceButton(contentX + 1*(modeBtnW+10), y, modeBtnW, 22, ButtonAction.Activate, "3D Iso")     { ButtonParameter = 2 });
            Add(new NiceButton(contentX + 2*(modeBtnW+10), y, modeBtnW, 22, ButtonAction.Activate, "Full 3D")    { ButtonParameter = 3 });
            y += ROW_H + 4;
            _modeLabel = new Label("Mode: " + _renderMode.CurrentLabel,
                true, HUE_OK, innerW - 20, 1) { X = contentX, Y = y };
            Add(_modeLabel);
            y += ROW_H + SECTION_GAP;

            // ---------- RT (per-mobile render target) ----------
            // Detail controls live in Debug3DRTGump ([rtdbg). This form keeps
            // only a single entry-point button so the RT tuning UI has one
            // canonical home and isn't duplicated across two gumps.
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "RT — PER-MOBILE 3D RENDER TARGET", y);
            Add(new NiceButton(contentX, y, innerW - 20, 22, ButtonAction.Activate,
                "RT debug (resolution / framing / SS)...")
                { ButtonParameter = 27 });
            y += ROW_H + SECTION_GAP;

            // ---------- Quick-fire anim triggers ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "ANIM  TRIGGER", y);
            int qW = (innerW - 40) / 4;
            Add(new NiceButton(contentX + 0*(qW+10), y, qW, 22, ButtonAction.Activate, "Idle")   { ButtonParameter = 10 });
            Add(new NiceButton(contentX + 1*(qW+10), y, qW, 22, ButtonAction.Activate, "Run")    { ButtonParameter = 11 });
            Add(new NiceButton(contentX + 2*(qW+10), y, qW, 22, ButtonAction.Activate, "Hit!")   { ButtonParameter = 12 });
            Add(new NiceButton(contentX + 3*(qW+10), y, qW, 22, ButtonAction.Activate, "Attack!") { ButtonParameter = 13 });
            y += ROW_H + SECTION_GAP;

            // ---------- Open satellite gumps ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "TOOLS", y);
            int btnW = (innerW - 30) / 2;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Player Mounts...")    { ButtonParameter = 20 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Model Info...") { ButtonParameter = 21 });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Equipment Tagger...") { ButtonParameter = 22 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Player Material...") { ButtonParameter = 23 });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Reload models")      { ButtonParameter = 24 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "World/Multi (Debug3D)...") { ButtonParameter = 25 });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Material Debug...") { ButtonParameter = 26 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "3D NPC Debug...") { ButtonParameter = 28 });
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case 1: _renderMode.SetMode(RenderMode.Classic2D); Reopen(); break;
                case 2: _renderMode.SetMode(RenderMode.Iso3D);     Reopen(); break;
                case 3: _renderMode.SetMode(RenderMode.Full3D);    Reopen(); break;

                case 10: _player.TriggerOneShot(PlayerAnimState.Idle, 0f); break;
                case 11: _player.TriggerOneShot(PlayerAnimState.Run, 0f); break;
                case 12: _player.TriggerOneShot(PlayerAnimState.Hit, 0.6f); break;
                case 13: _player.TriggerOneShot(PlayerAnimState.Attack, 1.2f); break;

                case 20: OpenSingleton<PlayerMountsGump>(w => new PlayerMountsGump(w)); break;
                case 21: OpenSingleton<ModelInfoGump>(w => new ModelInfoGump(w)); break;
                case 22: OpenSingleton<EquipmentTaggerGump>(w => new EquipmentTaggerGump(w)); break;
                case 23: OpenSingleton<PlayerMaterialGump>(w => new PlayerMaterialGump(w)); break;
                case 24: _player.InvalidateAll(); AttachmentRenderer.Invalidate(); break;
                case 25: OpenSingleton<Render3DLauncherGump>(w => new Render3DLauncherGump(w)); break;
                case 26: OpenSingleton<MaterialDebugGump>(w => new MaterialDebugGump(w)); break;
                case 27: OpenSingleton<Debug3DRTGump>(w => new Debug3DRTGump(w)); break;
                case 28: OpenSingleton<Npc3DDebugGump>(w => new Npc3DDebugGump(w)); break;
            }
        }

        // Generic singleton-gump opener — mirrors the case-25 pattern from
        // Debug3DGump but factored out since this gump opens 5 satellites.
        private void OpenSingleton<T>(System.Func<World, T> ctor) where T : Gump
        {
            // Per-type singleton field is set on each gump class; reflectively
            // reading them would be uglier than a switch. Just look up via
            // UIManager — if any instance is alive, bring to front.
            foreach (var g in Managers.UIManager.Gumps)
            {
                if (g is T existing && !existing.IsDisposed)
                {
                    existing.SetInScreen();
                    existing.BringOnTop();
                    return;
                }
            }
            var fresh = ctor(World);
            Managers.UIManager.Add(fresh);
        }

        public override void Update()
        {
            base.Update();
            if (_modeLabel != null)
                _modeLabel.Text = "Mode: " + _renderMode.CurrentLabel;
        }

        // Layout helper local to this gump (parallels Debug3DGump's signature so
        // future merges are obvious).
        private int AddSlider(string label, int min, int max, int value, int y,
            int contentX, int innerW, System.Action<int> onChange)
        {
            return Debug3DStyle.AddSlider(this, contentX, innerW,
                labelW: 150, label, min, max, value, y, onChange);
        }
    }
}
