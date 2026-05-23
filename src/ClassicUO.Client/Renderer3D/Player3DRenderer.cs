// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — Phase 3: render the local player as a 3D skinned mesh.
// Loads TestChar.glb at first use, advances the first animation in a loop,
// places the model at the player's UO world position (converted to 3D world
// units) and draws via FNA's SkinnedEffect.

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal enum AnimState { Idle = 0, Run = 1, Hit = 2, Attack = 3, Walk = 4, Die = 5 }

    internal static class Player3DRenderer
    {
        public static bool Enabled = true;

        // Per-state glb paths, relative to Models/ under the binary's working dir.
        // Each glb should contain one animation that drives the rig.
        public static readonly Dictionary<AnimState, string> StatePaths = new()
        {
            { AnimState.Idle,   "anims/Idle_001.glb" },
            { AnimState.Run,    "anims/Run_001.glb" },
            { AnimState.Hit,    "anims/Hit_001.glb" },
            { AnimState.Attack, "anims/Attack_Punch_001.glb" },
        };

        // Single-glb mode: one model holds every action by name. Toggle via Debug3DGump.
        public static bool   UseSingleGlb = false;
        public static string SingleGlbPath = "PlayerModel.glb";
        public static readonly Dictionary<AnimState, string> StateActionNames = new()
        {
            { AnimState.Idle,   "Idle" },
            { AnimState.Run,    "Run" },
            { AnimState.Walk,   "Walk" },
            { AnimState.Hit,    "Hit" },
            { AnimState.Attack, "Attack" },
            { AnimState.Die,    "Die" },
        };

        // Cross-fade duration in seconds when switching states (only used in single-glb mode).
        public static float BlendDurationSec = 0.20f;
        private static float _blendT;          // 0..BlendDurationSec, counts up while blending
        private static int   _blendFromAnim = -1;
        private static int   _blendToAnim   = -1;

        // Auto state machine
        public static bool  AutoStateFromMovement = true;
        // Grace period after the last detected position change before we flip
        // back to Idle. Must be longer than a single UO step duration: a run
        // step is ~200 ms, walk ~400 ms. In MobileRT mode position only updates
        // at tile boundaries (no interpolation), so anything shorter than a
        // step's worth of time will flicker back to Idle between tiles.
        public static float MotionStopTimeoutSec = 0.50f;
        public static AnimState BaselineState = AnimState.Idle;  // what to show when no movement / no override

        // Legacy single-file path (still respected if ModelPath is set explicitly).
        // Empty = use StatePaths instead.
        public static string ModelPath = "";
        public static float ModelScale = 30.7f;         // glTF unit -> world pixel. Tuned default for the current Synty body GLB; was 60 for the older Mixamo source.
        public static float ModelPitchDegrees = 0f;     // rotate around X-axis (lift model upright)
        public static float ModelYawDegrees = 0f;       // manual yaw (added on top of auto-yaw)
        public static float ModelRollDegrees = 0f;      // rotate around Z-axis
        public static float ModelYOffset = 0f;          // manual Y nudge to seat feet on ground
        public static int   AnimIndex = 0;
        public static float AnimSpeed = 1f;
        public static bool  TPoseOnly = false;          // skip animation eval — useful to debug skinning vs animation

        // Freeze the idle clip on a single frame instead of looping it. Helps the
        // 3D player blend in with surrounding 2D mobiles whose idle frame is also
        // static. Only applies while CurrentState == Idle; movement/one-shots
        // still play normally.
        public static bool  StaticIdle = false;
        public static float StaticIdleTimeSec = 0f;     // which frame of the idle clip to hold
        public static bool  DrawPositionMarker = true;  // 3D origin marker for diagnosing "where is the model"

        // Auto-yaw from UO 8-way Direction. Auto + manual ModelYawDegrees compose.
        public static bool  AutoYawFromFacing = true;
        public static float AutoYawOffsetDegrees = 180f;  // Mixamo characters face +Z, UO North = -Z, so flip 180

        // Skip the glTF baseColorFactor and render every submesh as flat white.
        // Useful when material data is wrong; turn off to see the authored colors.
        public static bool  ForceWhiteMaterial = false;

        // glTF stores BaseColor in linear RGB; Blender's viewport displays via sRGB.
        // When true, gamma-correct the FallbackColor (pow 1/2.2) before sending it
        // to the shader so the in-game render approximately matches Blender.
        public static bool  SrgbOutput = true;

        // 0 = CullCounterClockwise, 1 = CullClockwise (default), 2 = CullNone.
        // Empirically, with this view/projection setup the glTF outer/front faces
        // come out as CCW in screen space, so we cull CW (= back). The original
        // CullCounterClockwise choice was wrong and produced see-through holes.
        public static int   CullMode = 1;

        // When true, every skinned mobile (player + composed NPCs) renders as
        // wireframe instead of solid. Forces CullMode=None internally so back-
        // faces are also drawn as wires. Toggled from Npc3DDebugGump; covers
        // both the inline-3D path (this file) and the AttachmentRenderer pass
        // used by both the RT path and the inline NPC pass.
        public static bool  MobilesWireframe = false;
        private static RasterizerState _mobileWireframeState;
        internal static RasterizerState GetMobileWireframeState()
            => _mobileWireframeState ??= new RasterizerState
            {
                FillMode = FillMode.WireFrame,
                CullMode = Microsoft.Xna.Framework.Graphics.CullMode.None,
            };

        // Submesh visibility — case-insensitive substring match against submesh
        // name. Empty = render everything. Set to "Joint" to hide Mixamo joint-ball
        // helper meshes, but only if your body submesh isn't ALSO named "*Joint*".
        public static string HideSubmeshNameMatch = "";

        // When true, body submeshes whose names contain "hair" or "beard" are
        // skipped while a hat/helmet is loaded in AttachmentRenderer's Head
        // slot. Avoids hair clipping through helmets without forcing the user
        // to manually toggle slot visibility every time they swap headwear.
        public static bool AutoHideHairBeardWhenHat = true;

        // Cached helmet-equipped flag — set once per frame in Update() and read
        // by the submesh draw loop and by AttachmentRenderer when deciding
        // whether to skip the Hair attachment slot.
        public static bool IsHatEquipped { get; internal set; }

        public static string LastError;

        // Currently-active model (the one being drawn this frame). Public so the
        // Debug/ModelInfo gumps can introspect it.
        public static SkinnedModelGlb Model;

        // World matrix used to draw the model this frame. HatRenderer composes against this
        // so the hat inherits player position + facing.
        public static Matrix LastWorldMatrix = Matrix.Identity;

        // Per-state cached models.
        private static readonly Dictionary<AnimState, SkinnedModelGlb> _models = new();
        private static readonly HashSet<AnimState> _failedLoads = new();
        private static SkinnedModelGlb _singleModel;
        private static bool _singleLoadFailed;
        private static TextureSkinnedEffect _effect;
        private static BasicEffect _markerEffect;
        private static VertexBuffer _markerVbo;
        private static long _lastTickTicks;
        public static AnimState CurrentState { get; private set; } = AnimState.Idle;

        // Movement detection state
        private static float _lastUoX, _lastUoY, _lastUoZ;
        private static float _lastMovedTime;
        private static float _nowSec;

        // ---- Diagnostics: rolling history of state changes + motion samples ----
        public struct StateLogEntry
        {
            public float TimeSec;       // _nowSec at the change
            public AnimState From;
            public AnimState To;
            public float UoX, UoY, UoZ;
            public float DeltaSinceLastSample;
            public float TimeSinceLastMotion;
            public bool  IsMoving;
        }
        private const int LOG_CAPACITY = 64;
        private static readonly StateLogEntry[] _log = new StateLogEntry[LOG_CAPACITY];
        private static int _logHead;     // next write index
        private static int _logCount;    // total writes (clamped capacity for read)
        public static int TotalStateChanges;
        public static int TotalMotionFrames;
        public static float LastFrameDelta;     // last per-frame |dPos|
        public static int CallCount;            // how many times Draw() ran (helps detect double-call)

        public static IReadOnlyList<StateLogEntry> RecentStateLog()
        {
            int n = Math.Min(_logCount, LOG_CAPACITY);
            var list = new List<StateLogEntry>(n);
            // Walk oldest-to-newest.
            int start = _logCount > LOG_CAPACITY ? _logHead : 0;
            for (int i = 0; i < n; i++)
                list.Add(_log[(start + i) % LOG_CAPACITY]);
            return list;
        }

        private static void LogStateChange(AnimState from, AnimState to)
        {
            _log[_logHead] = new StateLogEntry
            {
                TimeSec = _nowSec,
                From = from, To = to,
                UoX = _lastUoX, UoY = _lastUoY, UoZ = _lastUoZ,
                DeltaSinceLastSample = LastFrameDelta,
                TimeSinceLastMotion = _nowSec - _lastMovedTime,
                IsMoving = IsMoving(),
            };
            _logHead = (_logHead + 1) % LOG_CAPACITY;
            _logCount++;
            TotalStateChanges++;
        }

        // One-shot override (Hit / Attack)
        private static AnimState? _oneShotState;
        private static float _oneShotEndSec;

        public static void TriggerOneShot(AnimState state, float durationSec = 0f)
        {
            // durationSec=0 -> auto-derive from the loaded clip's duration once we have it.
            _oneShotState = state;
            _oneShotEndSec = _nowSec + (durationSec > 0f ? durationSec : 5f);
        }

        public static string CurrentStatePath()
            => _models.TryGetValue(CurrentState, out var m) ? StatePaths.GetValueOrDefault(CurrentState, "?") : "(unloaded)";

        public static void InvalidateAll()
        {
            foreach (var m in _models.Values) m?.Dispose();
            _models.Clear();
            _failedLoads.Clear();
            _singleModel?.Dispose();
            _singleModel = null;
            _singleLoadFailed = false;
            _blendFromAnim = _blendToAnim = -1;
            _blendT = 0f;
            Model = null;
            LastError = null;
            TPoseOnly = false;
        }

        public static void Draw(
            GraphicsDevice gd,
            float playerUoX, float playerUoY, float playerUoZ,
            int facingDirection,
            Matrix view, Matrix projection
        )
        {
            if (!Enabled || gd == null) return;

            // Advance wall-clock time
            long now = DateTime.UtcNow.Ticks;
            float dt = _lastTickTicks == 0 ? 0f : (float)((now - _lastTickTicks) / 10_000_000.0);
            _lastTickTicks = now;
            _nowSec += dt;
            CallCount++;

            EnsureEffect(gd);

            // Even if no model loads, the position marker gives a visible
            // reference for "where the 3D player coords actually map".
            if (DrawPositionMarker)
                DrawMarker(gd, playerUoX, playerUoY, playerUoZ, view, projection);

            // ---- Decide active state ----
            UpdateMotion(playerUoX, playerUoY, playerUoZ);

            AnimState targetState;
            if (_oneShotState.HasValue && _nowSec < _oneShotEndSec)
                targetState = _oneShotState.Value;
            else
            {
                _oneShotState = null;
                if (AutoStateFromMovement)
                    targetState = IsMoving() ? AnimState.Run : BaselineState;
                else
                    targetState = BaselineState;
            }

            // ---- Get the model for that state ----
            bool stateChanged = CurrentState != targetState || Model == null;
            if (stateChanged)
            {
                LogStateChange(CurrentState, targetState);
                CurrentState = targetState;
            }

            if (UseSingleGlb)
            {
                Model = EnsureSingleModel(gd);
                if (Model == null) return;

                int desiredAnim = ResolveAnimIndex(Model, targetState);
                if (stateChanged && desiredAnim >= 0 && desiredAnim != Model.ActiveAnim && BlendDurationSec > 0f)
                {
                    // Begin a new cross-fade. If we're already mid-blend, snap the
                    // current visual pose by collapsing the prior target into ActiveAnim.
                    if (_blendToAnim >= 0)
                    {
                        Model.ActiveAnim = _blendToAnim;
                        Model.AnimTime = Model.TargetAnimTime;
                    }
                    _blendFromAnim = Model.ActiveAnim;
                    _blendToAnim   = desiredAnim;
                    _blendT = 0f;
                    Model.TargetAnim = desiredAnim;
                    Model.TargetAnimTime = 0f;
                }
                else if (!stateChanged && _blendToAnim < 0 && desiredAnim >= 0)
                {
                    // Steady-state: just keep the right anim selected.
                    Model.ActiveAnim = desiredAnim;
                }

                if (_blendToAnim >= 0)
                {
                    _blendT += dt;
                    float w = BlendDurationSec > 0f ? Math.Clamp(_blendT / BlendDurationSec, 0f, 1f) : 1f;
                    Model.BlendWeight = w;
                    if (w >= 1f)
                    {
                        // Promote target -> active, end blend.
                        Model.ActiveAnim = _blendToAnim;
                        Model.AnimTime = Model.TargetAnimTime;
                        Model.TargetAnim = -1;
                        Model.BlendWeight = 0f;
                        _blendFromAnim = _blendToAnim = -1;
                        _blendT = 0f;
                    }
                }
            }
            else
            {
                Model = EnsureStateModel(gd, targetState);
                if (Model == null) return;

                // Each per-state glb often contains every Action that has fake_user
                // in the source .blend, not just the relevant one. The most-recently
                // added Action in Blender ends up as the LAST animation in the
                // exported glb, so default to that. Override-able via AnimIndex
                // slider if the user wants a specific one.
                int defaultIdx = Math.Max(0, Model.Animations.Length - 1);
                Model.ActiveAnim = defaultIdx;
                Model.TargetAnim = -1;
                Model.BlendWeight = 0f;
            }

            if (TPoseOnly)
                Model.ResetToBindPose();
            else if (StaticIdle && CurrentState == AnimState.Idle)
            {
                Model.AnimTime = StaticIdleTimeSec;
                Model.TargetAnim = -1;
                Model.BlendWeight = 0f;
                Model.Update(0f);
            }
            else
                Model.Update(dt * AnimSpeed);

            // Place at player position
            float wx = playerUoX * 22f;
            float wy = playerUoZ * 4f;
            float wz = playerUoY * 22f;

            // Compose auto-yaw (from facing direction) + manual offset.
            float autoYaw = AutoYawFromFacing ? UoDirectionToYaw(facingDirection) + AutoYawOffsetDegrees : 0f;
            float effectiveYaw = autoYaw + ModelYawDegrees;

            var world =
                Matrix.CreateScale(ModelScale) *
                Matrix.CreateRotationX(MathHelper.ToRadians(ModelPitchDegrees)) *
                Matrix.CreateRotationY(MathHelper.ToRadians(effectiveYaw)) *
                Matrix.CreateRotationZ(MathHelper.ToRadians(ModelRollDegrees)) *
                Matrix.CreateTranslation(wx, wy + ModelYOffset, wz);
            LastWorldMatrix = world;

            // Render state
            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevBlend = gd.BlendState;
            // NOTE: previously this cleared the depth buffer to fix an "X-ray"
            // look caused by stale 2D-batcher z values. Both current call sites
            // now hand us a depth buffer we want to KEEP:
            //   * MobileRT3DRenderer.RenderMobile clears its per-mobile RT's
            //     depth buffer one line before calling Draw (line 165 there) —
            //     a second clear here is redundant.
            //   * World3DRenderer's inline pass relies on the per-pixel depth
            //     written by the heightmap / multi / static passes so walls
            //     occlude the player. Clearing depth here was making the player
            //     and every NPC attachment draw on top of walls regardless of
            //     where they stood. (Self-occlusion of the model still works:
            //     back-face culling + per-pixel z-write handles it as we draw.)
            gd.DepthStencilState = DepthStencilState.Default;
            // Cull back faces so we only see the outer skin, not interior tris.
            // Toggle CullMode at runtime via PlayerMaterialGump if the model has
            // inverted winding from the glTF→FNA handedness flip.
            gd.RasterizerState = MobilesWireframe
                ? GetMobileWireframeState()
                : (CullMode switch
                {
                    1 => RasterizerState.CullClockwise,
                    2 => RasterizerState.CullNone,
                    _ => RasterizerState.CullCounterClockwise,
                });
            gd.BlendState = BlendState.Opaque;

            _effect.World = world;
            _effect.View = view;
            _effect.Projection = projection;
            if (Model.SkinMatrices != null && Model.SkinMatrices.Length > 0)
                _effect.SetBoneTransforms(Model.SkinMatrices);

            // Refresh the cached "is a hat loaded?" flag once per frame. Cheap
            // (single index lookup); avoids re-evaluating it for every submesh.
            IsHatEquipped =
                AttachmentRenderer.Slots != null &&
                (int)AttachSlotKind.Head < AttachmentRenderer.Slots.Length &&
                AttachmentRenderer.Slots[(int)AttachSlotKind.Head].CurrentIndex >= 0;

            // One draw call per submesh — each carries its own texture + color factor.
            foreach (var sub in Model.Submeshes)
            {
                if (!sub.Visible) continue;
                if (!string.IsNullOrEmpty(HideSubmeshNameMatch) &&
                    sub.MeshName != null &&
                    sub.MeshName.IndexOf(HideSubmeshNameMatch, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (AutoHideHairBeardWhenHat && IsHatEquipped && sub.MeshName != null &&
                    (sub.MeshName.IndexOf("hair",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sub.MeshName.IndexOf("beard", StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                if (ForceWhiteMaterial)
                {
                    // Flat white, light-shaded only. Skip texture/factor entirely.
                    _effect.DiffuseTexture = null;
                    _effect.FallbackColor = Vector3.One;
                }
                else
                {
                    _effect.DiffuseTexture = MaterialDebug.ChooseTexture(gd, sub.Texture);
                    var rgb = new Vector3(sub.BaseColorFactor.X, sub.BaseColorFactor.Y, sub.BaseColorFactor.Z) * MaterialDebug.FactorBoost;
                    if (SrgbOutput)
                    {
                        const float invG = 1f / 2.2f;
                        rgb = new Vector3(MathF.Pow(rgb.X, invG), MathF.Pow(rgb.Y, invG), MathF.Pow(rgb.Z, invG));
                    }
                    _effect.FallbackColor = rgb;
                }

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

            // Diagnostic: render a textured cube via FNA's stock BasicEffect
            // sampling the first loaded attachment texture. Definitive test
            // for whether textures can be rendered at all on this build (if
            // YES → bug is in our custom skinning shader).
            if (TextureDebugCube.Enabled)
            {
                var debugTex = TextureDebugCube.ResolveAttachmentTexture();
                TextureDebugCube.Draw(gd, view, projection, new Vector3(wx, wy + ModelYOffset, wz), debugTex);
            }
        }

        // UO 8-way Direction enum -> world Y-axis rotation in degrees.
        // Default model "front" is -Z (glTF convention). North in UO = -uoY = -worldZ
        // (looking at +world from camera), so North maps to yaw=0.
        // Made internal so World3DRenderer's inline NPC pass can compose the
        // same per-mobile world matrix this file uses for the player.
        internal static float UoDirectionToYaw(int dir)
        {
            switch (dir & 7)
            {
                case 0: return 0f;     // North
                case 1: return 315f;   // Right (NE)
                case 2: return 270f;   // East
                case 3: return 225f;   // Down (SE)
                case 4: return 180f;   // South
                case 5: return 135f;   // Left (SW)
                case 6: return 90f;    // West
                case 7: return 45f;    // Up (NW)
                default: return 0f;
            }
        }

        private static void DrawMarker(GraphicsDevice gd, float uoX, float uoY, float uoZ, Matrix view, Matrix projection)
        {
            if (_markerEffect == null)
            {
                _markerEffect = new BasicEffect(gd) { VertexColorEnabled = true, LightingEnabled = false, TextureEnabled = false };

                // Tall thin magenta box — 22 px wide, 88 px tall (~UO sprite height) — so we
                // see exactly where the player should be drawn at full character height.
                var c = Color.Magenta;
                Vector3 a = new Vector3(-11, 0, -11), b = new Vector3(11, 0, 11);
                Vector3 a2 = new Vector3(-11, 88, -11), b2 = new Vector3(11, 88, 11);
                var verts = new VertexPositionColor[]
                {
                    // bottom face
                    new(new Vector3(a.X,a.Y,a.Z), c), new(new Vector3(b.X,a.Y,a.Z), c),
                    new(new Vector3(b.X,a.Y,a.Z), c), new(new Vector3(b.X,a.Y,b.Z), c),
                    new(new Vector3(b.X,a.Y,b.Z), c), new(new Vector3(a.X,a.Y,b.Z), c),
                    new(new Vector3(a.X,a.Y,b.Z), c), new(new Vector3(a.X,a.Y,a.Z), c),
                    // top face
                    new(new Vector3(a.X,a2.Y,a.Z), c), new(new Vector3(b.X,a2.Y,a.Z), c),
                    new(new Vector3(b.X,a2.Y,a.Z), c), new(new Vector3(b.X,a2.Y,b.Z), c),
                    new(new Vector3(b.X,a2.Y,b.Z), c), new(new Vector3(a.X,a2.Y,b.Z), c),
                    new(new Vector3(a.X,a2.Y,b.Z), c), new(new Vector3(a.X,a2.Y,a.Z), c),
                    // verticals
                    new(new Vector3(a.X,a.Y,a.Z), c), new(new Vector3(a.X,a2.Y,a.Z), c),
                    new(new Vector3(b.X,a.Y,a.Z), c), new(new Vector3(b.X,a2.Y,a.Z), c),
                    new(new Vector3(b.X,a.Y,b.Z), c), new(new Vector3(b.X,a2.Y,b.Z), c),
                    new(new Vector3(a.X,a.Y,b.Z), c), new(new Vector3(a.X,a2.Y,b.Z), c),
                };
                _markerVbo = new VertexBuffer(gd, typeof(VertexPositionColor), verts.Length, BufferUsage.WriteOnly);
                _markerVbo.SetData(verts);
            }

            float wx = uoX * 22f, wy = uoZ * 4f, wz = uoY * 22f;
            _markerEffect.World = Matrix.CreateTranslation(wx, wy, wz);
            _markerEffect.View = view;
            _markerEffect.Projection = projection;

            // Was DepthStencilState.None — that made the marker draw on top of
            // multis/statics even when the player stood behind a wall, leaving a
            // floating magenta box above rooftops. DepthRead lets walls occlude
            // the marker without polluting the depth buffer for downstream draws.
            var prevDepth = gd.DepthStencilState;
            gd.DepthStencilState = DepthStencilState.DepthRead;
            gd.SetVertexBuffer(_markerVbo);
            foreach (var pass in _markerEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawPrimitives(PrimitiveType.LineList, 0, _markerVbo.VertexCount / 2);
            }
            gd.DepthStencilState = prevDepth;
        }

        private static void EnsureEffect(GraphicsDevice gd)
        {
            if (_effect != null) return;
            string fxcPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "TextureSkinning.fxc");
            if (!File.Exists(fxcPath))
            {
                LastError = $"shader not found at {fxcPath}";
                Console.WriteLine($"[3DCUO] {LastError}");
                return;
            }
            _effect = TextureSkinnedEffect.LoadFromFile(gd, fxcPath);
            TextureSkinnedEffectAccessor.Effect = _effect;
        }

        private static SkinnedModelGlb EnsureStateModel(GraphicsDevice gd, AnimState state)
        {
            if (_models.TryGetValue(state, out var m) && m != null) return m;
            if (_failedLoads.Contains(state)) return null;

            // Resolve path: legacy ModelPath wins if set, otherwise StatePaths[state].
            string rel = !string.IsNullOrEmpty(ModelPath)
                ? ModelPath
                : (StatePaths.TryGetValue(state, out var p) ? p : null);
            if (string.IsNullOrEmpty(rel))
            {
                LastError = $"no path mapped for state {state}";
                _failedLoads.Add(state);
                return null;
            }

            string full = Path.Combine(AppContext.BaseDirectory, "Models", rel);
            if (!File.Exists(full))
            {
                LastError = $"glb not found at {full}";
                Console.WriteLine($"[3DCUO] {LastError}");
                _failedLoads.Add(state);
                return null;
            }

            try
            {
                var loaded = SkinnedModelGlb.Load(gd, full);
                int totalV = 0, totalI = 0;
                foreach (var s in loaded.Submeshes) { totalV += s.Vertices.VertexCount; totalI += s.IndexCount; }
                Console.WriteLine($"[3DCUO] Loaded [{state}] {full}: submeshes={loaded.Submeshes.Count} verts={totalV} idx={totalI} joints={loaded.JointNodeIndex.Length} anims={loaded.Animations.Length}");
                _models[state] = loaded;
                return loaded;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[3DCUO] state {state} load failed: {ex}");
                _failedLoads.Add(state);
                return null;
            }
        }

        private static SkinnedModelGlb EnsureSingleModel(GraphicsDevice gd)
        {
            if (_singleModel != null) return _singleModel;
            if (_singleLoadFailed) return null;

            string full = Path.Combine(AppContext.BaseDirectory, "Models", SingleGlbPath);
            if (!File.Exists(full))
            {
                LastError = $"single glb not found at {full}";
                Console.WriteLine($"[3DCUO] {LastError}");
                _singleLoadFailed = true;
                return null;
            }

            try
            {
                var loaded = SkinnedModelGlb.Load(gd, full);
                int totalV = 0, totalI = 0;
                foreach (var s in loaded.Submeshes) { totalV += s.Vertices.VertexCount; totalI += s.IndexCount; }
                Console.WriteLine($"[3DCUO] Loaded SINGLE {full}: submeshes={loaded.Submeshes.Count} verts={totalV} idx={totalI} joints={loaded.JointNodeIndex.Length} anims={loaded.Animations.Length}");
                for (int i = 0; i < loaded.Submeshes.Count; i++)
                {
                    var sm = loaded.Submeshes[i];
                    Console.WriteLine($"[3DCUO]   submesh[{i}] mesh='{sm.MeshName}' mat='{sm.MaterialName}' verts={sm.Vertices.VertexCount} idx={sm.IndexCount} tex={(sm.Texture != null ? "yes" : "no")}");
                }
                for (int i = 0; i < loaded.Animations.Length; i++)
                {
                    var a = loaded.Animations[i];
                    Console.WriteLine($"[3DCUO]   anim[{i}] name='{a.Name}' dur={a.Duration:F2}s channels={a.Channels.Count}");
                }
                _singleModel = loaded;
                return loaded;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[3DCUO] single glb load failed: {ex}");
                _singleLoadFailed = true;
                return null;
            }
        }

        private static int ResolveAnimIndex(SkinnedModelGlb m, AnimState s)
        {
            if (m == null || m.Animations == null || m.Animations.Length == 0) return -1;
            if (StateActionNames.TryGetValue(s, out var name))
            {
                int idx = m.FindAnimByName(name);
                if (idx >= 0) return idx;
            }
            // Fallback — first anim.
            return 0;
        }

        // ---- Motion detection ----

        private static void UpdateMotion(float ux, float uy, float uz)
        {
            // First call seeds the cache; don't trigger Run from the seed delta.
            if (_lastTickTicks == 0 && _lastMovedTime == 0)
            {
                _lastUoX = ux; _lastUoY = uy; _lastUoZ = uz;
                return;
            }
            float dx = ux - _lastUoX, dy = uy - _lastUoY, dz = uz - _lastUoZ;
            float distSq = dx*dx + dy*dy + dz*dz;
            LastFrameDelta = (float)Math.Sqrt(distSq);
            const float MOVE_EPSILON = 0.002f;
            if (distSq > MOVE_EPSILON * MOVE_EPSILON)
            {
                _lastMovedTime = _nowSec;
                TotalMotionFrames++;
            }
            _lastUoX = ux; _lastUoY = uy; _lastUoZ = uz;
        }

        private static bool IsMoving()
            => _lastMovedTime > 0f && (_nowSec - _lastMovedTime) < MotionStopTimeoutSec;
    }
}
