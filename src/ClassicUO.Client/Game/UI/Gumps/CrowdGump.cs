// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — admin tuning gump for CrowdMimicManager (fake-crowd
// ambience). Tunes voice count, durations, gap/spacing, volume range, then
// Start/Stop. Live status shows whether the crowd is running and how many
// voices are active. Same shell theme as the other 3D debug gumps.
//
// Open with `[crowdgump`. Code-only proto — no settings persistence.

using System;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class CrowdGump : Gump
    {
        public static CrowdGump Instance;

        private const int W = 460;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;
        private const int LABEL_W     = 170;

        private const int BTN_START = 1;
        private const int BTN_STOP  = 2;
        private const int BTN_DUMP  = 3;
        private const int BTN_REFRESH = 4;

        private Label _statusLabel;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        public CrowdGump(World world) : base(world, 0, 0)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "CROWD CHATTER",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- Section: VOICES ----------
            y = AddSectionHeader("VOICES", y);
            y = AddSlider("Voice count",     1, 16,   CrowdMimicManager.VoiceCount,         y, v => CrowdMimicManager.VoiceCount = v);
            y = AddSlider("Duration (s)",    5, 600,  CrowdMimicManager.DurationSec,        y, v => CrowdMimicManager.DurationSec = v);
            y += SECTION_GAP;

            // ---------- Section: SPACING ----------
            // The big knob for "too many at once" is MinIntervoiceGapMs — the
            // global gate that prevents two voices firing within Xms of each
            // other. Per-voice gap is the pause each voice takes after its own
            // clip ends; bumping that thins the clumps further.
            y = AddSectionHeader("SPACING", y);
            y = AddSlider("Inter-voice min gap (ms)", 0, 3000, CrowdMimicManager.MinIntervoiceGapMs, y, v => CrowdMimicManager.MinIntervoiceGapMs = v);
            y = AddSlider("Per-voice gap min (ms)",   0, 5000, CrowdMimicManager.GapMinMs,           y, v => CrowdMimicManager.GapMinMs = v);
            y = AddSlider("Per-voice gap max (ms)",   1, 8000, CrowdMimicManager.GapMaxMs,           y, v => CrowdMimicManager.GapMaxMs = v);
            y = AddSlider("Initial stagger (ms)",     0, 5000, CrowdMimicManager.InitialStaggerMs,   y, v => CrowdMimicManager.InitialStaggerMs = v);
            y += SECTION_GAP;

            // ---------- Section: VOLUME ----------
            // Sliders carry x100 so we can use ints.
            y = AddSectionHeader("VOLUME", y);
            y = AddSlider("Volume min (x100)", 0, 100, (int)Math.Round(CrowdMimicManager.VolumeMin * 100), y, v => CrowdMimicManager.VolumeMin = v / 100f);
            y = AddSlider("Volume max (x100)", 0, 100, (int)Math.Round(CrowdMimicManager.VolumeMax * 100), y, v => CrowdMimicManager.VolumeMax = v / 100f);
            y += SECTION_GAP;

            // ---------- Section: ACTIONS ----------
            y = AddSectionHeader("ACTIONS", y);
            int btnW = (innerW - 30) / 2;
            Add(new NiceButton(contentX,                y, btnW, 22, ButtonAction.Activate, "Start") { ButtonParameter = BTN_START });
            Add(new NiceButton(contentX + btnW + 10,    y, btnW, 22, ButtonAction.Activate, "Stop")  { ButtonParameter = BTN_STOP });
            y += ROW_H + 4;
            Add(new NiceButton(contentX,                y, btnW, 22, ButtonAction.Activate, "Refresh")        { ButtonParameter = BTN_REFRESH });
            Add(new NiceButton(contentX + btnW + 10,    y, btnW, 22, ButtonAction.Activate, "Dump State")     { ButtonParameter = BTN_DUMP });
            y += ROW_H + SECTION_GAP;

            // ---------- Section: STATUS ----------
            y = AddSectionHeader("STATUS", y);
            _statusLabel = new Label("(status)", true, Debug3DStyle.HUE_VALUE, innerW - 20, 1) { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H * 3 + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 80;
            Y = 80;
            WantUpdateSize = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE:
                    Dispose();
                    break;

                case BTN_START:
                {
                    string msg = CrowdMimicManager.Start();
                    GameActions.Print(World, "[crowd] " + msg);
                    break;
                }

                case BTN_STOP:
                    CrowdMimicManager.Stop();
                    GameActions.Print(World, "[crowd] stopped");
                    break;

                case BTN_REFRESH:
                {
                    // Reopen with the latest tunable values displayed.
                    var w = World;
                    Dispose();
                    var g = new CrowdGump(w);
                    Instance = g;
                    Managers.UIManager.Add(g);
                    break;
                }

                case BTN_DUMP:
                    Console.WriteLine(BuildDumpString());
                    GameActions.Print(World, "[crowd] state dumped to console");
                    break;
            }
        }

        public override void Update()
        {
            base.Update();
            if (_statusLabel != null)
            {
                bool running = CrowdMimicManager.IsRunning;
                _statusLabel.Hue = running ? Debug3DStyle.HUE_OK : Debug3DStyle.HUE_LABEL_WHITE;
                _statusLabel.Text =
                    $"running   {(running ? "YES" : "no")}\n" +
                    $"voices    {CrowdMimicManager.ActiveVoices}/{CrowdMimicManager.VoiceCount}\n" +
                    $"clips     {CrowdMimicManager.LoadedClips}   folder: {CrowdMimicManager.FolderRel}";
            }
        }

        // ---- Layout helpers ----

        private int AddSectionHeader(string title, int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSectionHeader(this, contentX, innerW, title, y);
        }

        private int AddSlider(string label, int min, int max, int value, int y, Action<int> onChange)
        {
            int contentX = INNER_PAD + 10;
            int rowW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSlider(this, contentX, rowW, LABEL_W,
                label, min, max, value, y, onChange);
        }

        public static string BuildDumpString()
        {
            return
                "==================== [3DCUO] CROWD STATE ====================\n" +
                $"  IsRunning           = {CrowdMimicManager.IsRunning}\n" +
                $"  ActiveVoices        = {CrowdMimicManager.ActiveVoices}\n" +
                $"  LoadedClips         = {CrowdMimicManager.LoadedClips}\n" +
                $"  FolderRel           = {CrowdMimicManager.FolderRel}\n" +
                $"  VoiceCount          = {CrowdMimicManager.VoiceCount}\n" +
                $"  DurationSec         = {CrowdMimicManager.DurationSec}\n" +
                $"  MinIntervoiceGapMs  = {CrowdMimicManager.MinIntervoiceGapMs}\n" +
                $"  GapMinMs / GapMaxMs = {CrowdMimicManager.GapMinMs} / {CrowdMimicManager.GapMaxMs}\n" +
                $"  InitialStaggerMs    = {CrowdMimicManager.InitialStaggerMs}\n" +
                $"  VolumeMin / Max     = {CrowdMimicManager.VolumeMin:F2} / {CrowdMimicManager.VolumeMax:F2}\n";
        }
    }
}
