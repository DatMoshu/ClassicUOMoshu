// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Thin bar at top-center of screen showing "Speaking..." while
    /// a PTT key (F5/F6/F7) is held. Shows "Speaking... MUTED" in
    /// red if mic is globally muted via F8.
    /// </summary>
    internal sealed class SpeakingBarGump : Gump
    {
        private const int WIDTH = 200;
        private const int HEIGHT = 24;
        private const int MARGIN = 8;

        private readonly Label _label;

        public SpeakingBarGump(World world) : base(world, 0, 0)
        {
            CanMove = false;
            CanCloseWithRightClick = false;
            CanCloseWithEsc = false;
            AcceptKeyboardInput = false;

            CenterHorizontally();
            Y = 4;

            var bg = new AlphaBlendControl(0.72f) { X = 0, Y = 0, Width = WIDTH, Height = HEIGHT };
            Add(bg);

            _label = new Label("Speaking...", false, 0x004F, WIDTH - MARGIN * 2, 1)
                { X = MARGIN, Y = 4 };
            Add(_label);
        }

        /// <summary>Update text/hue based on current mute state.</summary>
        public void SetMuted(bool isMuted)
        {
            if (isMuted)
            {
                _label.Text = "Speaking... MUTED";
                _label.Hue = 0x0026; // red
            }
            else
            {
                _label.Text = "Speaking...";
                _label.Hue = 0x004F; // green
            }
        }

        public override void Update()
        {
            base.Update();
            CenterHorizontally();
        }

        private void CenterHorizontally()
        {
            int screenWidth = Client.Game.Window.ClientBounds.Width;
            X = (screenWidth - WIDTH) / 2;
        }
    }
}
