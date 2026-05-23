// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO PROTOTYPE — diagnostic control that previews a live Texture2D via the
// standard 2D SpriteBatch path. Used by MaterialDebugGump to verify whether
// loaded textures are actually on the GPU (independent of our skinning shader).

using System;
using ClassicUO.Game.Scenes;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Controls
{
    internal sealed class Texture2DPreview : Control
    {
        // Resolver runs every frame so the preview re-samples whatever the
        // renderer last loaded — no need to refresh the gump manually.
        public Func<Texture2D> Resolver;

        public Texture2DPreview(int x, int y, int w, int h, Func<Texture2D> resolver)
        {
            X = x; Y = y; Width = w; Height = h;
            Resolver = resolver;
            AcceptMouseInput = false;
        }

        public override bool AddToRenderLists(RenderLists renderLists, int x, int y, ref float layerDepthRef)
        {
            float layerDepth = layerDepthRef;
            renderLists.AddGumpNoAtlas(batcher =>
            {
                var tex = Resolver?.Invoke();
                if (tex == null || tex.IsDisposed)
                {
                    batcher.Draw(SolidColorTextureCache.GetTexture(Color.DarkRed),
                        new Rectangle(x, y, Width, Height), Vector3.Zero, layerDepth);
                    return true;
                }
                batcher.Draw(tex, new Rectangle(x, y, Width, Height), Vector3.Zero, layerDepth);
                // 1px white border so the preview is visible against the gump bg
                batcher.DrawRectangle(SolidColorTextureCache.GetTexture(Color.White),
                    x, y, Width, Height, Vector3.Zero, layerDepth);
                return true;
            });
            return true;
        }
    }
}
