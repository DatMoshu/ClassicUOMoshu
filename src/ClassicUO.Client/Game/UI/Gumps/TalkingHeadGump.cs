// SPDX-License-Identifier: BSD-2-Clause
//
// TalkingHeadGump — animated NPC portrait that cycles through 9 phoneme
// frames while a Qwen-synthesised voice line plays. Frame swap is done by
// reassigning GumpPic.Graphic in Update(), which only updates the internal
// texture reference. No InvalidateContents / UpdateContents() rebuild is
// triggered, so animation is smooth and avoids the "gump refresh" flicker
// pattern that affects fully-redrawn dynamic gumps.
//
// Lifecycle:
//   1. Server sends a packet describing the persona (base gump id, frame
//      count, voice file path, audio duration).
//   2. PacketHandlers opens (or reuses) one TalkingHeadGump keyed by the
//      NPC's mobile serial — replacing any previous instance.
//   3. The gump calls NpcVoiceManager.Instance.Play(voicePath), records the
//      start tick, and cycles frames 1-(N-1) at audio_ms / N intervals.
//   4. When audio elapses or the player closes the X, the portrait holds on
//      frame 0 (rest) and stops cycling. The gump itself stays visible for
//      `LingerAfterAudioMs` so the player sees the final rest pose, then
//      disposes itself unless re-triggered.
//
// See design/Core/GameDesign/npcs/talking-head-npcs.md for the full spec
// and design/Core/Architecture/decisions/ADR-013-card-art-gump-ids.md for
// the gump-id allocation rules (LB occupies 0xC300-0xC308).

using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class TalkingHeadGump : Gump
    {
        // Tuning knobs — see GDD §Tuning Knobs.
        private const int FrameMinMs        = 60;
        private const int FrameMaxMs        = 250;
        private const int LingerAfterAudioMs = 1500;
        private const int BtnClose          = 1;

        private readonly GumpPic _portrait;
        private readonly ushort _baseGumpId;
        private readonly int _frameCount;
        private readonly int _audioDurationMs;
        private readonly int _frameIntervalMs;
        private readonly uint _startTick;
        private uint _endTick;
        private bool _animating;

        public TalkingHeadGump(
            World world,
            uint mobileSerial,
            ushort baseGumpId,
            int frameCount,
            string voicePath
        )
            : base(world, mobileSerial, 0)
        {
            _baseGumpId = baseGumpId;
            _frameCount = frameCount > 0 ? frameCount : 9;

            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            LayerOrder = UILayer.Over;

            // Frame 0 = rest. Show first, then animation flips us forward.
            _portrait = new GumpPic(0, 0, _baseGumpId, 0);
            Add(_portrait);

            Width  = _portrait.Width;
            Height = _portrait.Height;

            // Close X in the upper-right of the portrait region.
            Add(new Button(BtnClose, 0x00FB, 0x00FC, 0x00FD)
            {
                X = Width - 24,
                Y = 4,
                ButtonAction = ButtonAction.Activate,
            });

            // Drop into corner of the screen by default — gump is moveable
            // so the player can drag it.
            X = 32;
            Y = 32;

            // Play the audio first; the manager returns the decoded clip's
            // duration in ms. Using that as the animation window guarantees
            // the gump closes when the audio actually ends, regardless of
            // whether the server knew the duration at send time.
            int playedMs = 0;
            if (!string.IsNullOrEmpty(voicePath))
            {
                playedMs = NpcVoiceManager.Instance.Play(voicePath);
            }
            _audioDurationMs = playedMs > 0 ? playedMs : 1000;
            _frameIntervalMs = ClampFrameInterval(_audioDurationMs / _frameCount);

            _startTick = Time.Ticks;
            _endTick   = _startTick + (uint)_audioDurationMs;
            _animating = true;
        }

        private static int ClampFrameInterval(int v)
        {
            if (v < FrameMinMs) return FrameMinMs;
            if (v > FrameMaxMs) return FrameMaxMs;
            return v;
        }

        public override void Update()
        {
            base.Update();
            if (!_animating)
            {
                // Linger on rest frame so the gump doesn't pop away the
                // instant audio ends — gives the player a beat to read
                // the portrait before it closes itself.
                if (Time.Ticks > _endTick + LingerAfterAudioMs)
                {
                    Dispose();
                }
                return;
            }

            uint now = Time.Ticks;
            if (now >= _endTick)
            {
                // Audio done — hold at rest frame and start linger timer.
                _portrait.Graphic = _baseGumpId;
                _animating = false;
                _endTick = now; // reuse as linger anchor
                return;
            }

            uint elapsed = now - _startTick;
            // Frames 1..N-1 are "talking" frames; frame 0 (rest) is reserved
            // for the start/end pose so the mouth never freezes mid-phoneme.
            int talkingFrames = _frameCount - 1;
            if (talkingFrames < 1) return;
            int idx = 1 + (int)((elapsed / (uint)_frameIntervalMs) % (uint)talkingFrames);
            ushort target = (ushort)(_baseGumpId + idx);
            if (_portrait.Graphic != target)
            {
                _portrait.Graphic = target;
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == BtnClose)
            {
                // Close visually but don't cut audio mid-word — per GDD,
                // walking away / clicking X should let audio finish.
                Dispose();
                return;
            }
            base.OnButtonClick(buttonID);
        }
    }
}
