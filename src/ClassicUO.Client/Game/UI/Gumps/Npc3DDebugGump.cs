// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — single canonical home for every NPC-3D toggle, the
// mobile-3D index status, the nearby-NPC inspector table, and the new
// mobiles-wireframe toggle. Replaces the duplicated NPCs section that used
// to live in WorldRenderGump.
//
// Sections:
//   TOGGLES         — Mobiles in 3D / Mobile3DIndex / wireframe / 2D-A-B,
//                     RT geometry sliders (Max distance / RT W/H / SuperSample
//                     / Foot margin / Y anchor offset),
//                     Reroll outfits / Reload index / Open RT debug.
//   INDEX STATUS    — live load path / counts / last error / RT pool size /
//                     outfit cache / inline NPCs drawn last frame.
//   NEARBY NPCS     — scrollable table of every non-player human within
//                     MaxRender3DDistance + 4 tiles. Per-row columns and a
//                     status string showing the first failing render gate.
//                     Click a row → that serial becomes the SELECTED NPC.
//                     Per-row Reroll button drops & re-picks that NPC's outfit.
//   SELECTED NPC    — chosen NPC's outfit slot dump (each AttachSlotKind →
//                     glb path or "(none)").
//   FOOTER          — Dump State to Console (full toggle state + table + sel).
//
// Refresh cadence: full row rebuild every ~30 frames (~0.5s) to avoid churn.
// Live status labels update every Update().

using System;
using System.Collections.Generic;
using System.Text;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Mobiles;
using ClassicUO.Renderer.World;

#nullable disable

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class Npc3DDebugGump : Gump
    {
        public static Npc3DDebugGump Instance;

        // ===== Constructor-injected services (ADR-012 §6 / playbook §Q) =====
        private readonly IMobileOutfitService _outfit;
        private readonly IMobileAnimService _anim;
        private readonly IRenderDiagnosticsService _diag;
        private readonly IPlayer3DService _player;
        private readonly IMobileRT3DService _rt;
        private readonly IRenderModeService _renderMode;

        private const int W = 800;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;

        private const ushort HUE_LABEL = Debug3DStyle.HUE_LABEL_WHITE;
        private const ushort HUE_VALUE = Debug3DStyle.HUE_VALUE;
        private const ushort HUE_OK    = Debug3DStyle.HUE_OK;
        private const ushort HUE_WARN  = Debug3DStyle.HUE_WARN;

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        // Live labels.
        private Label _indexStatusLabel;
        private Label _runtimeStatusLabel;
        private Label _selectedHeaderLabel;
        private Label _selectedOutfitLabel;

        // Scrollable nearby-NPCs table.
        private ScrollArea _scroll;
        private int _refreshCounter;
        private const int REFRESH_EVERY = 30; // frames

        // Selected NPC for the outfit detail panel.
        private uint _selectedSerial;

        // Per-row reroll buttons remember which serial they target. Buttons
        // route through OnButtonClick so we share one handler — buttonID is
        // the row index, mapped via _rowSerials.
        private readonly List<uint> _rowSerials = new();

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        /// <summary>
        /// Convenience overload that resolves <see cref="IMobileOutfitService"/> and
        /// <see cref="IMobileAnimService"/> from the active renderer service container.
        /// </summary>
        public Npc3DDebugGump(World world)
            : this(world,
                Renderer3DHost.Services.MobileOutfit,
                Renderer3DHost.Services.MobileAnim,
                Renderer3DHost.Services.RenderDiagnostics,
                Renderer3DHost.Services.Player3D,
                Renderer3DHost.Services.MobileRT3D,
                Renderer3DHost.Services.RenderMode) { }

        public Npc3DDebugGump(World world, IMobileOutfitService outfit, IMobileAnimService anim,
                              IRenderDiagnosticsService diag, IPlayer3DService player,
                              IMobileRT3DService rt, IRenderModeService renderMode)
            : base(world, 0, 0)
        {
            _outfit = outfit ?? throw new ArgumentNullException(nameof(outfit));
            _anim = anim ?? throw new ArgumentNullException(nameof(anim));
            _diag = diag ?? throw new ArgumentNullException(nameof(diag));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _rt = rt ?? throw new ArgumentNullException(nameof(rt));
            _renderMode = renderMode ?? throw new ArgumentNullException(nameof(renderMode));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;
            Instance = this;

            int y = Debug3DStyle.BuildShell(this, W, "3D NPC DEBUG",
                out _outerBg, out _innerBg, out _borders);
            int contentX = Debug3DStyle.GetContentX();
            int innerW   = Debug3DStyle.GetContentWidth(W);

            // ---------- TOGGLES ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "TOGGLES", y);

            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Mobiles in 3D",         _renderMode.MobilesIn3D, v => _renderMode.SetMobilesIn3D(v),
                "Mobile3DIndex enabled", Mobile3DIndex.Enabled,            v => Mobile3DIndex.Enabled = v);

            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Mobiles wireframe",        _player.MobilesWireframe, v => _player.SetMobilesWireframe(v),
                "Show 2D player sprite (A/B)", _rt.Show2DPlayerSprite, v => _rt.SetShow2DPlayerSprite(v));

            y += SECTION_GAP;

            y = AddSlider("Max NPC 3D distance (tiles)", 4, 24,
                _rt.MaxRender3DDistance, y, contentX, innerW,
                v => _rt.SetMaxRender3DDistance(v));
            y = AddSlider("RT width (px)",  64, 512, _rt.RTWidth,  y, contentX, innerW, v => _rt.SetRTWidth(v));
            y = AddSlider("RT height (px)", 64, 512, _rt.RTHeight, y, contentX, innerW, v => _rt.SetRTHeight(v));
            y = AddSlider("SuperSample (x)", 1, 4,   _rt.SuperSample, y, contentX, innerW, v => _rt.SetSuperSample(v));
            y = AddSlider("Foot margin (px)", 0, 96, _rt.FootMarginFromBottom, y, contentX, innerW, v => _rt.SetFootMarginFromBottom(v));
            y = AddSlider("RT Y anchor offset (px)", -64, 64, _rt.RTYAnchorOffset, y, contentX, innerW, v => _rt.SetRTYAnchorOffset(v));

            y += SECTION_GAP;

            int btnW = (innerW - 30) / 3;
            Add(new NiceButton(contentX,                    y, btnW, 22, ButtonAction.Activate, "Reroll NPC outfits")    { ButtonParameter = BtnId.Reroll });
            Add(new NiceButton(contentX + btnW + 10,        y, btnW, 22, ButtonAction.Activate, "Reload mobile-3D index"){ ButtonParameter = BtnId.ReloadIndex });
            Add(new NiceButton(contentX + 2 * (btnW + 10),  y, btnW, 22, ButtonAction.Activate, "Open RT debug...")      { ButtonParameter = BtnId.OpenRtDbg });
            y += ROW_H + SECTION_GAP;

            // ---------- INDEX STATUS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "INDEX STATUS", y);
            _indexStatusLabel = new Label("(loading…)", true, HUE_VALUE, innerW, 0) { X = contentX, Y = y };
            Add(_indexStatusLabel);
            y += ROW_H * 3;

            _runtimeStatusLabel = new Label("(runtime status)", true, HUE_VALUE, innerW, 0) { X = contentX, Y = y };
            Add(_runtimeStatusLabel);
            y += ROW_H * 2 + SECTION_GAP;

            // ---------- NEARBY NPCS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "NEARBY NPCS  (within max distance + 4)", y);
            // Header row — fixed monospace-ish layout via padding in the row builder.
            Add(new Label(BuildHeaderRow(), true, HUE_LABEL, innerW, 0) { X = contentX, Y = y });
            y += ROW_H;

            _scroll = new ScrollArea(contentX, y, innerW, ROW_H * 12, true)
            {
                AcceptMouseInput = true,
            };
            Add(_scroll);
            y += ROW_H * 12 + SECTION_GAP;

            // ---------- SELECTED NPC ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "SELECTED NPC OUTFIT", y);
            _selectedHeaderLabel = new Label("(click a row above)", true, HUE_LABEL, innerW, 0) { X = contentX, Y = y };
            Add(_selectedHeaderLabel);
            y += ROW_H;
            _selectedOutfitLabel = new Label("", true, HUE_VALUE, innerW, 0) { X = contentX, Y = y };
            Add(_selectedOutfitLabel);
            y += ROW_H * 9 + SECTION_GAP;

            // ---------- Footer ----------
            y = Debug3DStyle.BeginFooter(this, contentX, innerW, y);
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Dump State to Console")
                { ButtonParameter = BtnId.Dump });
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 60;
            Y = 60;
            WantUpdateSize = false;

            RebuildNpcRows();
        }

        // ---------- Layout helpers ----------
        private int AddSlider(string label, int min, int max, int value, int y,
            int contentX, int innerW, Action<int> onChange)
        {
            return Debug3DStyle.AddSlider(this, contentX, innerW,
                labelW: 220, label, min, max, value, y, onChange);
        }

        // ---------- Row formatting (column widths chosen to fit ~760 px) ----------
        // Cols (chars):  Serial(10) Name(16) Title(14) Body(5) Dist(4) Hum(3)
        //                Type(14) Pack(14) Kind(8) Status(20)
        private static string BuildHeaderRow()
            => $"{"Serial",-10}  {"Name",-16}  {"Title",-14}  {"Body",-5} {"Dst",-4} {"H",-3} {"Type",-14}  {"Pack",-14}  {"Kind",-8}  Status";

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= max) return s;
            return s.Substring(0, max);
        }

        // ---------- Buttons ----------
        private static class BtnId
        {
            public const int Reroll        = 1;
            public const int ReloadIndex   = 2;
            public const int OpenRtDbg     = 3;
            public const int Dump          = 4;
            public const int RowRerollBase = 1000; // row idx + base; serial via _rowSerials
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); return;
                case BtnId.Reroll:
                    _outfit.Clear();
                    return;
                case BtnId.ReloadIndex:
                    Mobile3DIndex.Invalidate();
                    Mobile3DIndex.EnsureLoaded();
                    return;
                case BtnId.OpenRtDbg:
                    OpenSingleton<Debug3DRTGump>(w => new Debug3DRTGump(w));
                    return;
                case BtnId.Dump:
                    DumpToConsole();
                    return;
            }

            // Row reroll buttons.
            if (buttonID >= BtnId.RowRerollBase)
            {
                int idx = buttonID - BtnId.RowRerollBase;
                if (idx >= 0 && idx < _rowSerials.Count)
                {
                    uint serial = _rowSerials[idx];
                    _outfit.Drop(serial);
                    _selectedSerial = serial; // also select on reroll
                }
            }
        }

        private void OpenSingleton<T>(Func<World, T> ctor) where T : Gump
        {
            foreach (var g in Managers.UIManager.Gumps)
            {
                if (g is T existing && !existing.IsDisposed)
                {
                    existing.SetInScreen();
                    existing.BringOnTop();
                    return;
                }
            }
            var fresh = ctor(World);
            Managers.UIManager.Add(fresh);
        }

        // ---------- Update tick ----------
        public override void Update()
        {
            base.Update();

            if (_indexStatusLabel != null)
                _indexStatusLabel.Text = BuildIndexStatus();
            if (_runtimeStatusLabel != null)
                _runtimeStatusLabel.Text = BuildRuntimeStatus();

            UpdateSelectedDetail();

            _refreshCounter++;
            if (_refreshCounter >= REFRESH_EVERY)
            {
                _refreshCounter = 0;
                RebuildNpcRows();
            }
        }

        private string BuildIndexStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Loaded from: {Mobile3DIndex.LoadedFromPath ?? "(not yet — index loads on first NPC frame)"}");
            sb.AppendLine($"Types: {Mobile3DIndex.LoadedTypeCount}    Bodies: {Mobile3DIndex.LoadedBodyCount}");
            if (!string.IsNullOrEmpty(Mobile3DIndex.LastError))
                sb.Append($"LAST ERROR: {Mobile3DIndex.LastError}");
            else
                sb.Append("Last error: (none)");
            return sb.ToString();
        }

        private string BuildRuntimeStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Mode: {_renderMode.CurrentLabel}    " +
                          $"WorldIs3D={_renderMode.WorldIs3D}    " +
                          $"MobilesIn3D={_renderMode.MobilesIn3D}");
            sb.Append($"RT pool: {0}    Outfit cache: {_outfit.CachedCount}    " +
                      $"Anim states: {_anim.CachedCount}    " +
                      $"Inline NPCs drawn (last frame): {_diag.LastInlineNpcsDrawn}    " +
                      $"RT path enabled: {_rt.Enabled}");
            return sb.ToString();
        }

        private void UpdateSelectedDetail()
        {
            if (_selectedHeaderLabel == null) return;
            if (_selectedSerial == 0)
            {
                _selectedHeaderLabel.Text = "(click a row above)";
                if (_selectedOutfitLabel != null) _selectedOutfitLabel.Text = "";
                return;
            }

            Mobile m = null;
            if (World != null)
            {
                foreach (var mm in World.Mobiles.Values)
                {
                    if (mm != null && mm.Serial == _selectedSerial) { m = mm; break; }
                }
            }
            if (m == null)
            {
                _selectedHeaderLabel.Text = $"Serial 0x{_selectedSerial:X8} — no longer visible";
                if (_selectedOutfitLabel != null) _selectedOutfitLabel.Text = "";
                return;
            }

            string typeName = Mobile3DIndex.ResolveTypeFromMobile(m) ?? "(null)";
            bool hasEntry = Mobile3DIndex.TryGet(typeName, m.Graphic, out var entry);
            _selectedHeaderLabel.Text =
                $"Serial 0x{m.Serial:X8}  Name='{m.Name}'  Title='{m.Title}'  Body=0x{m.Graphic:X4}  " +
                $"Type='{typeName}'  Pack='{(hasEntry ? entry.Pack : "(none)")}'  Kind={(hasEntry ? entry.Kind.ToString() : "—")}";

            if (_selectedOutfitLabel == null) return;
            if (!hasEntry || string.IsNullOrEmpty(entry.Pack))
            {
                _selectedOutfitLabel.Text = "(no composed-humanoid entry — NPC stays as 2D sprite)";
                return;
            }
            var outfit = _outfit.GetOrPick(m.Serial, entry.Pack);
            if (outfit == null)
            {
                _selectedOutfitLabel.Text = "(outfit registry returned null)";
                return;
            }
            var sb = new StringBuilder();
            foreach (var kv in outfit.SlotIndex)
            {
                // outfit.SlotIndex.Key is the new domain OutfitSlot; cast to legacy
                // AttachSlotKind (values bit-identical, locked by parity test).
                var slot = AttachmentRenderer.FindSlotPublic((AttachSlotKind)(int)kv.Key);
                string path = "(none)";
                if (slot != null && kv.Value >= 0 && kv.Value < slot.Available.Count)
                    path = slot.Available[kv.Value];
                sb.AppendLine($"{kv.Key,-14} {path}");
            }
            _selectedOutfitLabel.Text = sb.ToString();
        }

        // ---------- Nearby-NPC scroll rows ----------
        private void RebuildNpcRows()
        {
            if (_scroll == null) return;
            _scroll.Clear();
            _rowSerials.Clear();

            if (World == null || World.Player == null)
            {
                _scroll.Add(new Label("(no world)", true, HUE_WARN, _scroll.Width - 24, 0) { X = 4, Y = 4 });
                return;
            }

            int max = _rt.MaxRender3DDistance + 4;
            int yRow = 4;
            int rowIdx = 0;
            int rowH = 18;

            foreach (var m in World.Mobiles.Values)
            {
                if (m == null || m == World.Player || m.IsDestroyed) continue;
                if (m.Distance > max) continue;

                _rowSerials.Add(m.Serial);

                string typeName = Mobile3DIndex.ResolveTypeFromMobile(m);
                bool hasEntry = Mobile3DIndex.TryGet(typeName, m.Graphic, out var entry);

                string pack = hasEntry ? entry.Pack : "—";
                string kind = hasEntry ? entry.Kind.ToString() : "—";
                string status = ComputeStatus(m, hasEntry, entry);

                string row =
                    $"0x{m.Serial:X8}  {Trim(m.Name, 16),-16}  {Trim(m.Title, 14),-14}  " +
                    $"0x{m.Graphic:X4} {(int)m.Distance,-4} {(m.IsHuman ? "Y" : "."),-3} " +
                    $"{Trim(typeName ?? "—", 14),-14}  {Trim(pack, 14),-14}  {Trim(kind, 8),-8}  {status}";

                ushort hue = status.StartsWith("OK", StringComparison.Ordinal) ? HUE_OK :
                             (m == null || !m.IsHuman ? HUE_LABEL : HUE_WARN);

                // Selectable label — clicking sets _selectedSerial. We use a
                // HitBox sized to the row to grab the click.
                var rowLabel = new Label(row, true, hue, _scroll.Width - 80, 0)
                    { X = 4, Y = yRow };
                _scroll.Add(rowLabel);

                var hb = new HitBox(0, yRow, _scroll.Width - 80, rowH, "select", 0.0f);
                uint serialCapture = m.Serial;
                hb.MouseUp += (s, e) => { _selectedSerial = serialCapture; };
                _scroll.Add(hb);

                // Row reroll button.
                var btn = new NiceButton(_scroll.Width - 70, yRow - 1, 60, rowH, ButtonAction.Activate, "Reroll")
                    { ButtonParameter = BtnId.RowRerollBase + rowIdx };
                _scroll.Add(btn);

                yRow += rowH;
                rowIdx++;
            }

            if (rowIdx == 0)
                _scroll.Add(new Label("(no nearby mobiles within range)", true, HUE_LABEL, _scroll.Width - 24, 0) { X = 4, Y = 4 });
        }

        // First failing render gate, or "OK 3D" if all gates pass.
        // Non-static because it pulls outfit lookup through the injected service.
        private string ComputeStatus(Mobile m, bool hasEntry, Mobile3DEntry entry)
        {
            if (!_renderMode.MobilesIn3D)                  return "✗ MobilesIn3D OFF";
            if (!Mobile3DIndex.Enabled)                    return "✗ Index disabled";
            if (!m.IsHuman)                                return "✗ not human";
            if (m.Distance > _rt.MaxRender3DDistance)
                                                           return $"✗ dist {(int)m.Distance}>{_rt.MaxRender3DDistance}";
            if (!hasEntry || !entry.IsComposedHumanoid)    return "✗ no index entry";
            var outfit = _outfit.GetOrPick(m.Serial, entry.Pack);
            if (outfit == null)                            return "✗ no outfit picked";

            // Backend depends on mode:
            if (_renderMode.WorldIs3D)                     return "OK inline-3D";
            if (_rt.Enabled)                               return "OK RT-blit";
            return "✗ no backend (Classic2D pure)";
        }

        // ---------- Console dump ----------
        private void DumpToConsole()
        {
            var sb = new StringBuilder();
            sb.AppendLine("======================== [3DCUO] NPC 3D DEBUG DUMP ========================");
            sb.AppendLine(BuildIndexStatus());
            sb.AppendLine();
            sb.AppendLine(BuildRuntimeStatus());
            sb.AppendLine();
            sb.AppendLine("NEARBY NPCS:");
            sb.AppendLine(BuildHeaderRow());
            if (World != null && World.Player != null)
            {
                int max = _rt.MaxRender3DDistance + 4;
                foreach (var m in World.Mobiles.Values)
                {
                    if (m == null || m == World.Player || m.IsDestroyed) continue;
                    if (m.Distance > max) continue;

                    string typeName = Mobile3DIndex.ResolveTypeFromMobile(m);
                    bool hasEntry = Mobile3DIndex.TryGet(typeName, m.Graphic, out var entry);
                    string pack = hasEntry ? entry.Pack : "—";
                    string kind = hasEntry ? entry.Kind.ToString() : "—";
                    string status = ComputeStatus(m, hasEntry, entry);
                    sb.AppendLine(
                        $"0x{m.Serial:X8}  {Trim(m.Name, 16),-16}  {Trim(m.Title, 14),-14}  " +
                        $"0x{m.Graphic:X4} {(int)m.Distance,-4} {(m.IsHuman ? "Y" : "."),-3} " +
                        $"{Trim(typeName ?? "—", 14),-14}  {Trim(pack, 14),-14}  {Trim(kind, 8),-8}  {status}");
                }
            }
            sb.AppendLine();
            sb.AppendLine($"SELECTED NPC: 0x{_selectedSerial:X8}");
            sb.AppendLine(_selectedHeaderLabel?.Text ?? "(none)");
            sb.AppendLine(_selectedOutfitLabel?.Text ?? "");
            sb.AppendLine("===========================================================================");
            Console.WriteLine(sb.ToString());
        }
    }
}
