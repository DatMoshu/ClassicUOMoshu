// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — dedicated form for tuning the per-mobile RT
// (MobileRT3DRenderer). Owns ALL RT-blit / RT-resolution / framing controls.
// Other gumps now link here instead of duplicating these sliders.
//
// Opened from chat with `[rtdbg`, or via the "RT debug..." button on the
// 3D Mobiles / Debug3D forms.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Mobiles;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class Debug3DRTGump : Gump
    {
        public static Debug3DRTGump Instance;

        private const int W = 460;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;
        private Label _resLabel;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        // The SS cycle button rebuilds the gump so the slider/checkbox state
        // reflects the new defaults; cheaper to respawn than to re-bind.
        private readonly IMobileRT3DService _rt;

        private void Reopen()
        {
            var world = World;
            Dispose();
            var g = new Debug3DRTGump(world, _rt);
            Instance = g;
            Managers.UIManager.Add(g);
        }

        public Debug3DRTGump(World world)
            : this(world, Renderer3DHost.Services.MobileRT3D) { }

        public Debug3DRTGump(World world, IMobileRT3DService rt) : base(world, 0, 0)
        {
            _rt = rt ?? throw new ArgumentNullException(nameof(rt));
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "3D  RT  DEBUG",
                out _outerBg, out _innerBg, out _borders);
            // Honour the inner-panel border art margin so sliders and buttons
            // don't clip the wood frame on the right edge.
            int contentX = Debug3DStyle.GetContentX();
            int innerW   = Debug3DStyle.GetContentWidth(W);

            // ---------- MASTER ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "MASTER", y);
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "RT'ed mobiles (trumps 3D)", _rt.Enabled,
                v => _rt.SetEnabled(v),
                "Show 2D player sprite", _rt.Show2DPlayerSprite,
                v => _rt.SetShow2DPlayerSprite(v));
            y += SECTION_GAP;

            // ---------- RESOLUTION ----------
            // RTWidth/RTHeight are the LOGICAL on-screen blit size. SuperSample
            // multiplies the actual RT pixel size, so the model is rendered at
            // higher density and downsampled by the batcher when blitted back
            // to logical size. Use this to find the resolution that visually
            // blends 3D against the 2D world.
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "RESOLUTION", y);
            y = AddSlider("RT logical W", 64, 512, _rt.RTWidth, y, contentX, innerW,
                v => { _rt.SetRTWidth(v); UpdateResLabel(); });
            y = AddSlider("RT logical H", 64, 512, _rt.RTHeight, y, contentX, innerW,
                v => { _rt.SetRTHeight(v); UpdateResLabel(); });
            y = AddSlider("SuperSample (1..4)", 1, 4, System.Math.Clamp(_rt.SuperSample, 1, 4),
                y, contentX, innerW,
                v => { _rt.SetSuperSample(System.Math.Clamp(v, 1, 4)); UpdateResLabel(); });

            _resLabel = new Label(BuildResString(),
                true, Debug3DStyle.HUE_OK, innerW - 20, 1) { X = contentX, Y = y };
            Add(_resLabel);
            y += ROW_H + SECTION_GAP;

            // ---------- FRAMING ----------
            // FootMarginFromBottom = pixels between the projected feet and the
            // bottom of the (logical) RT. Bigger = model sits higher. Tune so
            // the RT footprint barely extends south of the foot anchor.
            // RTYAnchorOffset = post-blit screen-Y nudge (positive = down).
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "FRAMING", y);
            y = AddSlider("Foot margin from bottom", 0, 128, _rt.FootMarginFromBottom, y, contentX, innerW,
                v => _rt.SetFootMarginFromBottom(v));
            y = AddSlider("Y anchor offset", -200, 200, _rt.RTYAnchorOffset, y, contentX, innerW,
                v => _rt.SetRTYAnchorOffset(v));
            y += SECTION_GAP;

            // ---------- LINKS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "RELATED", y);
            int btnW = (innerW - 30) / 2;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "3D Mobiles...")
                { ButtonParameter = 1 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Debug3D...")
                { ButtonParameter = 2 });
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case 1:
                    OpenSingleton<MobileRender3DGump>(w => new MobileRender3DGump(w));
                    break;
                case 2:
                    OpenSingleton<Render3DLauncherGump>(w => new Render3DLauncherGump(w));
                    break;
            }
        }

        private string BuildResString()
        {
            int ss = System.Math.Clamp(_rt.SuperSample, 1, 4);
            int aw = _rt.RTWidth * ss;
            int ah = _rt.RTHeight * ss;
            return $"Actual RT: {aw} x {ah}  ({_rt.RTWidth}x{_rt.RTHeight} blit, SS x{ss})";
        }

        private void UpdateResLabel()
        {
            if (_resLabel != null)
                _resLabel.Text = BuildResString();
        }

        public override void Update()
        {
            base.Update();
            UpdateResLabel();
        }

        private void OpenSingleton<T>(System.Func<World, T> ctor) where T : Gump
        {
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

        private int AddSlider(string label, int min, int max, int value, int y,
            int contentX, int innerW, System.Action<int> onChange)
        {
            return Debug3DStyle.AddSlider(this, contentX, innerW,
                labelW: 170, label, min, max, value, y, onChange);
        }
    }
}
