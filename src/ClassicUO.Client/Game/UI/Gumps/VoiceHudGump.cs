// VoiceHudGump.cs — Small draggable HUD showing current voice transmit state.
// Green=Proximity, Blue=Faction, Yellow=Guild, Grey=Muted, Dim=Idle.

using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using System.Xml;

namespace ClassicUO.Game.UI.Gumps
{
    internal class VoiceHudGump : Gump
    {
        private static Point _lastPosition = new Point(-1, -1);

        private readonly AlphaBlendControl _background;
        private readonly Label _label;
        private VoiceChannel _lastChannel = VoiceChannel.None;
        private bool _lastMuted;

        public VoiceHudGump(World world, int x, int y) : base(world, 0, 0)
        {
            CanMove = true;
            CanCloseWithEsc = false;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            AcceptKeyboardInput = false;

            X = _lastPosition.X <= 0 ? x : _lastPosition.X;
            Y = _lastPosition.Y <= 0 ? y : _lastPosition.Y;
            Width = 90;
            Height = 24;

            Add(
                _background = new AlphaBlendControl(.7f)
                {
                    Width = Width,
                    Height = Height
                }
            );

            Add(
                _label = new Label("\u2014", true, 0x0384, Width, 0xFF, FontStyle.BlackBorder)
                {
                    X = 4,
                    Y = 3
                }
            );

            LayerOrder = UILayer.Over;
            WantUpdateSize = false;

            // Subscribe to VivoxManager state changes
            if (World.VivoxManager != null)
            {
                World.VivoxManager.VoiceStateChanged += UpdateDisplay;
            }

            UpdateDisplay();
        }

        public override GumpType GumpType => GumpType.None;

        private void UpdateDisplay()
        {
            var manager = World.VivoxManager;
            if (manager == null)
            {
                _label.Text = "No Voice";
                _label.Hue = 0x0384; // Grey
                return;
            }

            if (manager.IsMicMuted)
            {
                _label.Text = "MUTED";
                _label.Hue = 0x0384; // Grey
            }
            else
            {
                switch (manager.ActiveChannel)
                {
                    case VoiceChannel.Proximity:
                        _label.Text = "PROX";
                        _label.Hue = 0x0040; // Green
                        break;
                    case VoiceChannel.Faction:
                        _label.Text = "FACTION";
                        _label.Hue = 0x054C; // Blue
                        break;
                    case VoiceChannel.Guild:
                        _label.Text = "GUILD";
                        _label.Hue = 0x0035; // Gold/Yellow
                        break;
                    default:
                        _label.Text = "\u2014"; // Em-dash for idle
                        _label.Hue = 0x0384; // Dim grey
                        break;
                }
            }
        }

        public override void Update()
        {
            base.Update();

            // Check for state changes without relying solely on events
            var manager = World.VivoxManager;
            if (manager != null && (_lastChannel != manager.ActiveChannel || _lastMuted != manager.IsMicMuted))
            {
                _lastChannel = manager.ActiveChannel;
                _lastMuted = manager.IsMicMuted;
                UpdateDisplay();
            }
        }

        public override void Dispose()
        {
            _lastPosition = Location;

            if (World.VivoxManager != null)
            {
                World.VivoxManager.VoiceStateChanged -= UpdateDisplay;
            }

            base.Dispose();
        }

        public override void Save(XmlTextWriter writer)
        {
            base.Save(writer);
            writer.WriteAttributeString("x", X.ToString());
            writer.WriteAttributeString("y", Y.ToString());
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml);

            if (int.TryParse(xml.GetAttribute("x"), out int x))
            {
                X = x;
            }

            if (int.TryParse(xml.GetAttribute("y"), out int y))
            {
                Y = y;
            }
        }
    }
}
