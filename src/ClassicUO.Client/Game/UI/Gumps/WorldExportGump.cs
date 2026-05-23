// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — dedicated world-GLB export gump. Split out of
// WallMeshGump so the export workflow has a focused surface (radius +
// block tunables, include flags, multi-vs-mesh routing) instead of being
// jammed into the wall-mesh tools.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class WorldExportGump : Gump
    {
        public static WorldExportGump Instance;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private const int W = 480;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

        private static class BtnId
        {
            public const int ExportRadius   = 1;
            public const int ExportFullWorld = 2;
            public const int OpenLastDir    = 3;
            public const int ReloadWalls    = 4;
        }

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        private Label _statusLabel;

        public WorldExportGump(World world) : base(world, 0, 0)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;
            Instance = this;

            int y = Debug3DStyle.BuildShell(this, W, "WORLD EXPORT (GLB)",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- Includes ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "INCLUDE", y);
            y = AddTwoColCheck(
                "Textures (PNG embed)", WorldGlbExporter.IncludeTextures,
                    v => WorldGlbExporter.IncludeTextures = v,
                "Statics", WorldGlbExporter.IncludeStatics,
                    v => WorldGlbExporter.IncludeStatics = v,
                y);
            y = AddTwoColCheck(
                "Multis", WorldGlbExporter.IncludeMultis,
                    v => WorldGlbExporter.IncludeMultis = v,
                "Wall meshes -> placements", WorldGlbExporter.ExternalizeWallMeshes,
                    v => WorldGlbExporter.ExternalizeWallMeshes = v,
                y);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- Tunables ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "TUNABLES", y);
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 180,
                "Radius (single export, tiles)", 32, 1024, WorldGlbExporter.RadiusTiles, y,
                v => WorldGlbExporter.RadiusTiles = v);
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 180,
                "Full-world radius (tiles)", 256, 4096, WorldGlbExporter.FullWorldRadiusTiles, y,
                v => WorldGlbExporter.FullWorldRadiusTiles = v);
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 180,
                "Full-world block (tiles)", 64, 1024, WorldGlbExporter.FullWorldBlockTiles, y,
                v => WorldGlbExporter.FullWorldBlockTiles = v);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- Actions ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "EXPORT", y);
            int btnW = (innerW - 10) / 2;
            Add(new NiceButton(contentX,              y, btnW, 22, ButtonAction.Activate, "Export Radius (single GLB)") { ButtonParameter = BtnId.ExportRadius });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Export Entire World (chunked)")  { ButtonParameter = BtnId.ExportFullWorld });
            y += ROW_H + 4;
            Add(new NiceButton(contentX,              y, btnW, 22, ButtonAction.Activate, "Reload Wall Manifest") { ButtonParameter = BtnId.ReloadWalls });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Open Last Output Dir")  { ButtonParameter = BtnId.OpenLastDir });
            y += ROW_H + 4;

            _statusLabel = new Label("ready", true, Debug3DStyle.HUE_LABEL_WHITE, innerW, font: 1)
            { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 280;
            Y = 60;
            WantUpdateSize = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); return;
                case BtnId.ExportRadius:
                {
                    string outDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                        AppContext.BaseDirectory, "..", "..", "..", "dumps"));
                    System.IO.Directory.CreateDirectory(outDir);
                    string outFile = System.IO.Path.Combine(outDir,
                        $"world_{DateTime.Now:yyyyMMdd_HHmmss}.glb");
                    var path = WorldGlbExporter.Export(World, Client.Game.GraphicsDevice, outFile);
                    SetStatus(path ?? $"FAILED: {WorldGlbExporter.LastError}");
                    break;
                }
                case BtnId.ExportFullWorld:
                {
                    string outDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                        AppContext.BaseDirectory, "..", "..", "..", "dumps",
                        $"world_{DateTime.Now:yyyyMMdd_HHmmss}"));
                    var manifest = WorldGlbExporter.ExportFullWorld(World, Client.Game.GraphicsDevice, outDir);
                    SetStatus(manifest ?? $"FAILED: {WorldGlbExporter.LastError}");
                    break;
                }
                case BtnId.ReloadWalls:
                    WallMeshRegistry.Invalidate();
                    Console.WriteLine("[3DCUO] WallMeshRegistry invalidated.");
                    SetStatus("wall manifest invalidated");
                    break;
                case BtnId.OpenLastDir:
                {
                    var path = WorldGlbExporter.LastOutputPath;
                    if (string.IsNullOrEmpty(path)) { SetStatus("no last output"); break; }
                    string dir = System.IO.Directory.Exists(path) ? path
                        : System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = dir, UseShellExecute = true
                            });
                        }
                        catch (Exception ex) { SetStatus("open failed: " + ex.Message); }
                    }
                    break;
                }
            }
        }

        private void SetStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.Text = msg ?? "";
            Console.WriteLine("[3DCUO][world-export] " + msg);
        }

        private int AddTwoColCheck(
            string label1, bool init1, Action<bool> on1,
            string label2, bool init2, Action<bool> on2,
            int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                label1, init1, on1, label2, init2, on2);
        }
    }
}
