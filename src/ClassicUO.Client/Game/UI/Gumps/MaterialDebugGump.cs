// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO PROTOTYPE — material/sampler debug gump for diagnosing the
// "white meshes" bug where loaded textures are non-null/decoded but the
// rendered output stays white. Provides ablation toggles to isolate the
// failure: test-pattern injection, sampler slot/filter cycling, factor boost,
// and CPU-side pixel readback. THROWAWAY DIAGNOSTIC UI.

using System;
using System.Collections.Generic;
using System.Text;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Mobiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// Brings effect parameter lists to the gump for the "Dump effect parameters"
// diagnostic. Each renderer owns its own TextureSkinnedEffect instance, so we
// expose them separately. PROTOTYPE diagnostic only.
namespace ClassicUO.Renderer.Renderer3D
{
    public static class TextureSkinnedEffectAccessor
    {
        public static Effect Effect;            // Player3DRenderer's effect (base anim mesh)
        public static Effect AttachEffect;      // AttachmentRenderer's effect (visible modular pieces)
        // Last DiffuseTex pointer that was uploaded to the attachment effect's
        // _pDiffuseTex parameter, captured immediately AFTER pass.Apply() in
        // the draw loop. Tells us whether SetValue is actually propagating.
        public static Texture2D LastAttachDiffuseTex;
        public static int LastAttachDiffuseW, LastAttachDiffuseH;
    }
}

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class MaterialDebugGump : Gump
    {
        public static MaterialDebugGump Instance;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private const int W = 560;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = 22;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;

        private const ushort HUE_LABEL = Debug3DStyle.HUE_LABEL_WHITE;
        private const ushort HUE_VALUE = Debug3DStyle.HUE_VALUE;
        private const ushort HUE_OK    = Debug3DStyle.HUE_OK;
        private const ushort HUE_WARN  = Debug3DStyle.HUE_WARN;

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        private NiceButton _btnPattern;
        private NiceButton _btnSlot;
        private NiceButton _btnSampler;
        private NiceButton _btnHasDiffuse;
        private NiceButton _btnFactorBoost;
        private NiceButton _btnForceSlot;
        private Label _statusLabel;
        private Label _readbackLabel;

        private readonly IPlayer3DService _player;

        public MaterialDebugGump(World world)
            : this(world, Renderer3DHost.Services.Player3D) { }

        public MaterialDebugGump(World world, IPlayer3DService player) : base(world, 0, 0)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "3DCUO MATERIAL DEBUG",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---- Cycling toggles ----
            y = AddSectionHeader("OVERRIDES (cycle by clicking)", y);

            int btnW = (innerW - 20) / 2;

            _btnPattern = new NiceButton(contentX, y, btnW, ROW_H, ButtonAction.Activate,
                PatternLabel()) { ButtonParameter = 1 };
            Add(_btnPattern);

            _btnSlot = new NiceButton(contentX + btnW + 10, y, btnW, ROW_H, ButtonAction.Activate,
                SlotLabel()) { ButtonParameter = 2 };
            Add(_btnSlot);
            y += ROW_H + 4;

            _btnSampler = new NiceButton(contentX, y, btnW, ROW_H, ButtonAction.Activate,
                SamplerLabel()) { ButtonParameter = 3 };
            Add(_btnSampler);

            _btnFactorBoost = new NiceButton(contentX + btnW + 10, y, btnW, ROW_H, ButtonAction.Activate,
                FactorBoostLabel()) { ButtonParameter = 5 };
            Add(_btnFactorBoost);
            y += ROW_H + 4;

            _btnHasDiffuse = new NiceButton(contentX, y, btnW, ROW_H, ButtonAction.Activate,
                HasDiffuseLabel()) { ButtonParameter = 4 };
            Add(_btnHasDiffuse);

            // Force-white toggle from Player3DRenderer for one-stop ablation
            var fwCb = new Checkbox(0x00D2, 0x00D3, "Force white material",
                font: 1, color: HUE_LABEL, isunicode: true)
                { X = contentX + btnW + 10, Y = y, IsChecked = _player.ForceWhiteMaterial };
            fwCb.ValueChanged += (s, e) => _player.SetForceWhiteMaterial(fwCb.IsChecked);
            Add(fwCb);
            y += ROW_H + 4;

            _btnForceSlot = new NiceButton(contentX, y, btnW, ROW_H, ButtonAction.Activate,
                ForceSlotLabel()) { ButtonParameter = 6 };
            Add(_btnForceSlot);

            // (A) Random per-submesh color — bypasses tex2D entirely, drives
            //     FallbackColor only. Each piece a different deterministic color.
            var randCb = new Checkbox(0x00D2, 0x00D3, "Random color per submesh",
                font: 1, color: HUE_LABEL, isunicode: true)
                { X = contentX + btnW + 10, Y = y, IsChecked = MaterialDebug.RandomPerSubmesh };
            randCb.ValueChanged += (s, e) => MaterialDebug.RandomPerSubmesh = randCb.IsChecked;
            Add(randCb);
            y += ROW_H + 4;

            // (C) Render a textured 3D cube via FNA stock BasicEffect — proves
            //     whether texturing works AT ALL on this build, independent
            //     of our custom skinning shader.
            var cubeCb = new Checkbox(0x00D2, 0x00D3, "Show debug textured cube (BasicEffect)",
                font: 1, color: HUE_LABEL, isunicode: true)
                { X = contentX, Y = y, IsChecked = TextureDebugCube.Enabled };
            cubeCb.ValueChanged += (s, e) => TextureDebugCube.Enabled = cubeCb.IsChecked;
            Add(cubeCb);
            y += ROW_H + SECTION_GAP;

            // (B) Live texture preview — first-loaded attachment texture rendered
            //     via SpriteBatch. If THIS shows the palette texture, GPU upload
            //     works and the bug is in our skinning shader's tex2D path.
            y = AddSectionHeader("LIVE TEXTURE PREVIEW (SpriteBatch path)", y);
            const int previewSize = 96;
            Add(new Texture2DPreview(contentX, y, previewSize, previewSize,
                () => FirstAttachmentTexture()));
            // Bigger 4x preview to the right so 32x32 atlas pixels are visible.
            Add(new Texture2DPreview(contentX + previewSize + 12, y, previewSize * 2, previewSize * 2,
                () => FirstAttachmentTexture()));
            y += previewSize * 2 + SECTION_GAP;

            // ---- Diagnostic actions ----
            y = AddSectionHeader("DIAGNOSTICS", y);

            Add(new NiceButton(contentX, y, btnW, ROW_H, ButtonAction.Activate,
                "Sample player tex[0] pixels") { ButtonParameter = 10 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, ROW_H, ButtonAction.Activate,
                "Sample first attachment tex") { ButtonParameter = 11 });
            y += ROW_H + 4;

            Add(new NiceButton(contentX, y, btnW, ROW_H, ButtonAction.Activate,
                "Dump first vertex UV") { ButtonParameter = 12 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, ROW_H, ButtonAction.Activate,
                "Dump effect parameters") { ButtonParameter = 13 });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, btnW, ROW_H, ButtonAction.Activate,
                "Save first attach tex as PNG") { ButtonParameter = 14 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, ROW_H, ButtonAction.Activate,
                "Save ALL attach textures") { ButtonParameter = 15 });
            y += ROW_H + SECTION_GAP;

            // ---- Readback ----
            y = AddSectionHeader("READBACK / STATUS", y);
            _readbackLabel = new Label("(click a Sample button)", true, HUE_VALUE, innerW, 0)
                { X = contentX, Y = y };
            Add(_readbackLabel);
            y += ROW_H * 4;

            _statusLabel = new Label("ready.", true, HUE_VALUE, innerW, 0)
                { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 60;
            Y = 60;
            WantUpdateSize = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); return;

                case 1: // pattern cycle
                    MaterialDebug.TestPattern = (TestPatternKind)
                        (((int)MaterialDebug.TestPattern + 1) % 6);
                    _btnPattern.TextLabel.Text = PatternLabel();
                    SetStatus($"test pattern → {MaterialDebug.TestPattern}", HUE_OK);
                    return;
                case 2: // slot mode
                    MaterialDebug.SlotMode = (SamplerSlotMode)
                        (((int)MaterialDebug.SlotMode + 1) % 4);
                    _btnSlot.TextLabel.Text = SlotLabel();
                    SetStatus($"sampler slot → {MaterialDebug.SlotMode}", HUE_OK);
                    return;
                case 3: // sampler kind
                    MaterialDebug.Sampler = (SamplerKind)
                        (((int)MaterialDebug.Sampler + 1) % 5);
                    _btnSampler.TextLabel.Text = SamplerLabel();
                    SetStatus($"sampler filter → {MaterialDebug.Sampler}", HUE_OK);
                    return;
                case 4: // HasDiffuse force
                    MaterialDebug.ForceHasDiffuse = MaterialDebug.ForceHasDiffuse switch
                    {
                        -1 => 0,
                        0 => 1,
                        _ => -1,
                    };
                    _btnHasDiffuse.TextLabel.Text = HasDiffuseLabel();
                    SetStatus($"HasDiffuse force → {MaterialDebug.ForceHasDiffuse}", HUE_OK);
                    return;
                case 5: // factor boost
                    MaterialDebug.FactorBoost = MaterialDebug.FactorBoost switch
                    {
                        1.0f => 2.0f,
                        2.0f => 5.0f,
                        5.0f => 0.5f,
                        0.5f => 0.0f,
                        _    => 1.0f,
                    };
                    _btnFactorBoost.TextLabel.Text = FactorBoostLabel();
                    SetStatus($"factor boost → x{MaterialDebug.FactorBoost}", HUE_OK);
                    return;
                case 6: // force texture slot cycle
                    MaterialDebug.ForceTextureSlot = MaterialDebug.ForceTextureSlot switch
                    {
                        -1 => 0,
                         0 => 1,
                         1 => 2,
                         2 => 3,
                         3 => 4,
                         _ => -1,
                    };
                    _btnForceSlot.TextLabel.Text = ForceSlotLabel();
                    SetStatus($"force texture slot → {MaterialDebug.ForceTextureSlot}", HUE_OK);
                    return;

                case 10: // sample player tex[0]
                    {
                        var m = Player3DRenderer.Model;
                        if (m == null || m.Submeshes.Count == 0) { SetReadback("no player model", HUE_WARN); return; }
                        Texture2D t = null;
                        string n = "(none)";
                        foreach (var s in m.Submeshes)
                            if (s.Texture != null) { t = s.Texture; n = s.MeshName; break; }
                        SetReadback($"player[{n}]: {MaterialDebug.SampleTexturePixels(t)}", HUE_VALUE);
                        Console.WriteLine($"[3DCUO][matdbg] player tex sample [{n}]: {MaterialDebug.SampleTexturePixels(t)}");
                        return;
                    }

                case 11: // sample first attachment tex
                    {
                        Texture2D t = null;
                        string n = "(none)";
                        foreach (var slot in AttachmentRenderer.Slots)
                        {
                            foreach (var kv in slot.Cache)
                            {
                                var mdl = kv.Value;
                                if (mdl == null) continue;
                                foreach (var s in mdl.Submeshes)
                                {
                                    if (s.Texture != null)
                                    {
                                        t = s.Texture;
                                        n = $"{slot.DisplayName}/{s.MeshName}";
                                        goto found;
                                    }
                                }
                            }
                        }
                        found:
                        SetReadback($"attach[{n}]: {MaterialDebug.SampleTexturePixels(t)}", HUE_VALUE);
                        Console.WriteLine($"[3DCUO][matdbg] attach tex sample [{n}]: {MaterialDebug.SampleTexturePixels(t)}");
                        return;
                    }

                case 12: // dump first vertex UV
                    {
                        // Player[0] vertex UV
                        var m = Player3DRenderer.Model;
                        if (m == null || m.Submeshes.Count == 0) { SetReadback("no player model", HUE_WARN); return; }
                        var sb = new StringBuilder();
                        for (int i = 0; i < Math.Min(m.Submeshes.Count, 2); i++)
                        {
                            var s = m.Submeshes[i];
                            sb.Append($"[{i}] {s.MeshName}: ").Append(ReadFirstUVs(s, 4)).Append("  ");
                        }
                        // First attachment, first submesh
                        foreach (var slot in AttachmentRenderer.Slots)
                        {
                            foreach (var kv in slot.Cache)
                            {
                                var mdl = kv.Value;
                                if (mdl == null) continue;
                                if (mdl.Submeshes.Count == 0) continue;
                                var s = mdl.Submeshes[0];
                                sb.Append($"\n[A:{slot.DisplayName}/{s.MeshName}]: ").Append(ReadFirstUVs(s, 4));
                                goto done;
                            }
                        }
                        done:
                        SetReadback(sb.ToString(), HUE_VALUE);
                        Console.WriteLine($"[3DCUO][matdbg] UV dump:\n{sb}");
                        return;
                    }

                case 14: // save first attachment texture as PNG
                    {
                        var t = FirstAttachmentTexture();
                        if (t == null) { SetReadback("no attachment texture loaded", HUE_WARN); return; }
                        try
                        {
                            string dir = System.IO.Path.Combine(AppContext.BaseDirectory, "dumps");
                            System.IO.Directory.CreateDirectory(dir);
                            string path = System.IO.Path.Combine(dir, $"texdump_{DateTime.Now:HHmmss}.png");
                            using (var fs = System.IO.File.Create(path))
                                t.SaveAsPng(fs, t.Width, t.Height);
                            SetReadback($"saved {t.Width}x{t.Height} -> {path}", HUE_OK);
                            Console.WriteLine($"[3DCUO][matdbg] saved tex -> {path}");
                        }
                        catch (Exception ex) { SetReadback($"save failed: {ex.Message}", HUE_WARN); }
                        return;
                    }
                case 15: // save ALL loaded attachment textures
                    {
                        try
                        {
                            string dir = System.IO.Path.Combine(AppContext.BaseDirectory, "dumps", $"texdump_{DateTime.Now:HHmmss}");
                            System.IO.Directory.CreateDirectory(dir);
                            int saved = 0;
                            var seen = new HashSet<int>();
                            foreach (var slot in AttachmentRenderer.Slots)
                            {
                                foreach (var kv in slot.Cache)
                                {
                                    var mdl = kv.Value;
                                    if (mdl == null) continue;
                                    foreach (var s in mdl.Submeshes)
                                    {
                                        if (s.Texture == null) continue;
                                        if (!seen.Add(s.Texture.GetHashCode())) continue;
                                        string path = System.IO.Path.Combine(dir, $"{slot.DisplayName}_{s.MeshName}.png");
                                        using var fs = System.IO.File.Create(path);
                                        s.Texture.SaveAsPng(fs, s.Texture.Width, s.Texture.Height);
                                        saved++;
                                    }
                                }
                            }
                            SetReadback($"saved {saved} unique textures -> {dir}", HUE_OK);
                            Console.WriteLine($"[3DCUO][matdbg] saved {saved} textures -> {dir}");
                        }
                        catch (Exception ex) { SetReadback($"bulk save failed: {ex.Message}", HUE_WARN); }
                        return;
                    }
                case 13: // dump effect parameters by name + type
                    {
                        var sb = new StringBuilder();
                        sb.Append($"State: TestPattern={MaterialDebug.TestPattern} SlotMode={MaterialDebug.SlotMode} ")
                          .Append($"Filter={MaterialDebug.Sampler} ForceHasDiffuse={MaterialDebug.ForceHasDiffuse} ")
                          .Append($"FactorBoost={MaterialDebug.FactorBoost} ForceSlot={MaterialDebug.ForceTextureSlot} ")
                          .Append($"ForceWhite={_player.ForceWhiteMaterial}\n");

                        // Snapshot of what the AttachmentRenderer actually
                        // pushed to its effect on the LAST submesh draw —
                        // captured at draw time so it survives even when the
                        // gump runs outside the render frame.
                        sb.Append($"Last attach DiffuseTex push: ");
                        if (TextureSkinnedEffectAccessor.LastAttachDiffuseTex == null)
                            sb.Append("null\n");
                        else
                            sb.Append($"{TextureSkinnedEffectAccessor.LastAttachDiffuseW}x{TextureSkinnedEffectAccessor.LastAttachDiffuseH} ptr={TextureSkinnedEffectAccessor.LastAttachDiffuseTex.GetHashCode():X8}\n");

                        DumpEffectParams(sb, "Player3D effect", TextureSkinnedEffectAccessor.Effect);
                        DumpEffectParams(sb, "Attach effect",  TextureSkinnedEffectAccessor.AttachEffect);
                        SetReadback(sb.ToString(), HUE_VALUE);
                        Console.WriteLine($"[3DCUO][matdbg] effect parameter dump:\n{sb}");
                        return;
                    }
            }
        }

        private static Texture2D FirstAttachmentTexture()
        {
            foreach (var slot in AttachmentRenderer.Slots)
            {
                foreach (var kv in slot.Cache)
                {
                    var mdl = kv.Value;
                    if (mdl == null) continue;
                    foreach (var s in mdl.Submeshes)
                        if (s.Texture != null) return s.Texture;
                }
            }
            return null;
        }

        private static void DumpEffectParams(StringBuilder sb, string label, Effect fx)
        {
            if (fx == null)
            {
                sb.Append($"{label}: (null)\n");
                return;
            }
            sb.Append($"{label} [{fx.Parameters.Count} params]:\n");
            foreach (var p in fx.Parameters)
            {
                sb.Append($"  '{p.Name}' {p.ParameterClass}/{p.ParameterType}");
                if (p.ParameterClass == EffectParameterClass.Object &&
                    (p.ParameterType == EffectParameterType.Texture ||
                     p.ParameterType == EffectParameterType.Texture2D))
                {
                    var tex = p.GetValueTexture2D();
                    sb.Append($" tex={(tex == null ? "null" : $"{tex.Width}x{tex.Height} {tex.Format} ptr={tex.GetHashCode():X8}")}");
                }
                else if (p.ParameterClass == EffectParameterClass.Scalar &&
                         p.ParameterType == EffectParameterType.Single)
                {
                    sb.Append($" = {p.GetValueSingle():F3}");
                }
                else if (p.ParameterClass == EffectParameterClass.Vector &&
                         p.ParameterType == EffectParameterType.Single)
                {
                    var v = p.GetValueVector3();
                    sb.Append($" = ({v.X:F3},{v.Y:F3},{v.Z:F3})");
                }
                sb.Append('\n');
            }
        }

        private static string ReadFirstUVs(Submesh s, int count)
        {
            if (s.Vertices == null) return "(no vbo)";
            try
            {
                int n = Math.Min(count, s.Vertices.VertexCount);
                var verts = new VertexSkinned[n];
                s.Vertices.GetData(verts);
                var sb = new StringBuilder();
                for (int i = 0; i < n; i++)
                    sb.Append($"({verts[i].TexCoord.X:F3},{verts[i].TexCoord.Y:F3}) ");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"GetData failed: {ex.Message}";
            }
        }

        private static string PatternLabel()      => $"Pattern: {MaterialDebug.TestPattern}";
        private static string SlotLabel()         => $"Slot: {MaterialDebug.SlotMode}";
        private static string SamplerLabel()      => $"Filter: {MaterialDebug.Sampler}";
        private static string HasDiffuseLabel()   => $"HasDiffuse force: " + MaterialDebug.ForceHasDiffuse switch
        {
            -1 => "off",
            0  => "0",
            _  => "1",
        };
        private static string FactorBoostLabel()  => $"FactorBoost: x{MaterialDebug.FactorBoost}";
        private static string ForceSlotLabel()    => $"ForceSlot: " + (MaterialDebug.ForceTextureSlot < 0 ? "off" : MaterialDebug.ForceTextureSlot.ToString());

        private void SetStatus(string text, ushort hue)
        {
            if (_statusLabel == null) return;
            _statusLabel.Text = text;
            _statusLabel.Hue = hue;
        }

        private void SetReadback(string text, ushort hue)
        {
            if (_readbackLabel == null) return;
            _readbackLabel.Text = text;
            _readbackLabel.Hue = hue;
        }

        private int AddSectionHeader(string title, int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSectionHeader(this, contentX, innerW, title, y);
        }
    }
}
