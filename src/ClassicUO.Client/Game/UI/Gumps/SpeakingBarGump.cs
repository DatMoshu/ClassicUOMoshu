// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Voice transmit indicator anchored just above the chat input at the
    /// bottom-left of the game viewport. Visible only while a PTT key
    /// (F5/F6/F7) is held. Text switches to red "MUTED" if mic is globally
    /// muted via F8.
    ///
    /// Styled to match SystemChatControl: 0.5 alpha gradient background,
    /// unicode chat font with BlackBorder for readability on any scene.
    /// </summary>
    internal sealed class SpeakingBarGump : Gump
    {
        private const int WIDTH = 220;
        private const int HEIGHT = 26;
        private const int PAD_X = 10;
        // Distance from the viewport bottom edge to the bar's top. Positive =
        // below viewport; negative = overlapping viewport (sits above chat).
        private const int OFFSET_FROM_VIEWPORT_BOTTOM = -96;

        // Hues chosen to match UO chat palette: bright green / bright red.
        private const ushort HUE_SPEAKING = 0x0044; // bright green
        private const ushort HUE_MUTED    = 0x0026; // bright red

        private readonly AlphaBlendControl _bg;
        private readonly Label _label;

        public SpeakingBarGump(World world) : base(world, 0, 0)
        {
            CanMove = false;
            CanCloseWithRightClick = false;
            CanCloseWithEsc = false;
            AcceptKeyboardInput = false;
            AcceptMouseInput = false;
            LayerOrder = UILayer.Over;

            Width = WIDTH;
            Height = HEIGHT;

            // Translucent dark background — same gradient used by SystemChatControl.
            _bg = new AlphaBlendControl(0.5f)
            {
                X = 0,
                Y = 0,
                Width = WIDTH,
                Height = HEIGHT,
            };
            Add(_bg);

            // Unicode label matching the chat font, with BlackBorder for contrast
            // against bright terrain. Centered horizontally inside the bar.
            byte chatFont = ProfileManager.CurrentProfile?.ChatFont ?? 1;
            _label = new Label(
                "● Speaking...",
                isunicode: true,
                hue: HUE_SPEAKING,
                maxwidth: WIDTH - PAD_X * 2,
                font: chatFont,
                style: FontStyle.BlackBorder,
                align: TEXT_ALIGN_TYPE.TS_CENTER
            )
            {
                X = PAD_X,
            };
            CenterLabelVertically();
            Add(_label);

            AnchorToChat();
        }

        /// <summary>Update text/hue based on current mute state.</summary>
        public void SetMuted(bool isMuted)
        {
            if (isMuted)
            {
                _label.Text = "● Speaking — MUTED";
                _label.Hue = HUE_MUTED;
            }
            else
            {
                _label.Text = "● Speaking...";
                _label.Hue = HUE_SPEAKING;
            }

            CenterLabelVertically();
        }

        public override void Update()
        {
            base.Update();
            AnchorToChat();
        }

        /// <summary>
        /// Position the bar at the left edge of the game viewport, just above
        /// the chat input. Falls back to bottom-left of the screen if the
        /// viewport gump is not available yet. The final position is clamped
        /// to the visible window so the bar is never offscreen — important
        /// when multiple clients are open with different viewport sizes.
        /// </summary>
        private void AnchorToChat()
        {
            int targetX, targetY;

            var viewport = UIManager.GetGump<WorldViewportGump>();
            if (viewport != null)
            {
                targetX = viewport.X + WorldViewportGump.BORDER_WIDTH;
                targetY = viewport.Y + viewport.Height + OFFSET_FROM_VIEWPORT_BOTTOM;
            }
            else
            {
                targetX = 4;
                targetY = Client.Game.Window.ClientBounds.Height - HEIGHT - 4;
            }

            // Clamp to visible window bounds so the bar never renders offscreen.
            int winW = Client.Game.Window.ClientBounds.Width;
            int winH = Client.Game.Window.ClientBounds.Height;
            if (targetX < 0) targetX = 0;
            if (targetY < 0) targetY = 0;
            if (targetX + WIDTH > winW) targetX = winW - WIDTH;
            if (targetY + HEIGHT > winH) targetY = winH - HEIGHT;

            X = targetX;
            Y = targetY;
        }

        private void CenterLabelVertically()
        {
            // Label height depends on the rendered text; center it within the bar.
            int labelH = _label.Height > 0 ? _label.Height : HEIGHT;
            _label.Y = (HEIGHT - labelH) / 2;
        }
    }
}
