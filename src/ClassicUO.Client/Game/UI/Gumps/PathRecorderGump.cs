// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO PROTOTYPE — admin gump for PathRecorderManager / PathReplayManager.
// Record / save / play / loop / stop a player path. Use chat commands
// `[recordpath <name>` / `[playpath <name>` / `[seasonsdemo` for arbitrary names.

using System;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PathRecorderGump : Gump
    {
        public static PathRecorderGump Instance;

        private const int W = 360;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

        private const int BTN_RECORD_TOGGLE = 1;
        private const int BTN_PLAY          = 2;
        private const int BTN_STOP_PLAY     = 3;
        private const int BTN_PLAY_SEASONS  = 4;
        private const int BTN_OPEN_FOLDER   = 5;

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;
        private Label _statusLabel;
        private StbTextBox _nameBox;
        private Checkbox _loopCheck;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        public PathRecorderGump(World world) : base(world, 0, 0)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "PATH RECORDER",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW   = W - INNER_PAD * 2 - 20;

            // ---------- NAME ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "REPLAY NAME", y);
            // Black-bordered text input that says what we'll save / load.
            int boxX = contentX, boxY = y;
            int boxW = innerW;
            Add(new AlphaBlendControl(0.55f) { X = boxX, Y = boxY, Width = boxW, Height = 22 });
            Add(new Line(boxX, boxY,        boxW, 1, Debug3DStyle.BORDER_RGBA));
            Add(new Line(boxX, boxY + 21,   boxW, 1, Debug3DStyle.BORDER_RGBA));
            Add(new Line(boxX, boxY,        1, 22,    Debug3DStyle.BORDER_RGBA));
            Add(new Line(boxX + boxW - 1, boxY, 1, 22, Debug3DStyle.BORDER_RGBA));
            _nameBox = new StbTextBox(1, max_char_count: 48, maxWidth: boxW - 8,
                isunicode: true, hue: Debug3DStyle.HUE_VALUE)
            {
                X = boxX + 4, Y = boxY + 3,
                Width = boxW - 8, Height = 18,
            };
            Add(_nameBox);
            _nameBox.SetText("test1");
            y += ROW_H + 4;

            // ---------- ACTIONS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "RECORD", y);
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Toggle Record / Stop & Save")
                { ButtonParameter = BTN_RECORD_TOGGLE });
            y += ROW_H + 6;

            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "PLAYBACK", y);
            int btnW = (innerW - 6) / 2;
            Add(new NiceButton(contentX,             y, btnW, 22, ButtonAction.Activate, "Play")
                { ButtonParameter = BTN_PLAY });
            Add(new NiceButton(contentX + btnW + 6,  y, btnW, 22, ButtonAction.Activate, "Stop")
                { ButtonParameter = BTN_STOP_PLAY });
            y += ROW_H + 4;
            _loopCheck = Debug3DStyle.AddCheck(this, contentX, y, "Loop on end", PathReplayManager.Loop,
                v => PathReplayManager.Loop = v);
            y += ROW_H;

            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Play Seasons Demo (looped)")
                { ButtonParameter = BTN_PLAY_SEASONS });
            y += ROW_H + 6;

            // ---------- STATUS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "STATUS", y);
            _statusLabel = new Label("(idle)", true, Debug3DStyle.HUE_VALUE, innerW, font: 1)
                { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H * 4;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 120; Y = 80;
            WantUpdateSize = false;
        }

        public override void Update()
        {
            base.Update();
            if (_statusLabel != null && !_statusLabel.IsDisposed)
            {
                string rec = PathRecorderManager.IsRecording
                    ? $"REC '{PathRecorderManager.CurrentName}' samples={PathRecorderManager.SampleCount} t={PathRecorderManager.ElapsedMs}ms"
                    : $"rec idle  ({PathRecorderManager.SampleCount} buffered)";
                string play = PathReplayManager.IsPlaying
                    ? $"PLAY '{PathReplayManager.CurrentName}' i={PathReplayManager.LastSampleIdx} t={PathReplayManager.ElapsedMs}ms loop={PathReplayManager.Loop}"
                    : "play idle";
                _statusLabel.Text = rec + "\n" + play;
                _statusLabel.Hue = (PathRecorderManager.IsRecording || PathReplayManager.IsPlaying)
                    ? Debug3DStyle.HUE_OK : Debug3DStyle.HUE_LABEL_WHITE;
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); break;

                case BTN_RECORD_TOGGLE:
                {
                    if (PathRecorderManager.IsRecording)
                    {
                        string file = PathRecorderManager.StopAndSave();
                        Console.WriteLine("[PathRecorder] " + file);
                        GameActions.Print(World, "[recordpath] saved to " + file);
                    }
                    else
                    {
                        string n = string.IsNullOrWhiteSpace(_nameBox.Text) ? "test1" : _nameBox.Text.Trim();
                        PathRecorderManager.StartRecording(n);
                        GameActions.Print(World, "[recordpath] recording '" + n + "'");
                    }
                    break;
                }
                case BTN_PLAY:
                {
                    string n = string.IsNullOrWhiteSpace(_nameBox.Text) ? "test1" : _nameBox.Text.Trim();
                    if (PathReplayManager.Load(n))
                    {
                        PathReplayManager.Start(World, _loopCheck != null && _loopCheck.IsChecked);
                        GameActions.Print(World, "[playpath] " + n);
                    }
                    else
                    {
                        GameActions.Print(World, "[playpath] not found: " + n);
                    }
                    break;
                }
                case BTN_STOP_PLAY:
                    PathReplayManager.Stop();
                    break;
                case BTN_PLAY_SEASONS:
                {
                    if (PathReplayManager.Load("seasons-demo"))
                    {
                        PathReplayManager.Start(World, true);
                        GameActions.Print(World, "[seasonsdemo] started");
                    }
                    else
                    {
                        GameActions.Print(World, "[seasonsdemo] missing Data/Replays/seasons-demo.json");
                    }
                    break;
                }
            }
        }
    }
}
