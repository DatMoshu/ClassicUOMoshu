// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment & Lighting controls (clear color, fog,
// skybox preview). Lives alongside CameraGump (which keeps camera
// transform / fog sliders). This gump owns the *look* of the world's
// empty space: clear color, sky gradient, and (eventually) cubemap.
//
// MIGRATION (ADR-012 §6 / playbook §Q): constructor-injected IEnvironmentService.
// All 53 World3DRenderer.{BackgroundColor, FogX, SkyX} touch points migrated.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Environment;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class EnvironmentLightingGump : Gump
    {
        public static EnvironmentLightingGump Instance;

        private readonly IEnvironmentService _env;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private void Reopen()
        {
            var w = World;
            Dispose();
            var g = new EnvironmentLightingGump(w, _env);
            Instance = g;
            Game.Managers.UIManager.Add(g);
        }

        private const int W = 460;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;
        private const int LABEL_W   = 160;

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        // Button IDs — local namespace, won't collide with Debug3DStyle
        // shared button ids (close=9990, dump=9991).
        private const int BTN_PRESET_BLACK   = 1;
        private const int BTN_PRESET_SKY     = 2;
        private const int BTN_PRESET_NIGHT   = 3;
        private const int BTN_PRESET_DUSK    = 4;
        private const int BTN_REFRESH        = 5;
        private const int BTN_SKY_OFF        = 10;
        private const int BTN_SKY_GRADIENT   = 11;
        private const int BTN_SKY_CUBEMAP    = 12;

        /// <summary>
        /// Convenience overload that resolves <see cref="IEnvironmentService"/> from the
        /// active renderer service container.
        /// </summary>
        public EnvironmentLightingGump(World world)
            : this(world, Renderer3DHost.Services.Environment) { }

        public EnvironmentLightingGump(World world, IEnvironmentService env) : base(world, 0, 0)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "ENVIRONMENT  +  LIGHTING",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- Clear / background color ----------
            y = AddSectionHeader("CLEAR COLOR (camera default)", y);
            y = AddSlider("BG R", 0, 255, _env.BackgroundColor.R, y, v =>
            {
                var c = _env.BackgroundColor;
                _env.SetBackgroundColor(new Color((byte)v, c.G, c.B, c.A));
            });
            y = AddSlider("BG G", 0, 255, _env.BackgroundColor.G, y, v =>
            {
                var c = _env.BackgroundColor;
                _env.SetBackgroundColor(new Color(c.R, (byte)v, c.B, c.A));
            });
            y = AddSlider("BG B", 0, 255, _env.BackgroundColor.B, y, v =>
            {
                var c = _env.BackgroundColor;
                _env.SetBackgroundColor(new Color(c.R, c.G, (byte)v, c.A));
            });

            // Preset row — quick swap between common defaults.
            int btnW = (innerW - 20) / 4;
            Add(new NiceButton(contentX,                   y, btnW, 22, ButtonAction.Activate, "Black")
                { ButtonParameter = BTN_PRESET_BLACK });
            Add(new NiceButton(contentX + (btnW +  6) * 1, y, btnW, 22, ButtonAction.Activate, "Sky")
                { ButtonParameter = BTN_PRESET_SKY });
            Add(new NiceButton(contentX + (btnW +  6) * 2, y, btnW, 22, ButtonAction.Activate, "Night")
                { ButtonParameter = BTN_PRESET_NIGHT });
            Add(new NiceButton(contentX + (btnW +  6) * 3, y, btnW, 22, ButtonAction.Activate, "Dusk")
                { ButtonParameter = BTN_PRESET_DUSK });
            y += ROW_H + 4;
            y += SECTION_GAP;

            // ---------- Fog ----------
            y = AddSectionHeader("FOG", y);
            y = AddCheck("Fog enabled", _env.FogEnabled,
                v => _env.SetFogEnabled(v), y);
            y = AddSlider("Fog start", 0, 8192, (int)_env.FogStart, y,
                v => _env.SetFogStart(v));
            y = AddSlider("Fog end",   0, 16384, (int)_env.FogEnd, y,
                v => _env.SetFogEnd(v));
            y = AddSlider("Fog R", 0, 255, _env.FogColor.R, y, v =>
            {
                var c = _env.FogColor; _env.SetFogColor(new Color((byte)v, c.G, c.B, c.A));
            });
            y = AddSlider("Fog G", 0, 255, _env.FogColor.G, y, v =>
            {
                var c = _env.FogColor; _env.SetFogColor(new Color(c.R, (byte)v, c.B, c.A));
            });
            y = AddSlider("Fog B", 0, 255, _env.FogColor.B, y, v =>
            {
                var c = _env.FogColor; _env.SetFogColor(new Color(c.R, c.G, (byte)v, c.A));
            });
            y += SECTION_GAP;

            // ---------- Skybox ----------
            y = AddSectionHeader("SKYBOX", y);

            // Mode picker — three buttons. Cubemap is reserved (no asset wired yet).
            int modeBtnW = (innerW - 20) / 3;
            Add(new NiceButton(contentX,                       y, modeBtnW, 22, ButtonAction.Activate,
                BuildModeLabel("Off",      SkyboxMode.Off))
                { ButtonParameter = BTN_SKY_OFF });
            Add(new NiceButton(contentX + (modeBtnW + 10) * 1, y, modeBtnW, 22, ButtonAction.Activate,
                BuildModeLabel("Gradient", SkyboxMode.Gradient))
                { ButtonParameter = BTN_SKY_GRADIENT });
            Add(new NiceButton(contentX + (modeBtnW + 10) * 2, y, modeBtnW, 22, ButtonAction.Activate,
                BuildModeLabel("Cubemap",  SkyboxMode.Cubemap))
                { ButtonParameter = BTN_SKY_CUBEMAP });
            y += ROW_H + 4;

            y = AddSlider("Sky Top R", 0, 255, _env.SkyTopColor.R, y, v =>
            {
                var c = _env.SkyTopColor; _env.SetSkyTopColor(new Color((byte)v, c.G, c.B, c.A));
            });
            y = AddSlider("Sky Top G", 0, 255, _env.SkyTopColor.G, y, v =>
            {
                var c = _env.SkyTopColor; _env.SetSkyTopColor(new Color(c.R, (byte)v, c.B, c.A));
            });
            y = AddSlider("Sky Top B", 0, 255, _env.SkyTopColor.B, y, v =>
            {
                var c = _env.SkyTopColor; _env.SetSkyTopColor(new Color(c.R, c.G, (byte)v, c.A));
            });
            y = AddSlider("Sky Horizon R", 0, 255, _env.SkyHorizonColor.R, y, v =>
            {
                var c = _env.SkyHorizonColor; _env.SetSkyHorizonColor(new Color((byte)v, c.G, c.B, c.A));
            });
            y = AddSlider("Sky Horizon G", 0, 255, _env.SkyHorizonColor.G, y, v =>
            {
                var c = _env.SkyHorizonColor; _env.SetSkyHorizonColor(new Color(c.R, (byte)v, c.B, c.A));
            });
            y = AddSlider("Sky Horizon B", 0, 255, _env.SkyHorizonColor.B, y, v =>
            {
                var c = _env.SkyHorizonColor; _env.SetSkyHorizonColor(new Color(c.R, c.G, (byte)v, c.A));
            });
            y = AddSlider("Sky Bottom R", 0, 255, _env.SkyBottomColor.R, y, v =>
            {
                var c = _env.SkyBottomColor; _env.SetSkyBottomColor(new Color((byte)v, c.G, c.B, c.A));
            });
            y = AddSlider("Sky Bottom G", 0, 255, _env.SkyBottomColor.G, y, v =>
            {
                var c = _env.SkyBottomColor; _env.SetSkyBottomColor(new Color(c.R, (byte)v, c.B, c.A));
            });
            y = AddSlider("Sky Bottom B", 0, 255, _env.SkyBottomColor.B, y, v =>
            {
                var c = _env.SkyBottomColor; _env.SetSkyBottomColor(new Color(c.R, c.G, (byte)v, c.A));
            });
            y = AddSlider("Horizon line (x100)", 0, 100,
                (int)(_env.SkyHorizonY * 100f), y,
                v => _env.SetSkyHorizonY(v / 100f));

            // Note row — explains what each mode means / what's wired.
            y += 2;
            Add(new Label(
                "Off       = clear to BG color only.\n" +
                "Gradient  = top→horizon→bottom screen-space dome (procedural, no textures).\n" +
                "Cubemap   = sample a TextureCube — drop 6 PNGs in Data/Skybox/ to enable.",
                isunicode: true, hue: Debug3DStyle.HUE_VALUE, maxwidth: innerW, font: 1)
                { X = contentX, Y = y });
            y += ROW_H * 3 + 4;
            y += SECTION_GAP;

            // ---------- Footer ----------
            y = Debug3DStyle.BeginFooter(this, contentX, innerW, y);
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Refresh form")
                { ButtonParameter = BTN_REFRESH });
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 880;
            Y = 60;
            WantUpdateSize = false;
        }

        // Non-static so it can read injected _env.
        private string BuildModeLabel(string name, SkyboxMode m)
            => _env.SkyMode == m ? "[" + name + "]" : name;

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE:
                    Dispose();
                    break;

                case BTN_PRESET_BLACK:
                    _env.SetBackgroundColor(Color.Black);
                    Reopen();
                    break;
                case BTN_PRESET_SKY:
                    _env.SetBackgroundColor(new Color(70, 130, 180, 255));
                    Reopen();
                    break;
                case BTN_PRESET_NIGHT:
                    _env.SetBackgroundColor(new Color(8, 12, 32, 255));
                    Reopen();
                    break;
                case BTN_PRESET_DUSK:
                    _env.SetBackgroundColor(new Color(180, 90, 40, 255));
                    Reopen();
                    break;

                case BTN_SKY_OFF:
                    _env.SetSkyMode(SkyboxMode.Off);
                    Reopen();
                    break;
                case BTN_SKY_GRADIENT:
                    _env.SetSkyMode(SkyboxMode.Gradient);
                    Reopen();
                    break;
                case BTN_SKY_CUBEMAP:
                    _env.SetSkyMode(SkyboxMode.Cubemap);
                    Reopen();
                    break;

                case BTN_REFRESH:
                    Reopen();
                    break;
            }
        }

        // ----- shared helpers -----
        private int AddSectionHeader(string title, int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSectionHeader(this, contentX, innerW, title, y);
        }

        private int AddCheck(string label, bool init, Action<bool> on, int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                label, init, on, "", false, null);
        }

        private int AddSlider(string label, int min, int max, int value, int y, Action<int> onChange)
        {
            int contentX = INNER_PAD + 10;
            int rowW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSlider(this, contentX, rowW, LABEL_W,
                label, min, max, value, y, onChange);
        }
    }
}
