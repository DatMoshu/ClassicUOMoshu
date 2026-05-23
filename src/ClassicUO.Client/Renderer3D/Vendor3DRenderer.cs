// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — render non-player human mobiles (vendors / NPCs) as a
// shared default 3D mesh. ONE GLB is loaded and reused for every vendor; per-
// mobile state (anim time, motion detection) lives in a Dictionary keyed by
// Serial. Each draw rewinds the shared model's AnimTime/ActiveAnim to that
// vendor's values, calls Update(0) to refresh SkinMatrices, then draws.
//
// Activated by MobileRT3DRenderer.RenderVendorsAs3D. Has no effect when off.

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Game.GameObjects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class Vendor3DRenderer
    {
        public static string DefaultGlbPath = "Defaults/VendorNPC.glb";

        // Mirrors Player3DRenderer's tuning knobs so vendors render at the same
        // scale and orientation. Defaults match a Mixamo-normalized rig
        // (1.8m tall, +Z up in Blender → +Y up in glTF).
        public static float ModelScale = 60f;
        public static bool  LinkScaleToPlayer = true;  // mirrors Player3DRenderer.ModelScale when on
        public static float ModelPitchDegrees = 0f;
        public static float ModelYawDegrees = 0f;
        public static float ModelRollDegrees = 0f;
        public static float ModelYOffset = 0f;
        public static float AnimSpeed = 1f;
        public static bool  AutoYawFromFacing = true;
        public static float AutoYawOffsetDegrees = 180f;
        public static int   CullMode = 1; // 0=CCW, 1=CW(default), 2=None
        public static bool  SrgbOutput = true;
        public static float MotionStopTimeoutSec = 0.50f;

        // Anim slot names — matched case-insensitively against the GLB's
        // animation list. If a name doesn't match, falls back to anim 0.
        public static string IdleActionName = "Idle";
        public static string WalkActionName = "Walk";

        public static string LastError;

        private static SkinnedModelGlb _model;
        private static bool _loadFailed;
        private static TextureSkinnedEffect _effect;
        private static int _idleAnimIdx = 0;
        private static int _walkAnimIdx = 0;

        private sealed class VendorState
        {
            public float AnimTime;
            public float LastUoX, LastUoY, LastUoZ;
            public float LastMoveTimeSec;
            public bool  PrevWalking;
        }

        private static readonly Dictionary<uint, VendorState> _state = new();
        private static long _lastTickTicks;
        private static float _nowSec;

        public static void Invalidate()
        {
            _model?.Dispose();
            _model = null;
            _loadFailed = false;
            _state.Clear();
            LastError = null;
        }

        // Per-frame entry point — call ONCE per frame before drawing any vendor.
        // Advances wall-clock so per-vendor AnimTime can be ticked.
        public static void Tick()
        {
            long now = DateTime.UtcNow.Ticks;
            float dt = _lastTickTicks == 0 ? 0f : (float)((now - _lastTickTicks) / 10_000_000.0);
            _lastTickTicks = now;
            _nowSec += dt;
        }

        public static float DeltaTime
        {
            get
            {
                long now = DateTime.UtcNow.Ticks;
                if (_lastTickTicks == 0) return 0f;
                return (float)((now - _lastTickTicks) / 10_000_000.0);
            }
        }

        public static SkinnedModelGlb SharedModel => _model;

        public static void Draw(
            GraphicsDevice gd,
            Mobile mobile,
            Matrix view,
            Matrix projection)
        {
            if (gd == null || mobile == null) return;

            EnsureEffect(gd);
            if (_effect == null) return;

            var model = EnsureModel(gd);
            if (model == null) return;

            // Per-vendor state lookup / lazy create.
            if (!_state.TryGetValue(mobile.Serial, out var st))
            {
                st = new VendorState
                {
                    LastUoX = mobile.X,
                    LastUoY = mobile.Y,
                    LastUoZ = mobile.Z,
                    LastMoveTimeSec = float.NegativeInfinity,
                };
                _state[mobile.Serial] = st;
            }

            // Motion detection — same heuristic as Player3DRenderer but per
            // vendor. Vendors don't have client prediction so we just compare
            // tile coords across frames.
            float dx = mobile.X - st.LastUoX;
            float dy = mobile.Y - st.LastUoY;
            float dz = mobile.Z - st.LastUoZ;
            bool moved = (dx * dx + dy * dy + dz * dz) > 0.001f;
            if (moved)
            {
                st.LastUoX = mobile.X;
                st.LastUoY = mobile.Y;
                st.LastUoZ = mobile.Z;
                st.LastMoveTimeSec = _nowSec;
            }
            bool walking = (_nowSec - st.LastMoveTimeSec) < MotionStopTimeoutSec;

            // Pick anim slot — Idle vs Walk (fallback to anim 0 if names not
            // present in this GLB).
            int desired = walking ? _walkAnimIdx : _idleAnimIdx;
            if (model.Animations == null || model.Animations.Length == 0)
                return;
            desired = Math.Clamp(desired, 0, model.Animations.Length - 1);

            // Advance AnimTime locally then push to the shared model.
            float dt = ComputeDt();
            if (st.PrevWalking != walking) { st.AnimTime = 0f; }
            st.PrevWalking = walking;

            var anim = model.Animations[desired];
            if (anim.Duration > 0f)
                st.AnimTime = (st.AnimTime + dt * AnimSpeed) % anim.Duration;

            // Configure shared model for THIS vendor's pose, then refresh skin.
            model.ActiveAnim = desired;
            model.AnimTime = st.AnimTime;
            model.TargetAnim = -1;
            model.BlendWeight = 0f;
            model.Update(0f);

            // World matrix — same iso convention as Player3DRenderer.
            float wx = mobile.X * 22f;
            float wy = mobile.Z * 4f;
            float wz = mobile.Y * 22f;

            float autoYaw = AutoYawFromFacing
                ? UoDirectionToYaw((int)(mobile.Direction & ClassicUO.Game.Data.Direction.Mask)) + AutoYawOffsetDegrees
                : 0f;
            float effectiveYaw = autoYaw + ModelYawDegrees;

            float effectiveScale = LinkScaleToPlayer ? Player3DRenderer.ModelScale : ModelScale;
            var world =
                Matrix.CreateScale(effectiveScale) *
                Matrix.CreateRotationX(MathHelper.ToRadians(ModelPitchDegrees)) *
                Matrix.CreateRotationY(MathHelper.ToRadians(effectiveYaw)) *
                Matrix.CreateRotationZ(MathHelper.ToRadians(ModelRollDegrees)) *
                Matrix.CreateTranslation(wx, wy + ModelYOffset, wz);

            // Render state
            var prevDepth  = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevBlend  = gd.BlendState;
            gd.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1f, 0);
            gd.DepthStencilState = DepthStencilState.Default;
            gd.RasterizerState = CullMode switch
            {
                1 => RasterizerState.CullClockwise,
                2 => RasterizerState.CullNone,
                _ => RasterizerState.CullCounterClockwise,
            };
            gd.BlendState = BlendState.Opaque;

            _effect.World = world;
            _effect.View = view;
            _effect.Projection = projection;
            if (model.SkinMatrices != null && model.SkinMatrices.Length > 0)
                _effect.SetBoneTransforms(model.SkinMatrices);

            foreach (var sub in model.Submeshes)
            {
                if (!sub.Visible) continue;

                _effect.DiffuseTexture = MaterialDebug.ChooseTexture(gd, sub.Texture);
                var rgb = new Vector3(sub.BaseColorFactor.X, sub.BaseColorFactor.Y, sub.BaseColorFactor.Z) * MaterialDebug.FactorBoost;
                if (SrgbOutput)
                {
                    const float invG = 1f / 2.2f;
                    rgb = new Vector3(MathF.Pow(rgb.X, invG), MathF.Pow(rgb.Y, invG), MathF.Pow(rgb.Z, invG));
                }
                _effect.FallbackColor = rgb;

                if (MaterialDebug.ForceHasDiffuse >= 0)
                    _effect.SetHasDiffuseForce(MaterialDebug.ForceHasDiffuse);

                gd.SetVertexBuffer(sub.Vertices);
                gd.Indices = sub.Indices;

                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    MaterialDebug.ApplySamplerState(gd);
                    MaterialDebug.ForceBindTexture(gd, MaterialDebug.ChooseTexture(gd, sub.Texture));
                    gd.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        baseVertex: 0,
                        minVertexIndex: 0,
                        numVertices: sub.Vertices.VertexCount,
                        startIndex: 0,
                        primitiveCount: sub.IndexCount / 3
                    );
                }
            }

            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.BlendState = prevBlend;
        }

        public static void DropMobile(uint serial)
        {
            _state.Remove(serial);
        }

        private static float ComputeDt()
        {
            // Same wall-clock source as Tick(); sample without mutating state
            // so multiple vendors in the same frame see identical dt.
            long now = DateTime.UtcNow.Ticks;
            if (_lastTickTicks == 0) return 0f;
            return (float)((now - _lastTickTicks) / 10_000_000.0);
        }

        private static SkinnedModelGlb EnsureModel(GraphicsDevice gd)
        {
            if (_model != null) return _model;
            if (_loadFailed) return null;

            string full = Path.Combine(AppContext.BaseDirectory, "Models", DefaultGlbPath);
            if (!File.Exists(full))
            {
                LastError = $"vendor glb not found at {full}";
                Console.WriteLine($"[3DCUO][vendor] {LastError}");
                _loadFailed = true;
                return null;
            }

            try
            {
                _model = SkinnedModelGlb.Load(gd, full);
                _idleAnimIdx = Math.Max(0, _model.FindAnimByName(IdleActionName));
                _walkAnimIdx = Math.Max(0, _model.FindAnimByName(WalkActionName));
                if (_idleAnimIdx < 0) _idleAnimIdx = 0;
                if (_walkAnimIdx < 0) _walkAnimIdx = 0;
                Console.WriteLine($"[3DCUO][vendor] Loaded {full}: anims={_model.Animations.Length} idleIdx={_idleAnimIdx} walkIdx={_walkAnimIdx}");
                return _model;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[3DCUO][vendor] load failed: {ex}");
                _loadFailed = true;
                return null;
            }
        }

        private static void EnsureEffect(GraphicsDevice gd)
        {
            if (_effect != null) return;
            // Reuse the shared shader the player renderer already loaded if
            // available — avoids duplicate effect compilation. Otherwise load
            // independently from disk.
            if (TextureSkinnedEffectAccessor.Effect is TextureSkinnedEffect existing)
            {
                _effect = existing;
                return;
            }
            string fxcPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "TextureSkinning.fxc");
            if (!File.Exists(fxcPath))
            {
                LastError = $"shader not found at {fxcPath}";
                Console.WriteLine($"[3DCUO][vendor] {LastError}");
                return;
            }
            _effect = TextureSkinnedEffect.LoadFromFile(gd, fxcPath);
            TextureSkinnedEffectAccessor.Effect = _effect;
        }

        private static float UoDirectionToYaw(int dir)
        {
            switch (dir & 7)
            {
                case 0: return 0f;
                case 1: return 315f;
                case 2: return 270f;
                case 3: return 225f;
                case 4: return 180f;
                case 5: return 135f;
                case 6: return 90f;
                case 7: return 45f;
                default: return 0f;
            }
        }
    }
}
