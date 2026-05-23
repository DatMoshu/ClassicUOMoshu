// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO PROTOTYPE — bottom-center subtitle/caption overlay used by
// PathReplayManager to narrate over scripted player movement.

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class CinematicCaptionGump : Gump
    {
        public static CinematicCaptionGump Instance;

        private const int WIDTH    = 800;
        private const int HEIGHT   = 64;
        private const int PAD_X    = 16;
        private const int BOTTOM_OFFSET = 80;

        private readonly AlphaBlendControl _bg;
        private readonly Label _label;
        private long _hideAtTicks;

        public CinematicCaptionGump(World world) : base(world, 0, 0)
        {
            CanMove = false;
            CanCloseWithRightClick = false;
            CanCloseWithEsc = false;
            AcceptKeyboardInput = false;
            AcceptMouseInput = false;
            LayerOrder = UILayer.Over;

            Width = WIDTH;
            Height = HEIGHT;

            _bg = new AlphaBlendControl(0.55f)
            {
                X = 0, Y = 0, Width = WIDTH, Height = HEIGHT,
            };
            Add(_bg);

            byte chatFont = ProfileManager.CurrentProfile?.ChatFont ?? 1;
            _label = new Label(
                "",
                isunicode: true,
                hue: 1153, // bright white
                maxwidth: WIDTH - PAD_X * 2,
                font: chatFont,
                style: FontStyle.BlackBorder,
                align: TEXT_ALIGN_TYPE.TS_CENTER
            )
            {
                X = PAD_X, Y = 8,
            };
            Add(_label);
        }

        public static void ShowCaption(World world, string text, int durationMs)
        {
            EnsureInstance(world);
            if (Instance == null) return;
            Instance._label.Text = text ?? "";
            Instance._hideAtTicks = (long)Time.Ticks + durationMs;
            Instance.IsVisible = true;
            Instance.Reposition();
        }

        public static void HideNow()
        {
            if (Instance == null) return;
            Instance._label.Text = "";
            Instance.IsVisible = false;
        }

        private static void EnsureInstance(World world)
        {
            if (Instance == null || Instance.IsDisposed)
            {
                var g = new CinematicCaptionGump(world);
                Instance = g;
                UIManager.Add(g);
            }
            else
            {
                Instance.BringOnTop();
            }
        }

        public override void Update()
        {
            base.Update();
            if (IsVisible && Time.Ticks >= _hideAtTicks)
            {
                _label.Text = "";
                IsVisible = false;
            }
            Reposition();
        }

        private void Reposition()
        {
            int winW = Client.Game.Window.ClientBounds.Width;
            int winH = Client.Game.Window.ClientBounds.Height;
            X = (winW - WIDTH) / 2;
            Y = winH - HEIGHT - BOTTOM_OFFSET;
            if (X < 0) X = 0;
            if (Y < 0) Y = 0;
        }

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }
    }
}
