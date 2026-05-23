// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Tools.GumpSnapshot
{
    /// <summary>
    /// One-shot snapshot driver. Called from <c>LoginScene.Load()</c> when
    /// <see cref="CUOEnviroment.GumpSnapshotFactory"/> is set, BEFORE the
    /// login UI is constructed. Instantiates the named client-built gump,
    /// renders it to an offscreen <see cref="RenderTarget2D"/>, writes a PNG,
    /// then exits the client.
    ///
    /// The gump is rendered through the standard <see cref="UIManager.Draw"/>
    /// pipeline (which handles batcher Begin/End, render lists, and depth
    /// sorting); we just bind a non-default render target before calling it
    /// and only have one gump open at the time.
    /// </summary>
    internal static class GumpSnapshotController
    {
        public static void RunOnce()
        {
            var world = Client.Game?.UO?.World;
            if (world == null)
            {
                Log.Error("[gump-snapshot] no World available; assets may not be loaded yet");
                return;
            }

            // Close any pre-existing gumps so only the snapshot target renders.
            ClearAllGumps();

            // Choose source: replay a captured 0xDD .bin, or build via the
            // client-side factory registry.
            Gump gump;
            string outName;
            var binPath = CUOEnviroment.GumpSnapshotBinPath;
            var factoryName = CUOEnviroment.GumpSnapshotFactory;

            if (!string.IsNullOrWhiteSpace(binPath))
            {
                try
                {
                    gump = GumpReplayBuilder.BuildFromBin(world, binPath);
                }
                catch (Exception ex)
                {
                    Log.Error($"[gump-snapshot] replay '{binPath}' threw: {ex}");
                    return;
                }
                outName = CUOEnviroment.GumpSnapshotOutName
                          ?? System.IO.Path.GetFileNameWithoutExtension(binPath);
            }
            else if (!string.IsNullOrWhiteSpace(factoryName))
            {
                if (!ClientGumpFactoryRegistry.TryGet(factoryName, out var factory))
                {
                    Log.Error($"[gump-snapshot] unknown factory '{factoryName}'. Known: {string.Join(", ", ClientGumpFactoryRegistry.Names)}");
                    return;
                }
                try
                {
                    gump = factory(world);
                }
                catch (Exception ex)
                {
                    Log.Error($"[gump-snapshot] factory '{factoryName}' threw: {ex}");
                    return;
                }
                outName = factoryName;
            }
            else
            {
                Log.Warn("[gump-snapshot] no factory name or bin path; aborting");
                return;
            }

            if (gump == null)
            {
                Log.Error($"[gump-snapshot] gump construction returned null");
                return;
            }

            // Force the gump to (0,0) so the render target only covers its own
            // bounds — no offset waste, deterministic PNG size.
            gump.X = 0;
            gump.Y = 0;
            Log.Trace($"[gump-snapshot] step: UIManager.Add (gump.Width={gump.Width}, gump.Height={gump.Height})");
            UIManager.Add(gump);

            // One Update tick to resolve initial layout / lazy children.
            Log.Trace("[gump-snapshot] step: UIManager.Update");
            try { UIManager.Update(); }
            catch (Exception ex) { Log.Warn($"[gump-snapshot] UIManager.Update threw (continuing): {ex.Message}"); }

            int w = Math.Max(1, gump.Width);
            int h = Math.Max(1, gump.Height);

            Log.Trace($"[gump-snapshot] step: alloc RenderTarget2D {w}x{h}");
            var device = Client.Game.GraphicsDevice;
            var prevTargets = device.GetRenderTargets();

            using var rt = new RenderTarget2D(device, w, h, false, SurfaceFormat.Color, DepthFormat.None);
            Log.Trace("[gump-snapshot] step: SetRenderTarget(rt)");
            device.SetRenderTarget(rt);
            device.Clear(new Color(0, 0, 0, 0));

            Log.Trace("[gump-snapshot] step: UIManager.Draw");
            var batcher = new UltimaBatcher2D(device);
            try { UIManager.Draw(batcher); }
            catch (Exception ex) { Log.Error($"[gump-snapshot] UIManager.Draw threw: {ex}"); }

            Log.Trace("[gump-snapshot] step: restore RenderTarget");
            if (prevTargets == null || prevTargets.Length == 0)
                device.SetRenderTarget(null);
            else
                device.SetRenderTargets(prevTargets);

            Log.Trace("[gump-snapshot] step: build outPath");
            var outPath = ResolveOutputPath(outName);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");

            try
            {
                using var stream = File.OpenWrite(outPath);
                rt.SaveAsPng(stream, w, h);
                Log.Trace($"[gump-snapshot] wrote {outPath} ({w}x{h})");
                Console.Out.WriteLine($"GUMP_SNAPSHOT_OUT={outPath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[gump-snapshot] PNG save failed: {ex}");
            }
        }

        private static void ClearAllGumps()
        {
            // Snapshot the list to avoid mutation while iterating.
            var snapshot = new System.Collections.Generic.List<Gump>(UIManager.Gumps);
            foreach (var g in snapshot)
            {
                try { g.Dispose(); } catch { /* ignore */ }
            }
        }

        private static string ResolveOutputPath(string factoryName)
        {
            var outDir = CUOEnviroment.GumpSnapshotOutDir;
            if (string.IsNullOrWhiteSpace(outDir))
            {
                outDir = Path.Combine(
                    CUOEnviroment.ExecutablePath,
                    "gump-snapshots"
                );
            }
            return Path.Combine(outDir, factoryName + ".png");
        }
    }
}
