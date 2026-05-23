// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — equipment-driven slot swap toggle + status, split
// out from Debug3DGump's EQUIPMENT → SLOTS section. Tiny gump on
// purpose; this is one toggle plus a live counter.

using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class EquipmentSlotsGump : Gump
    {
        public static EquipmentSlotsGump Instance;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private const int W = 360;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

        private Label _statusLabel;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        public EquipmentSlotsGump(World world) : base(world, 0, 0)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;
            Instance = this;

            int y = Debug3DStyle.BuildShell(this, W, "EQUIPMENT → SLOTS",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "AUTO-APPLY", y);
            Debug3DStyle.AddCheck(this, contentX, y,
                "Force slots from equipment",
                AttachmentRenderer.AutoFromEquipment,
                v => AttachmentRenderer.AutoFromEquipment = v);
            y += ROW_H;

            _statusLabel = new Label("(loading…)", true, Debug3DStyle.HUE_LABEL_WHITE,
                innerW, 2) { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H * 2 + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 280;
            Y = 60;
            WantUpdateSize = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == Debug3DStyle.BTN_CLOSE) Dispose();
        }

        public override void Update()
        {
            base.Update();
            if (_statusLabel == null) return;

            if (!AttachmentRenderer.AutoFromEquipment)
            {
                _statusLabel.Text = $"map     {EquipmentSidekickMap.LoadedCount} items loaded  (toggle off)";
                _statusLabel.Hue = Debug3DStyle.HUE_LABEL_WHITE;
            }
            else if (!string.IsNullOrEmpty(AttachmentRenderer.LastAutoApplyError))
            {
                _statusLabel.Text = $"auto    ERR  {AttachmentRenderer.LastAutoApplyError}";
                _statusLabel.Hue = Debug3DStyle.HUE_WARN;
            }
            else
            {
                _statusLabel.Text =
                    $"auto    items {AttachmentRenderer.LastAutoAppliedItems}  slots {AttachmentRenderer.LastAutoAppliedSlots}\n" +
                    $"map     {EquipmentSidekickMap.LoadedCount} entries";
                _statusLabel.Hue = AttachmentRenderer.LastAutoAppliedSlots > 0
                    ? Debug3DStyle.HUE_OK : Debug3DStyle.HUE_WARN;
            }
        }
    }
}
