// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Runtime.CompilerServices;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.IO;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    enum ObjectHandlesStatus
    {
        NONE,
        OPEN,
        CLOSED,
        DISPLAYING
    }

    internal abstract partial class GameObject
    {
        public byte AlphaHue;
        public bool AllowedToDraw = true;
        public bool InChunkMesh;
        public int MeshSpriteIndex = -1;
        public ObjectHandlesStatus ObjectHandlesStatus;
        public Rectangle FrameInfo;
        protected bool IsFlipped;

        public abstract bool Draw(UltimaBatcher2D batcher, int posX, int posY, float depth);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateDepthZ()
        {
            int x = X;
            int y = Y;
            int z = PriorityZ;

            // Offsets are in SCREEN coordinates
            if (Offset.X > 0 && Offset.Y < 0)
            {
                // North
            }
            else if (Offset.X > 0 && Offset.Y == 0)
            {
                // Northeast
                x++;
            }
            else if (Offset.X > 0 && Offset.Y > 0)
            {
                // East
                z += Math.Max(0, (int)Offset.Z);
                x++;
            }
            else if (Offset.X == 0 && Offset.Y > 0)
            {
                // Southeast
                x++;
                y++;
            }
            else if (Offset.X < 0 && Offset.Y > 0)
            {
                // South
                z += Math.Max(0, (int)Offset.Z);
                y++;
            }
            else if (Offset.X < 0 && Offset.Y == 0)
            {
                // Southwest
                y++;
            }
            else if (Offset.X < 0 && Offset.Y > 0)
            {
                // West
            }
            else if (Offset.X == 0 && Offset.Y < 0)
            {
                // Northwest
            }

            return (x + y) + (127 + z) * 0.01f;
        }

        public Rectangle GetOnScreenRectangle()
        {
            Rectangle prect = Rectangle.Empty;

            prect.X = (int)(RealScreenPosition.X - FrameInfo.X + 22 + Offset.X);
            prect.Y = (int)(RealScreenPosition.Y - FrameInfo.Y + 22 + (Offset.Y - Offset.Z));
            prect.Width = FrameInfo.Width;
            prect.Height = FrameInfo.Height;

            return prect;
        }

        public virtual bool TransparentTest(int z)
        {
            return false;
        }

        protected static void DrawStatic(
            UltimaBatcher2D batcher,
            ushort graphic,
            int x,
            int y,
            Vector3 hue,
            float depth,
            bool isWet = false
        )
        {
            ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(graphic);

            if (artInfo.Texture != null)
            {
                ref var index = ref Client.Game.UO.FileManager.Arts.File.GetValidRefEntry(graphic + 0x4000);

                float scaleFactor = artInfo.Scale > 0 ? artInfo.Scale : 1.0f;

                if (artInfo.Pivot.X != 0 || artInfo.Pivot.Y != 0)
                {
                    index.Width = (short)(artInfo.Pivot.X * scaleFactor - 22);
                    index.Height = (short)(artInfo.Pivot.Y * scaleFactor - 44);
                }
                else
                {
                    index.Width = (short)((artInfo.UV.Width * scaleFactor / 2) - 22);
                    index.Height = (short)(artInfo.UV.Height * scaleFactor - 44);
                }

                x -= index.Width;
                y -= index.Height;

                var pos = new Vector2(x, y);
                var scale = new Vector2(scaleFactor);
                if (isWet)
                {
                    batcher.Draw(
                        artInfo.Texture,
                        pos,
                        artInfo.UV,
                        hue,
                        0f,
                        Vector2.Zero,
                        scale,
                        SpriteEffects.None,
                        depth + 0.5f
                    );

                    var sin = (float)Math.Sin(Time.Ticks / 1000f);
                    var cos = (float)Math.Cos(Time.Ticks / 1000f);
                    scale = new Vector2(1.1f + sin * 0.1f, 1.1f + cos * 0.5f * 0.1f);
                }

                batcher.Draw(
                    artInfo.Texture,
                    pos,
                    artInfo.UV,
                    hue,
                    0f,
                    Vector2.Zero,
                    scale,
                    SpriteEffects.None,
                    depth + 0.5f
                );
            }
        }

        protected static void DrawGump(
            UltimaBatcher2D batcher,
            ushort graphic,
            int x,
            int y,
            Vector3 hue,
            float depth
        )
        {
            ref readonly var gumpInfo = ref Client.Game.UO.Gumps.GetGump(graphic);

            if (gumpInfo.Texture != null)
            {
                batcher.Draw(
                    gumpInfo.Texture,
                    new Vector2(x, y),
                    gumpInfo.UV,
                    hue,
                    0f,
                    Vector2.Zero,
                    1f,
                    SpriteEffects.None,
                    depth + 0.5f
                );
            }
        }

        protected static void DrawStaticRotated(
            UltimaBatcher2D batcher,
            ushort graphic,
            int x,
            int y,
            float angle,
            Vector3 hue,
            float depth
        )
        {
            ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(graphic);

            if (artInfo.Texture != null)
            {
                ref var index = ref Client.Game.UO.FileManager.Arts.File.GetValidRefEntry(graphic + 0x4000);

                float scaleFactor = artInfo.Scale > 0 ? artInfo.Scale : 1.0f;

                if (artInfo.Pivot.X != 0 || artInfo.Pivot.Y != 0)
                {
                    index.Width = (short)(artInfo.Pivot.X * scaleFactor - 22);
                    index.Height = (short)(artInfo.Pivot.Y * scaleFactor - 44);
                }
                else
                {
                    index.Width = (short)((artInfo.UV.Width * scaleFactor / 2) - 22);
                    index.Height = (short)(artInfo.UV.Height * scaleFactor - 44);
                }

                batcher.Draw(
                    artInfo.Texture,
                    new Rectangle(
                        x - index.Width,
                        y - index.Height,
                        (int)(artInfo.UV.Width * scaleFactor),
                        (int)(artInfo.UV.Height * scaleFactor)
                    ),
                    artInfo.UV,
                    hue,
                    angle,
                    Vector2.Zero,
                    SpriteEffects.None,
                    depth + 0.5f
                );
            }
        }

        protected static void DrawStaticAnimated(
            UltimaBatcher2D batcher,
            ushort graphic,
            int x,
            int y,
            Vector3 hue,
            bool shadow,
            float depth,
            bool isWet = false,
            bool tileExposedToSky = true
        )
        {
            ref UOFileIndex index = ref Client.Game.UO.FileManager.Arts.File.GetValidRefEntry(graphic + 0x4000);

            graphic = (ushort)(graphic + index.AnimOffset);

            ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(graphic);

            if (artInfo.Texture != null)
            {
                index = ref Client.Game.UO.FileManager.Arts.File.GetValidRefEntry(graphic + 0x4000);

                float scaleFactor = artInfo.Scale > 0 ? artInfo.Scale : 1.0f;

                if (artInfo.Pivot.X != 0 || artInfo.Pivot.Y != 0)
                {
                    index.Width = (short)(artInfo.Pivot.X * scaleFactor - 22);
                    index.Height = (short)(artInfo.Pivot.Y * scaleFactor - 44);
                }
                else
                {
                    index.Width = (short)((artInfo.UV.Width * scaleFactor / 2) - 22);
                    index.Height = (short)(artInfo.UV.Height * scaleFactor - 44);
                }

                x -= index.Width;
                y -= index.Height;

                Vector2 pos = new Vector2(x, y);

                // Seasonal recolor for tree-leaf statics: substitute the
                // runtime-recolored Texture2D in place of the atlas region.
                // Applies to both 2D and 3D rendering paths (3D side does this
                // in Static3DRenderer.ResolveDrawTexture).
                Microsoft.Xna.Framework.Graphics.Texture2D drawTex = artInfo.Texture;
                Microsoft.Xna.Framework.Rectangle drawUV = artInfo.UV;
                if (ClassicUO.Game.Data.StaticFilters.IsTreeLeaf(graphic))
                {
                    var recolored = ClassicUO.Renderer.Renderer3D.TreeTextureCache.Get(
                        graphic, applySnow: tileExposedToSky);
                    if (recolored != null)
                    {
                        drawTex = recolored;
                        drawUV = new Microsoft.Xna.Framework.Rectangle(0, 0, recolored.Width, recolored.Height);
                    }
                }

                if (shadow)
                {
                    batcher.DrawShadow(drawTex, pos, drawUV, false, depth + 0.25f);
                }

                var scale = new Vector2(scaleFactor);
                if (isWet)
                {
                    batcher.Draw(
                        drawTex,
                        pos,
                        drawUV,
                        hue,
                        0f,
                        Vector2.Zero,
                        scale,
                        SpriteEffects.None,
                        depth + 0.5f
                    );

                    var sin = (float)Math.Sin(Time.Ticks / 1000f);
                    var cos = (float)Math.Cos(Time.Ticks / 1000f);
                    scale = new Vector2(1.1f + sin * 0.1f, 1.1f + cos * 0.5f * 0.1f);
                }

                batcher.Draw(
                    drawTex,
                    pos,
                    drawUV,
                    hue,
                    0f,
                    Vector2.Zero,
                    scale,
                    SpriteEffects.None,
                    depth + 0.5f
                );
            }
        }
    }
}
