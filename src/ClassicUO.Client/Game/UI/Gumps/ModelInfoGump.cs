// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — runtime glb inspector + full hierarchy debug view.
//
// Sections:
//   PLAYER          mobile state, world position, facing, current anim model
//   PLAYER MESH     submesh list (was the only section in the old version)
//   ATTACHMENTS     each AttachmentRenderer slot, its loaded glb if any, its
//                   submeshes, joint count, joint-remap match count, current
//                   slot tuning
//   COUNTERS        per-frame draw stats (slots/submeshes/triangles drawn)
//
// All sections refresh every Update so the gump tracks live state.

using System;
using System.Text;
using ClassicUO.Assets;
using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Mobiles;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ModelInfoGump : Gump
    {
        public static ModelInfoGump Instance;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private const int W = 620;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = 22;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;
        private const int TITLE_BAR_H = Debug3DStyle.TITLE_BAR_H;

        private const ushort HUE_LABEL = Debug3DStyle.HUE_LABEL_WHITE;
        private const ushort HUE_VALUE = Debug3DStyle.HUE_VALUE;
        private const ushort HUE_OK    = Debug3DStyle.HUE_OK;
        private const ushort HUE_WARN  = Debug3DStyle.HUE_WARN;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        private Label _playerLabel;
        private Label _summaryLabel;
        private Label _statusLabel;
        private Label _countersLabel;

        private SkinnedModelGlb _shownModel;
        private int _shownSubmeshCount;
        private ScrollArea _scroll;
        private ScrollArea _attachScroll;
        private string _lastAttachSig;

        private readonly IPlayer3DService _player;
        private readonly IMobileRT3DService _rt;

        public ModelInfoGump(World world)
            : this(world, Renderer3DHost.Services.Player3D, Renderer3DHost.Services.MobileRT3D) { }

        public ModelInfoGump(World world, IPlayer3DService player, IMobileRT3DService rt) : base(world, 0, 0)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _rt = rt ?? throw new ArgumentNullException(nameof(rt));
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "3DCUO MODEL INFO",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- PLAYER section ----------
            y = AddSectionHeader("PLAYER  (mobile + world)", y);
            _playerLabel = new Label("(...)", true, HUE_LABEL, innerW, 0)
                { X = contentX, Y = y };
            Add(_playerLabel);
            y += ROW_H * 5;

            // ---------- PLAYER MESH summary ----------
            y = AddSectionHeader("PLAYER MESH  (current anim model)", y);
            _summaryLabel = new Label("(no model loaded)", true, HUE_LABEL, innerW, 0)
                { X = contentX, Y = y };
            Add(_summaryLabel);
            y += ROW_H * 8;

            int btnW = (innerW - 30) / 4;
            Add(new NiceButton(contentX,                  y, btnW, 22, ButtonAction.Activate, "Hot Reload") { ButtonParameter = 1 });
            Add(new NiceButton(contentX + (btnW + 10),    y, btnW, 22, ButtonAction.Activate, "Show All") { ButtonParameter = 3 });
            Add(new NiceButton(contentX + (btnW + 10)*2,  y, btnW, 22, ButtonAction.Activate, "Dump Values") { ButtonParameter = 4 });
            Add(new NiceButton(contentX + (btnW + 10)*3,  y, btnW, 22, ButtonAction.Activate, "Open Debug3D") { ButtonParameter = 5 });
            y += ROW_H + SECTION_GAP;

            y = AddSectionHeader("PLAYER MESH SUBMESHES", y);
            _scroll = new ScrollArea(contentX, y, innerW, ROW_H * 8, true)
            {
                AcceptMouseInput = true,
            };
            Add(_scroll);
            y += ROW_H * 8 + SECTION_GAP;

            // ---------- ATTACHMENTS section ----------
            // 9 slots × 2 lines each = 18 rows. A static Label can't scroll
            // and was being clipped by the next section header. Scroll area
            // gives the user access to every slot regardless of slot count.
            y = AddSectionHeader("ATTACHMENTS  (slots + loaded glbs)", y);
            _attachScroll = new ScrollArea(contentX, y, innerW, ROW_H * 8, true)
            {
                AcceptMouseInput = true,
            };
            Add(_attachScroll);
            y += ROW_H * 8 + SECTION_GAP;

            // ---------- COUNTERS ----------
            y = AddSectionHeader("PER-FRAME DRAW COUNTERS", y);
            _countersLabel = new Label("(...)", true, HUE_VALUE, innerW, 0)
                { X = contentX, Y = y };
            Add(_countersLabel);
            y += ROW_H * 2;

            y = AddSectionHeader("STATUS", y);
            _statusLabel = new Label("(status)", true, HUE_VALUE, innerW, 0) { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H * 3 + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 440;
            Y = 60;
            WantUpdateSize = false;

            RebuildSubmeshRows();
            RebuildAttachmentRowsIfChanged();
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); break;
                case 1: HotReload(); break;
                case 3:
                    var m = Player3DRenderer.Model; // SkinnedModelGlb internal-sealed — read off legacy.
                    if (m != null)
                        foreach (var s in m.Submeshes) s.Visible = true;
                    _player.SetHideSubmeshNameMatch("");
                    RebuildSubmeshRows();
                    break;
                case 4:
                    Console.WriteLine("==================== [3DCUO] MODEL INFO DUMP ====================");
                    Console.WriteLine(BuildPlayerSection());
                    Console.WriteLine(BuildSummary(Player3DRenderer.Model)); // Model = SkinnedModelGlb (internal-sealed)
                    Console.WriteLine(BuildAttachmentSection());
                    Console.WriteLine(BuildCounters());
                    Console.WriteLine("=================================================================");
                    break;
                case 5:
                    if (Render3DLauncherGump.Instance == null)
                    {
                        var dg = new Render3DLauncherGump(World);
                        Render3DLauncherGump.Instance = dg;
                        ClassicUO.Game.Managers.UIManager.Add(dg);
                    }
                    else
                    {
                        Render3DLauncherGump.Instance.SetInScreen();
                        Render3DLauncherGump.Instance.BringOnTop();
                    }
                    break;
            }
        }

        // Non-static so it can read injected _player.
        private void HotReload()
        {
            _player.InvalidateAll();
            AttachmentRenderer.Invalidate();
        }

        public override void Update()
        {
            base.Update();

            var m = Player3DRenderer.Model;

            if (_playerLabel != null) _playerLabel.Text = BuildPlayerSection();
            if (_summaryLabel != null)
            {
                _summaryLabel.Text = BuildSummary(m);
                _summaryLabel.Hue = m != null ? HUE_LABEL : HUE_WARN;
            }
            RebuildAttachmentRowsIfChanged();
            if (_countersLabel != null) _countersLabel.Text = BuildCounters();

            if (_statusLabel != null)
            {
                if (m != null)
                {
                    _statusLabel.Text =
                        $"path   {_player.ModelPath}\n" +
                        $"clip   [{_player.AnimIndex}] t={m.AnimTime:F2}s   tpose={_player.TPoseOnly}\n" +
                        $"hide   '{_player.HideSubmeshNameMatch}'  (empty = show all)";
                    _statusLabel.Hue = HUE_OK;
                }
                else if (!string.IsNullOrEmpty(_player.LastError))
                {
                    _statusLabel.Text = $"LOAD FAILED:\n{_player.LastError}";
                    _statusLabel.Hue = HUE_WARN;
                }
                else
                {
                    _statusLabel.Text = "model not loaded yet";
                    _statusLabel.Hue = HUE_LABEL;
                }
            }

            int subCount = m?.Submeshes?.Count ?? 0;
            if (!ReferenceEquals(_shownModel, m) || subCount != _shownSubmeshCount)
            {
                _shownModel = m;
                _shownSubmeshCount = subCount;
                RebuildSubmeshRows();
            }
        }

        private string BuildPlayerSection()
        {
            var p = World?.Player;
            if (p == null) return "(no player)";

            var lwm = Player3DRenderer.LastWorldMatrix; // Matrix; not yet on IPlayer3DService.
            var sb = new StringBuilder();
            sb.AppendLine($"mobile      serial={p.Serial:X8}  name='{p.Name}'");
            sb.AppendLine($"position    UO=({p.X},{p.Y},{p.Z})  dir={(int)(p.Direction & ClassicUO.Game.Data.Direction.Mask)}");
            sb.AppendLine($"world       3D=({lwm.M41:F0},{lwm.M42:F0},{lwm.M43:F0})  scale={_player.ModelScale:F1}");
            sb.AppendLine($"state       {_player.CurrentState}  auto={_player.AutoStateFromMovement}");
            sb.Append(    $"rt-mode     {(_rt.Enabled ? "ON (RT'ed mobile path)" : "OFF (world 3D pass)")}");
            return sb.ToString();
        }

        // Non-static so it can read injected _player.
        private string BuildSummary(SkinnedModelGlb m)
        {
            if (m == null) return "(no model loaded — click Hot Reload to retry)";
            int totalV = 0, totalI = 0;
            foreach (var s in m.Submeshes) { totalV += s.IndexCount > 0 ? s.Vertices.VertexCount : 0; totalI += s.IndexCount; }
            var b = m.Bounds;
            var sb = new StringBuilder();
            sb.AppendLine($"path        {_player.ModelPath}");
            sb.AppendLine($"meshes      submeshes={m.Submeshes.Count}   verts={totalV}   idx={totalI}");
            sb.AppendLine($"skeleton    joints={m.JointNodeIndex.Length}   nodes={m.AllNodes?.Length ?? 0}");
            sb.AppendLine($"animations  count={m.Animations?.Length ?? 0}   active=[{_player.AnimIndex}]");
            sb.AppendLine($"bounds      min=({b.Min.X:F1},{b.Min.Y:F1},{b.Min.Z:F1})");
            sb.AppendLine($"            max=({b.Max.X:F1},{b.Max.Y:F1},{b.Max.Z:F1})");
            sb.AppendLine($"            size=({b.Max.X-b.Min.X:F1},{b.Max.Y-b.Min.Y:F1},{b.Max.Z-b.Min.Z:F1})");
            if (m.Animations != null && _player.AnimIndex < m.Animations.Length)
            {
                var a = m.Animations[_player.AnimIndex];
                sb.Append($"clip name   '{a.Name}'  duration={a.Duration:F2}s  channels={a.Channels.Count}");
            }
            return sb.ToString();
        }

        // Build a cheap signature of slot state. If unchanged across frames we
        // skip the rebuild — Update() runs ~60Hz and rebuilding the scroll
        // every frame churns Label allocations for no gain.
        private static string BuildAttachmentSig()
        {
            var sb = new StringBuilder();
            sb.Append(AttachmentRenderer.Enabled).Append('|')
              .Append(AttachmentRenderer.ForceIdentityPalette).Append('|')
              .Append(AttachmentRenderer.ForceDebugColor).Append('|');
            foreach (var slot in AttachmentRenderer.Slots)
            {
                sb.Append(slot.DisplayName).Append(':')
                  .Append(slot.CurrentIndex).Append('/')
                  .Append(slot.Available.Count).Append('|');
            }
            return sb.ToString();
        }

        private void RebuildAttachmentRowsIfChanged()
        {
            if (_attachScroll == null) return;
            string sig = BuildAttachmentSig();
            if (sig == _lastAttachSig) return;
            _lastAttachSig = sig;

            _attachScroll.Clear();
            int innerW = _attachScroll.Width - 24;

            int rowY = 0;
            // Header row — flag summary line.
            string flags =
                $"flags  enabled={AttachmentRenderer.Enabled}  identity={AttachmentRenderer.ForceIdentityPalette}  debugRGB={AttachmentRenderer.ForceDebugColor}";
            _attachScroll.Add(new Label(flags, true, HUE_VALUE, innerW, 0)
                { X = 4, Y = rowY });
            rowY += ROW_H;

            foreach (var slot in AttachmentRenderer.Slots)
            {
                int idx = slot.CurrentIndex;
                if (idx < 0)
                {
                    _attachScroll.Add(new Label(
                        $"[{slot.DisplayName,-9}] bone={slot.BoneName,-15} {slot.Available.Count,3} glbs   (none)",
                        true, HUE_LABEL, innerW, 0)
                        { X = 4, Y = rowY });
                    rowY += ROW_H;
                    continue;
                }

                int totalV = 0, totalI = 0, submeshCount = 0, joints = 0;
                if (slot.Cache.TryGetValue(idx, out var model) && model != null)
                {
                    submeshCount = model.Submeshes.Count;
                    foreach (var s in model.Submeshes) { totalV += s.Vertices.VertexCount; totalI += s.IndexCount; }
                    joints = model.JointNodeIndex?.Length ?? 0;
                }
                int matched = 0;
                if (slot.JointToPlayerNode.TryGetValue(idx, out var remap) && remap != null)
                {
                    foreach (var v in remap) if (v >= 0) matched++;
                }
                string fileName = slot.CurrentPath ?? "";
                int slash = fileName.LastIndexOf('/');
                if (slash >= 0) fileName = fileName.Substring(slash + 1);
                if (fileName.Length > 38) fileName = fileName.Substring(0, 37) + "…";

                string line1 = $"[{slot.DisplayName,-9}] bone={slot.BoneName,-15} [{idx,3}/{slot.Available.Count,3}] {fileName}";
                string line2 = $"            sub={submeshCount} v={totalV} i={totalI} joints={joints} remap={matched}/{joints}  err='{slot.LastError}'";

                _attachScroll.Add(new Label(line1, true, HUE_LABEL, innerW, 0)
                    { X = 4, Y = rowY });
                rowY += ROW_H;
                _attachScroll.Add(new Label(line2, true, HUE_VALUE, innerW, 0)
                    { X = 4, Y = rowY });
                rowY += ROW_H;
            }
        }

        private static string BuildAttachmentSection()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"flags  enabled={AttachmentRenderer.Enabled}  identity={AttachmentRenderer.ForceIdentityPalette}  debugRGB={AttachmentRenderer.ForceDebugColor}");
            foreach (var slot in AttachmentRenderer.Slots)
            {
                int idx = slot.CurrentIndex;
                if (idx < 0)
                {
                    sb.AppendLine($"[{slot.DisplayName,-9}] bone={slot.BoneName,-15} {slot.Available.Count,3} glbs   (none)");
                    continue;
                }

                int totalV = 0, totalI = 0, submeshCount = 0;
                int joints = 0;
                if (slot.Cache.TryGetValue(idx, out var model) && model != null)
                {
                    submeshCount = model.Submeshes.Count;
                    foreach (var s in model.Submeshes) { totalV += s.Vertices.VertexCount; totalI += s.IndexCount; }
                    joints = model.JointNodeIndex?.Length ?? 0;
                }
                int matched = 0;
                if (slot.JointToPlayerNode.TryGetValue(idx, out var remap) && remap != null)
                {
                    foreach (var v in remap) if (v >= 0) matched++;
                }
                string fileName = slot.CurrentPath;
                int slash = fileName.LastIndexOf('/');
                if (slash >= 0) fileName = fileName.Substring(slash + 1);
                if (fileName.Length > 38) fileName = fileName.Substring(0, 37) + "…";

                sb.AppendLine(
                    $"[{slot.DisplayName,-9}] bone={slot.BoneName,-15} [{idx,3}/{slot.Available.Count,3}] {fileName}");
                sb.AppendLine(
                    $"            sub={submeshCount} v={totalV} i={totalI} joints={joints} remap={matched}/{joints}  err='{slot.LastError}'");
            }
            return sb.ToString().TrimEnd();
        }

        private static string BuildCounters()
        {
            return
                $"slotsDrawn={AttachmentRenderer.LastSlotsDrawn}  " +
                $"submeshesDrawn={AttachmentRenderer.LastSubmeshesDrawn}  " +
                $"trianglesDrawn={AttachmentRenderer.LastTrianglesDrawn}";
        }

        private void RebuildSubmeshRows()
        {
            if (_scroll == null) return;
            _scroll.Clear();

            var m = Player3DRenderer.Model;
            if (m == null)
            {
                _scroll.Add(new Label("(no submeshes)", true, HUE_WARN, _scroll.Width - 20, 0) { X = 4, Y = 4 });
                return;
            }

            int innerW = _scroll.Width - 24;
            int rowY = 0;
            for (int i = 0; i < m.Submeshes.Count; i++)
            {
                var sub = m.Submeshes[i];

                var captured = sub;
                var cb = new Checkbox(0x00D2, 0x00D3, "", font: 1, color: HUE_LABEL, isunicode: true)
                    { X = 2, Y = rowY, IsChecked = captured.Visible };
                cb.ValueChanged += (s, e) => captured.Visible = cb.IsChecked;
                _scroll.Add(cb);

                string line =
                    $"[{i}] {Trim(captured.MeshName, 22)}  /  {Trim(captured.MaterialName, 22)}\n" +
                    $"     v={captured.Vertices.VertexCount}  i={captured.IndexCount}  " +
                    $"tex={(captured.Texture != null ? "yes" : "no")}  " +
                    $"factor=({captured.BaseColorFactor.X:F2},{captured.BaseColorFactor.Y:F2},{captured.BaseColorFactor.Z:F2})";
                _scroll.Add(new Label(line, true, HUE_LABEL, innerW - 30, 0)
                    { X = 28, Y = rowY });

                rowY += ROW_H * 2 + 2;
            }
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "(none)";
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        private int AddSectionHeader(string title, int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSectionHeader(this, contentX, innerW, title, y);
        }
    }
}
