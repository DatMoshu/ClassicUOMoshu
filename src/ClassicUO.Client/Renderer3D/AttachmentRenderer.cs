// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — generic attachment renderer.
// Draws static glb meshes attached to named bones of the player skinned model so
// they follow animation. Replaces the older single-slot HatRenderer.
//
// Slots (fixed array — order matters for the gump dropdown):
//   0 Head      — Models/Hats/      (file pattern *AHED*.glb)  → bone "Head"
//   1 Back      — Models/Backpacks/ (file pattern *ABAC*.glb)  → bone "Spine2"
//   2 HipFront  — Models/Hips/      (file pattern *AHPF*.glb)  → bone "Hips"
//   3 HipBack   — Models/Hips/      (file pattern *AHPB*.glb)  → bone "Hips"
//   4 HipLeft   — Models/Hips/      (file pattern *AHPL*.glb)  → bone "LeftUpLeg"
//   5 HipRight  — Models/Hips/      (file pattern *AHPR*.glb)  → bone "RightUpLeg"
//
// World matrix per slot:
//     attachWorld = LocalOffset * BoneWorldFromPlayerRig * Player3DRenderer.LastWorldMatrix
//
// Glbs reuse SkinnedModelGlb.Load (works for non-skinned models). Drawing is via
// BasicEffect, no bone palette upload.

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Renderer.Mobiles; // MobileOutfit (migrated to Mobiles domain in ADR-012)
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal enum AttachSlotKind
    {
        // ── Bone-attached pieces (rigid: skinned to a Synty *Attach socket) ──
        Head,
        Face,
        Back,
        HipFront,
        HipBack,
        HipLeft,
        HipRight,
        ShoulderLeft,
        ShoulderRight,

        // ── Skinned body parts (deform with full body animation) ──
        Torso,
        HipsMesh,
        ArmUpperL,
        ArmUpperR,
        ArmLowerL,
        ArmLowerR,
        HandL,
        HandR,
        LegL,
        LegR,
        FootL,
        FootR,
        Hair,
        EarL,
        EarR,
        EyebrowL,
        EyebrowR,
        FaceCheek,
        Nose,
        Teeth,
    }

    internal sealed class AttachSlot
    {
        public readonly AttachSlotKind Kind;
        public string DisplayName;
        public string BoneName;            // substring match against rig node names
        public string Subfolder;           // under Models/, e.g. "Hats" or "Backpacks" or "Hips"
        public string FileNameContains;    // e.g. "AHED" or "AHPF" — empty = no filter

        public int CurrentIndex = -1;      // -1 = none
        public float Scale = 1.0f;
        public Vector3 LocalOffset = Vector3.Zero;
        public float YawDegrees;
        public float PitchDegrees;
        public float RollDegrees;

        public readonly List<string> Available = new();
        public string LastError;

        // Cache of loaded glbs keyed by Available index.
        public readonly Dictionary<int, SkinnedModelGlb> Cache = new();
        public readonly HashSet<int> FailedLoads = new();

        // Per-hat: hatJointIndex -> playerNodeIndex (-1 if no match by bone name).
        // Used to drive the hat's skinning palette from the player's animated rig
        // so the hat follows head/spine/hip animation. Built once on load.
        public readonly Dictionary<int, int[]> JointToPlayerNode = new();
        // Reusable palette buffer per loaded hat — sized to hat joint count.
        public readonly Dictionary<int, Matrix[]> Palette = new();

        public AttachSlot(AttachSlotKind kind, string display, string bone, string subfolder, string filenameContains)
        {
            Kind = kind;
            DisplayName = display;
            BoneName = bone;
            Subfolder = subfolder;
            FileNameContains = filenameContains;
        }

        public string CurrentPath
            => CurrentIndex >= 0 && CurrentIndex < Available.Count
                ? Available[CurrentIndex]
                : "(none)";
    }

    internal static class AttachmentRenderer
    {
        public static bool Enabled = true;

        // Debug toggle — when true, every attachment renders with an IDENTITY
        // bone palette (no animation, vertices stay at hat-bind-pose worn position).
        public static bool ForceIdentityPalette = false;

        // Debug toggle — paint every attachment bright red regardless of texture
        // or material. Definitive "is this drawing AT ALL" test: if you can't see
        // red anywhere on the player, draws aren't reaching the screen.
        public static bool ForceDebugColor = false;
        public static Vector3 DebugColor = new Vector3(1.0f, 0.05f, 0.05f);

        // Per-frame counters for the gump.
        public static int LastSlotsDrawn;
        public static int LastSubmeshesDrawn;
        public static int LastTrianglesDrawn;

        // Species used for base-mesh fallback when a slot has no equipment selected.
        // Matches the outfit folder name under prototypes/3DCUO/Models (HumanSpecies, ElvenSpecies, ...).
        // See BaseMeshRegistry + Sidekick's ActorCustomizationController for source.
        public static string CurrentSpecies = "HumanSpecies";

        // When true, each frame we look up the local player's equipped UO items
        // (via World.Player.FindItemByLayer) in EquipmentSidekickMap and force
        // every relevant AttachSlot.CurrentIndex to the matching glb. Turn off
        // for free-form debug picking via the gump dropdowns.
        public static bool AutoFromEquipment = true;

        // Last-applied snapshot so we only re-resolve when equipment changes —
        // FindItemByLayer + dictionary lookups are cheap, but skipping the
        // per-slot path index search every frame is cheaper still.
        private static readonly Dictionary<AttachSlotKind, ushort> _lastAppliedGraphic = new();
        // Slots written by the last auto-apply pass. We clear (-1) any of these
        // that drop out of the next pass so a previously-equipped item's mesh
        // doesn't ghost into the new frame.
        private static readonly HashSet<AttachSlotKind> _lastAppliedSlots = new();
        public static int LastAutoAppliedItems;     // # of UO items resolved this frame
        public static int LastAutoAppliedSlots;     // # of AttachSlot indices written
        public static string LastAutoApplyError;

        // Sentinel cache key for the per-slot base-mesh entry in JointToPlayerNode/Palette.
        private const int BASE_MESH_KEY = -100;

        // Periodic diagnostics — every N frames write to dumps/renderer.log.
        public static bool LogSkinDelta = true;
        public static int LogEveryNFrames = 60;
        private static int _logFrameCounter;
        private static int _cachedPlayerHeadJoint = -1;
        private static SkinnedModelGlb _cachedPlayerForHead;

        public static readonly AttachSlot[] Slots =
        {
            // Bone-attached pieces — skinned to one Synty *Attach socket bone, fall
            // back to identity for non-existent dynamic-cloth bones.
            new AttachSlot(AttachSlotKind.Head,          "Head",      "Head",          "Hats",      "AHED"),
            new AttachSlot(AttachSlotKind.Face,          "Face",      "Head",          "Faces",     "AFAC"),
            new AttachSlot(AttachSlotKind.Back,          "Back",      "Spine2",        "Backpacks", "ABAC"),
            new AttachSlot(AttachSlotKind.HipFront,      "Hip Front", "Hips",          "Hips",      "AHPF"),
            new AttachSlot(AttachSlotKind.HipBack,       "Hip Back",  "Hips",          "Hips",      "AHPB"),
            new AttachSlot(AttachSlotKind.HipLeft,       "Hip Left",  "LeftUpLeg",     "Hips",      "AHPL"),
            new AttachSlot(AttachSlotKind.HipRight,      "Hip Right", "RightUpLeg",    "Hips",      "AHPR"),
            new AttachSlot(AttachSlotKind.ShoulderLeft,  "Shldr L",   "LeftShoulder",  "Shoulders", "ASHL"),
            new AttachSlot(AttachSlotKind.ShoulderRight, "Shldr R",   "RightShoulder", "Shoulders", "ASHR"),

            // Skinned body parts — fully rigged to the Synty skeleton, deform with
            // every joint that influences them. The bone field is a documentation /
            // gump-readability anchor only; the entire palette is fed by the player's
            // SkinMatrices via the bone-name remap, identical to the rigid path.
            new AttachSlot(AttachSlotKind.Torso,     "Torso",     "Spine2",        "Torso",     ""),
            new AttachSlot(AttachSlotKind.HipsMesh,  "HipsMesh",  "Hips",          "HipsMesh",  ""),
            new AttachSlot(AttachSlotKind.ArmUpperL, "ArmUp L",   "LeftArm",       "ArmUpperL", ""),
            new AttachSlot(AttachSlotKind.ArmUpperR, "ArmUp R",   "RightArm",      "ArmUpperR", ""),
            new AttachSlot(AttachSlotKind.ArmLowerL, "ArmLo L",   "LeftForeArm",   "ArmLowerL", ""),
            new AttachSlot(AttachSlotKind.ArmLowerR, "ArmLo R",   "RightForeArm",  "ArmLowerR", ""),
            new AttachSlot(AttachSlotKind.HandL,     "Hand L",    "LeftHand",      "HandL",     ""),
            new AttachSlot(AttachSlotKind.HandR,     "Hand R",    "RightHand",     "HandR",     ""),
            new AttachSlot(AttachSlotKind.LegL,      "Leg L",     "LeftUpLeg",     "LegL",      ""),
            new AttachSlot(AttachSlotKind.LegR,      "Leg R",     "RightUpLeg",    "LegR",      ""),
            new AttachSlot(AttachSlotKind.FootL,     "Foot L",    "LeftFoot",      "FootL",     ""),
            new AttachSlot(AttachSlotKind.FootR,     "Foot R",    "RightFoot",     "FootR",     ""),
            new AttachSlot(AttachSlotKind.Hair,      "Hair",      "Head",          "Hair",      ""),
            new AttachSlot(AttachSlotKind.EarL,      "Ear L",     "Head",          "EarL",      ""),
            new AttachSlot(AttachSlotKind.EarR,      "Ear R",     "Head",          "EarR",      ""),
            new AttachSlot(AttachSlotKind.EyebrowL,  "Brow L",    "Head",          "EyebrowL",  ""),
            new AttachSlot(AttachSlotKind.EyebrowR,  "Brow R",    "Head",          "EyebrowR",  ""),
            new AttachSlot(AttachSlotKind.FaceCheek, "Cheek",     "Head",          "FaceCheek", ""),
            new AttachSlot(AttachSlotKind.Nose,      "Nose",      "Head",          "Nose",      ""),
            new AttachSlot(AttachSlotKind.Teeth,     "Teeth",     "Head",          "Teeth",     ""),
        };

        private static TextureSkinnedEffect _skinEffect;
        private static BasicEffect _staticEffect;
        private static bool _scanned;

        public static void RescanAll()
        {
            string root = Path.Combine(AppContext.BaseDirectory, "Models");
            foreach (var slot in Slots)
                RescanSlot(slot, root);
            _scanned = true;
        }

        private static void RescanSlot(AttachSlot slot, string modelsRoot)
        {
            slot.Available.Clear();
            string dir = Path.Combine(modelsRoot, slot.Subfolder);
            if (!Directory.Exists(dir))
            {
                slot.LastError = $"folder not found: {dir}";
                return;
            }
            foreach (var path in Directory.EnumerateFiles(dir, "*.glb", SearchOption.AllDirectories))
            {
                if (!string.IsNullOrEmpty(slot.FileNameContains) &&
                    Path.GetFileName(path).IndexOf(slot.FileNameContains, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                string rel = Path.GetRelativePath(modelsRoot, path).Replace('\\', '/');
                slot.Available.Add(rel);
            }
            slot.Available.Sort(StringComparer.OrdinalIgnoreCase);
            slot.LastError = null;
            Console.WriteLine($"[3DCUO] AttachmentRenderer slot {slot.DisplayName}: {slot.Available.Count} glbs");
        }

        // Public helper for MobileOutfitRegistry — exposes the private
        // FindSlot lookup so non-AttachmentRenderer code can enumerate a
        // slot's Available list without reflection.
        public static AttachSlot FindSlotPublic(AttachSlotKind kind) => FindSlot(kind);

        // Render the player's existing slot composition (auto-equipment +
        // user picks). Used for the local player's per-frame draw.
        public static void Draw(GraphicsDevice gd, Matrix view, Matrix projection)
        {
            if (!Enabled || gd == null) return;
            if (!_scanned) RescanAll();

            if (AutoFromEquipment) ApplyEquipmentToSlots();

            DrawCore(gd, view, projection);
        }

        // Render with a per-mobile outfit's slot indices temporarily overriding
        // the global Slots[].CurrentIndex. The auto-equipment path is suppressed
        // for the duration of this call; the outfit fully drives the composition.
        // Used by MobileRT3DRenderer when drawing non-player mobiles.
        public static void DrawWithOverrides(GraphicsDevice gd, Matrix view, Matrix projection, MobileOutfit outfit)
        {
            if (!Enabled || gd == null || outfit == null) return;
            if (!_scanned) RescanAll();

            // Snapshot every slot's CurrentIndex so we can restore exactly.
            var saved = new int[Slots.Length];
            for (int i = 0; i < Slots.Length; i++) saved[i] = Slots[i].CurrentIndex;

            // Default everything to base mesh; then layer the outfit picks.
            for (int i = 0; i < Slots.Length; i++) Slots[i].CurrentIndex = -1;
            foreach (var kv in outfit.SlotIndex)
            {
                // outfit.SlotIndex.Key is the new domain AttachSlot; cast to the legacy
                // AttachSlotKind (values bit-identical, parity test in MobileOutfit tests).
                var s = FindSlot((AttachSlotKind)(int)kv.Key);
                if (s != null && kv.Value >= 0 && kv.Value < s.Available.Count)
                    s.CurrentIndex = kv.Value;
            }

            try
            {
                DrawCore(gd, view, projection);
            }
            finally
            {
                for (int i = 0; i < Slots.Length; i++) Slots[i].CurrentIndex = saved[i];
            }
        }

        private static void DrawCore(GraphicsDevice gd, Matrix view, Matrix projection)
        {
            var player = Player3DRenderer.Model;
            if (player == null) return;

            // Periodic diagnostic write to renderer.log — captures the live
            // animation state so we have a record across runs without flooding the
            // console.
            if (LogSkinDelta && (++_logFrameCounter % LogEveryNFrames == 1))
            {
                if (!ReferenceEquals(_cachedPlayerForHead, player))
                {
                    _cachedPlayerForHead = player;
                    _cachedPlayerHeadJoint = -1;
                    int hn = player.FindNodeIndex("mixamorig:Head");
                    if (hn >= 0)
                        for (int j = 0; j < player.JointNodeIndex.Length; j++)
                            if (player.JointNodeIndex[j] == hn) { _cachedPlayerHeadJoint = j; break; }
                }
                if (_cachedPlayerHeadJoint >= 0 && _cachedPlayerHeadJoint < player.SkinMatrices.Length)
                {
                    var m = player.SkinMatrices[_cachedPlayerHeadJoint];
                    RendererLog.Write("attach",
                        $"head SkinMatrix.t=({m.M41:F3},{m.M42:F3},{m.M43:F3}) " +
                        $"diag=({m.M11:F3},{m.M22:F3},{m.M33:F3}) " +
                        $"state={Player3DRenderer.CurrentState} " +
                        $"identityPalette={ForceIdentityPalette}");
                }
            }

            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevBlend = gd.BlendState;
            gd.DepthStencilState = DepthStencilState.Default;
            gd.RasterizerState = Player3DRenderer.MobilesWireframe
                ? Player3DRenderer.GetMobileWireframeState()
                : (Player3DRenderer.CullMode switch
                {
                    1 => RasterizerState.CullClockwise,
                    2 => RasterizerState.CullNone,
                    _ => RasterizerState.CullCounterClockwise,
                });
            gd.BlendState = BlendState.Opaque;

            // Reset per-frame counters before slot loop.
            LastSlotsDrawn = 0;
            LastSubmeshesDrawn = 0;
            LastTrianglesDrawn = 0;

            foreach (var slot in Slots)
                DrawSlot(gd, slot, player, view, projection);

            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.BlendState = prevBlend;
        }

        // Layers we scan on the local player. Order matters only for diagnostics;
        // each item resolves to its own (disjoint) AttachSlot set so layer order
        // doesn't change the final composition.
        private static readonly ClassicUO.Game.Data.Layer[] _equipLayers =
        {
            ClassicUO.Game.Data.Layer.Helmet,
            ClassicUO.Game.Data.Layer.Cloak,
            ClassicUO.Game.Data.Layer.Robe,        // OuterTorso (server-side)
            ClassicUO.Game.Data.Layer.Tunic,       // MiddleTorso
            ClassicUO.Game.Data.Layer.Torso,       // InnerTorso
            ClassicUO.Game.Data.Layer.Arms,
            ClassicUO.Game.Data.Layer.Gloves,
            ClassicUO.Game.Data.Layer.Pants,
            ClassicUO.Game.Data.Layer.Skirt,       // OuterLegs
            ClassicUO.Game.Data.Layer.Shoes,
            ClassicUO.Game.Data.Layer.Waist,
            ClassicUO.Game.Data.Layer.Necklace,    // gorget
            ClassicUO.Game.Data.Layer.Shirt,
        };

        // Walk the player's equipment, look up Sidekick parts in EquipmentSidekickMap,
        // and force each AttachSlot.CurrentIndex to point at the corresponding glb.
        // Slots that previously held an auto-applied mesh but aren't covered this
        // frame fall back to -1 (base mesh). User-driven slots (when the toggle is
        // off) are untouched.
        private static void ApplyEquipmentToSlots()
        {
            var player = ClassicUO.Client.Game.UO.World.Player;
            if (player == null)
            {
                LastAutoApplyError = "no player";
                return;
            }
            EquipmentSidekickMap.EnsureLoaded();

            var coveredThisFrame = new HashSet<AttachSlotKind>();
            int itemsResolved = 0, slotsWritten = 0;

            foreach (var layer in _equipLayers)
            {
                var item = player.FindItemByLayer(layer);
                if (item == null) continue;
                if (!EquipmentSidekickMap.TryGetParts(item.Graphic, out var parts)) continue;
                itemsResolved++;

                foreach (var part in parts)
                {
                    var slot = FindSlot(part.Slot);
                    if (slot == null) continue;

                    // Per-slot Subfolder is fixed; sanity-check that the json's
                    // intended subfolder still matches the slot's scanner before
                    // searching its Available list. If they ever diverge (renamed
                    // a slot's source folder, etc.) we'd silently fail otherwise.
                    if (!string.Equals(slot.Subfolder, part.Subfolder, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // RescanSlot stores paths relative to Models/ (e.g.
                    // "Hats/FantasyKnights/...glb"); JSON's rel is relative to
                    // the slot subfolder (e.g. "FantasyKnights/...glb"). Try
                    // both shapes so future format drift on either side doesn't
                    // silently break the lookup.
                    string fullRel = part.Subfolder + "/" + part.Rel;
                    int idx = slot.Available.IndexOf(fullRel);
                    if (idx < 0) idx = slot.Available.IndexOf(part.Rel);
                    if (idx < 0)
                    {
                        // Equipment maps to a glb that isn't in the slot's scanned
                        // list — usually means the file was added after startup or
                        // the FileNameContains filter excluded it. Skip; the user
                        // can hit "Reload models" to rescan.
                        continue;
                    }

                    if (slot.CurrentIndex != idx)
                        slot.CurrentIndex = idx;
                    coveredThisFrame.Add(part.Slot);
                    _lastAppliedGraphic[part.Slot] = item.Graphic;
                    slotsWritten++;
                }
            }

            // Clear any slot we wrote on a previous auto pass that isn't covered now.
            // This drops a helmet back to the base head mesh when the player un-equips it.
            foreach (var prev in _lastAppliedSlots)
            {
                if (coveredThisFrame.Contains(prev)) continue;
                var slot = FindSlot(prev);
                if (slot != null && slot.CurrentIndex >= 0)
                    slot.CurrentIndex = -1;
                _lastAppliedGraphic.Remove(prev);
            }
            _lastAppliedSlots.Clear();
            foreach (var k in coveredThisFrame) _lastAppliedSlots.Add(k);

            LastAutoAppliedItems = itemsResolved;
            LastAutoAppliedSlots = slotsWritten;
            LastAutoApplyError = null;
        }

        private static AttachSlot FindSlot(AttachSlotKind kind)
        {
            foreach (var s in Slots) if (s.Kind == kind) return s;
            return null;
        }

        private static void DrawSlot(GraphicsDevice gd, AttachSlot slot, SkinnedModelGlb player, Matrix view, Matrix projection)
        {
            // Hide the Hair attachment glb when a hat/helmet is loaded — mirrors
            // the body-submesh hair/beard suppression in Player3DRenderer.
            if (slot.Kind == AttachSlotKind.Hair &&
                Player3DRenderer.AutoHideHairBeardWhenHat &&
                Player3DRenderer.IsHatEquipped)
                return;

            // Pick equipment if selected; otherwise fall back to the species base mesh.
            // Sidekick's ActorCustomizationController applies the same rule: equipped
            // outfit part wins, base body mesh fills any unequipped slot.
            SkinnedModelGlb model;
            int cacheIdx;
            if (slot.CurrentIndex >= 0 && slot.CurrentIndex < slot.Available.Count)
            {
                model = EnsureModel(gd, slot, slot.CurrentIndex);
                cacheIdx = slot.CurrentIndex;
            }
            else
            {
                model = BaseMeshRegistry.EnsureModel(gd, CurrentSpecies, slot.Subfolder, out var berr);
                if (model == null) { slot.LastError = berr; return; }
                cacheIdx = BASE_MESH_KEY;
            }
            if (model == null) return;

            int playerBoneIdx = player.FindNodeIndex(slot.BoneName);
            if (playerBoneIdx < 0)
            {
                slot.LastError = $"bone '{slot.BoneName}' not found";
                return;
            }

            // Per-slot tuning composed onto the player's world transform. With proper
            // skinning the offset/yaw/pitch tuning sliders just trim the final placement
            // (most glbs need none, but admins may want to nudge a hat sideways).
            var local =
                Matrix.CreateScale(slot.Scale) *
                Matrix.CreateRotationX(MathHelper.ToRadians(slot.PitchDegrees)) *
                Matrix.CreateRotationY(MathHelper.ToRadians(slot.YawDegrees)) *
                Matrix.CreateRotationZ(MathHelper.ToRadians(slot.RollDegrees)) *
                Matrix.CreateTranslation(slot.LocalOffset);

            // Synty hats are SKINNED to a Mixamo-style rig. Vertices live in the WORN
            // position (e.g. hat geometry centered on head height in glb space) with
            // skin weights pointing to bones. If we render them as static meshes and
            // apply boneWorld * playerWorld, the head-height vertices end up doubled
            // (drawn at head_pos + head_height_offset), floating off-screen.
            //
            // Correct approach: render with skinning. Build the hat's per-frame palette
            // using the PLAYER's animated world matrices, indexed by bone name match,
            // composed with the HAT's own inverse-bind matrices. The result follows
            // the player's animation correctly.
            if (model.JointNodeIndex == null || model.JointNodeIndex.Length == 0)
            {
                // Unskinned fallback — just place at the bone, scaled by tuning. Most
                // Synty pieces have a skin so this branch is rare.
                DrawAsStatic(gd, model, slot, player.WorldMatrices[playerBoneIdx], local, view, projection);
                return;
            }

            int[] remap = slot.JointToPlayerNode.GetValueOrDefault(cacheIdx);
            if (remap == null)
            {
                remap = BuildJointRemap(model, player);
                slot.JointToPlayerNode[cacheIdx] = remap;
            }

            int n = model.JointNodeIndex.Length;
            if (!slot.Palette.TryGetValue(cacheIdx, out var palette) || palette.Length != n)
            {
                palette = new Matrix[n];
                slot.Palette[cacheIdx] = palette;
            }

            // Make sure the hat's own world matrices are in bind pose (the static
            // attachment fallback path uses them too).
            if (model.WorldMatrices == null || model.WorldMatrices.Length == 0)
                model.ResetToBindPose();

            // palette[i] = player.SkinMatrices[remap[i]] applies the player joint's
            // animation delta (inverse-bind * animated-world) to vertices weighted
            // to hat joint i. This is the correct retarget formula when the hat
            // and player rigs share a roughly-equivalent bind pose at the matched
            // joints. UE and Mixamo bind poses differ slightly (a few cm of joint
            // offset / a few degrees of orientation) so static placement will be
            // imperfect, but the rig will ANIMATE correctly with the player.
            //
            // ForceIdentityPalette short-circuits skinning — every entry becomes
            // identity so vertices stay at their authored hat-bind-pose position.
            // Used to verify static placement independently of animation.
            for (int i = 0; i < n; i++)
            {
                if (ForceIdentityPalette) { palette[i] = Matrix.Identity; continue; }
                int pj = remap[i];
                if (pj >= 0 && pj < player.SkinMatrices.Length)
                    palette[i] = player.SkinMatrices[pj];
                else
                    palette[i] = Matrix.Identity;  // unmatched joints leave verts at bind pose
            }

            EnsureSkinEffect(gd);
            _skinEffect.World = local * Player3DRenderer.LastWorldMatrix;
            _skinEffect.View = view;
            _skinEffect.Projection = projection;
            _skinEffect.SetBoneTransforms(palette);

            int submeshesDrawn = 0;
            int trisDrawn = 0;
            foreach (var sub in model.Submeshes)
            {
                if (!sub.Visible) continue;

                if (ForceDebugColor)
                {
                    _skinEffect.DiffuseTexture = null;  // force fallback
                    _skinEffect.FallbackColor = DebugColor;
                    TextureSkinnedEffectAccessor.LastAttachDiffuseTex = null;
                    TextureSkinnedEffectAccessor.LastAttachDiffuseW = 0;
                    TextureSkinnedEffectAccessor.LastAttachDiffuseH = 0;
                }
                else if (MaterialDebug.RandomPerSubmesh)
                {
                    // Per-submesh deterministic color via FallbackColor only.
                    // HasDiffuse=0 forces the shader's else branch (no texture).
                    _skinEffect.DiffuseTexture = null;
                    _skinEffect.FallbackColor = MaterialDebug.ColorFromName(slot.DisplayName + "/" + sub.MeshName);
                    _skinEffect.SetHasDiffuseForce(0f);
                }
                else
                {
                    var chosen = MaterialDebug.ChooseTexture(gd, sub.Texture);
                    _skinEffect.DiffuseTexture = chosen;
                    _skinEffect.FallbackColor = new Vector3(sub.BaseColorFactor.X, sub.BaseColorFactor.Y, sub.BaseColorFactor.Z) * MaterialDebug.FactorBoost;
                    TextureSkinnedEffectAccessor.LastAttachDiffuseTex = chosen;
                    TextureSkinnedEffectAccessor.LastAttachDiffuseW = chosen?.Width ?? 0;
                    TextureSkinnedEffectAccessor.LastAttachDiffuseH = chosen?.Height ?? 0;
                }
                if (MaterialDebug.ForceHasDiffuse >= 0 && !MaterialDebug.RandomPerSubmesh)
                    _skinEffect.SetHasDiffuseForce(MaterialDebug.ForceHasDiffuse);

                gd.SetVertexBuffer(sub.Vertices);
                gd.Indices = sub.Indices;
                foreach (var pass in _skinEffect.CurrentTechnique.Passes)
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
                submeshesDrawn++;
                trisDrawn += sub.IndexCount / 3;
            }
            LastSubmeshesDrawn += submeshesDrawn;
            LastTrianglesDrawn += trisDrawn;
            if (submeshesDrawn > 0) LastSlotsDrawn++;
        }

        // Synty Sidekick FBXes use the Unreal Mannequin skeleton (lowercase, no
        // prefix), whereas the player's Beta rig is Mixamo (mixamorig: prefix,
        // PascalCase). The two name sets share zero exact tokens — without this
        // table only "head" matches, every other joint falls back to bind pose
        // and the attachment freezes through animation.
        // Map covers the ~52 Mixamo standard joints. Synty's twist / IK / dyn /
        // attach helper bones stay unmapped (no Mixamo analogue) and contribute
        // bind-pose-only weight, which is the correct fallback.
        private static readonly Dictionary<string, string> _ueToMixamo = new(StringComparer.OrdinalIgnoreCase)
        {
            // Synty-specific dedicated attachment-point bones. Every Sidekick
            // attachment piece (hat, backpack, hip pad, shoulder pad, face mask)
            // is skinned with weight=1.0 to ONE of these bones. They MUST map
            // back to a player rig joint or the entire piece falls back to
            // bind-pose identity and stops following animation.
            { "headAttach",       "mixamorig:Head" },
            { "faceAttach",       "mixamorig:Head" },
            { "backAttach",       "mixamorig:Spine2" },
            { "hipAttachFront",   "mixamorig:Hips" },
            { "hipAttachBack",    "mixamorig:Hips" },
            { "hipAttach_l",      "mixamorig:LeftUpLeg" },
            { "hipAttach_r",      "mixamorig:RightUpLeg" },
            { "shoulderAttach_l", "mixamorig:LeftShoulder" },
            { "shoulderAttach_r", "mixamorig:RightShoulder" },
            { "elbowAttach_l",    "mixamorig:LeftForeArm" },
            { "elbowAttach_r",    "mixamorig:RightForeArm" },
            { "kneeAttach_l",     "mixamorig:LeftLeg" },
            { "kneeAttach_r",     "mixamorig:RightLeg" },
            { "prop_l",           "mixamorig:LeftHand" },
            { "prop_r",           "mixamorig:RightHand" },

            { "pelvis",      "mixamorig:Hips" },
            { "root",        "mixamorig:Hips" },
            { "spine_01",    "mixamorig:Spine" },
            { "spine_02",    "mixamorig:Spine1" },
            { "spine_03",    "mixamorig:Spine2" },
            { "neck_01",     "mixamorig:Neck" },
            { "head",        "mixamorig:Head" },

            { "clavicle_l",  "mixamorig:LeftShoulder" },
            { "clavicle_r",  "mixamorig:RightShoulder" },
            { "upperarm_l",  "mixamorig:LeftArm" },
            { "upperarm_r",  "mixamorig:RightArm" },
            { "upperarm_twist_01_l", "mixamorig:LeftArm" },
            { "upperarm_twist_01_r", "mixamorig:RightArm" },
            { "lowerarm_l",  "mixamorig:LeftForeArm" },
            { "lowerarm_r",  "mixamorig:RightForeArm" },
            { "lowerarm_twist_01_l", "mixamorig:LeftForeArm" },
            { "lowerarm_twist_01_r", "mixamorig:RightForeArm" },
            { "hand_l",      "mixamorig:LeftHand" },
            { "hand_r",      "mixamorig:RightHand" },

            { "thigh_l",     "mixamorig:LeftUpLeg" },
            { "thigh_r",     "mixamorig:RightUpLeg" },
            { "thigh_twist_01_l", "mixamorig:LeftUpLeg" },
            { "thigh_twist_01_r", "mixamorig:RightUpLeg" },
            { "calf_l",      "mixamorig:LeftLeg" },
            { "calf_r",      "mixamorig:RightLeg" },
            { "calf_twist_01_l", "mixamorig:LeftLeg" },
            { "calf_twist_01_r", "mixamorig:RightLeg" },
            { "foot_l",      "mixamorig:LeftFoot" },
            { "foot_r",      "mixamorig:RightFoot" },
            { "ball_l",      "mixamorig:LeftToeBase" },
            { "ball_r",      "mixamorig:RightToeBase" },

            // Fingers — UE numbers segments _01.._03; Mixamo numbers them 1..3.
            { "thumb_01_l",  "mixamorig:LeftHandThumb1" },
            { "thumb_02_l",  "mixamorig:LeftHandThumb2" },
            { "thumb_03_l",  "mixamorig:LeftHandThumb3" },
            { "thumb_01_r",  "mixamorig:RightHandThumb1" },
            { "thumb_02_r",  "mixamorig:RightHandThumb2" },
            { "thumb_03_r",  "mixamorig:RightHandThumb3" },
            { "index_01_l",  "mixamorig:LeftHandIndex1" },
            { "index_02_l",  "mixamorig:LeftHandIndex2" },
            { "index_03_l",  "mixamorig:LeftHandIndex3" },
            { "index_01_r",  "mixamorig:RightHandIndex1" },
            { "index_02_r",  "mixamorig:RightHandIndex2" },
            { "index_03_r",  "mixamorig:RightHandIndex3" },
            { "middle_01_l", "mixamorig:LeftHandMiddle1" },
            { "middle_02_l", "mixamorig:LeftHandMiddle2" },
            { "middle_03_l", "mixamorig:LeftHandMiddle3" },
            { "middle_01_r", "mixamorig:RightHandMiddle1" },
            { "middle_02_r", "mixamorig:RightHandMiddle2" },
            { "middle_03_r", "mixamorig:RightHandMiddle3" },
            { "ring_01_l",   "mixamorig:LeftHandRing1" },
            { "ring_02_l",   "mixamorig:LeftHandRing2" },
            { "ring_03_l",   "mixamorig:LeftHandRing3" },
            { "ring_01_r",   "mixamorig:RightHandRing1" },
            { "ring_02_r",   "mixamorig:RightHandRing2" },
            { "ring_03_r",   "mixamorig:RightHandRing3" },
            { "pinky_01_l",  "mixamorig:LeftHandPinky1" },
            { "pinky_02_l",  "mixamorig:LeftHandPinky2" },
            { "pinky_03_l",  "mixamorig:LeftHandPinky3" },
            { "pinky_01_r",  "mixamorig:RightHandPinky1" },
            { "pinky_02_r",  "mixamorig:RightHandPinky2" },
            { "pinky_03_r",  "mixamorig:RightHandPinky3" },
        };

        // Maps hat-joint-index -> player-JOINT-index (not node index). -1 means no
        // match; that joint's palette entry stays at identity so vertices weighted
        // to it just sit at their bind-pose position. We use player joints (not
        // nodes) because the rendered palette comes from player.SkinMatrices,
        // which is sized by joint count.
        private static int[] BuildJointRemap(SkinnedModelGlb hat, SkinnedModelGlb player)
        {
            // Build a player-node-index -> player-joint-index lookup once per call.
            var nodeToJoint = new Dictionary<int, int>(player.JointNodeIndex.Length);
            for (int j = 0; j < player.JointNodeIndex.Length; j++)
                nodeToJoint[player.JointNodeIndex[j]] = j;

            int[] map = new int[hat.JointNodeIndex.Length];
            int matched = 0;
            for (int i = 0; i < map.Length; i++)
            {
                int hatNodeIdx = hat.JointNodeIndex[i];
                string name = hat.AllNodes[hatNodeIdx].Name ?? string.Empty;

                int playerNode = -1;
                if (_ueToMixamo.TryGetValue(name, out var mixamoName))
                    playerNode = player.FindNodeIndex(mixamoName);
                if (playerNode < 0)
                    playerNode = player.FindNodeIndex(name);

                // Prefix-pattern fallback for Synty dynamic-cloth bones:
                //   a<slot>_dyn_*   — physics-simulated cloth/ribbon bones, animate
                //   in Unity via Cloth component but in our static rendering they
                //   have no animation. Route them to the corresponding anchor body
                //   bone so the cloth at least follows the parent body part.
                //
                // Without this, hip pieces and ribbon-y hat bits fall back to
                // identity (vertices stuck at glb-bind-pose worn position) and
                // appear to "not follow bones" while the rest of the player moves.
                if (playerNode < 0 && name.Length > 5)
                {
                    string mixamoEquiv = null;
                    if      (name.StartsWith("ahed_dyn", StringComparison.OrdinalIgnoreCase)) mixamoEquiv = "mixamorig:Head";
                    else if (name.StartsWith("afac_dyn", StringComparison.OrdinalIgnoreCase)) mixamoEquiv = "mixamorig:Head";
                    else if (name.StartsWith("abac_dyn", StringComparison.OrdinalIgnoreCase)) mixamoEquiv = "mixamorig:Spine2";
                    else if (name.StartsWith("ahpf_dyn", StringComparison.OrdinalIgnoreCase)) mixamoEquiv = "mixamorig:Hips";
                    else if (name.StartsWith("ahpb_dyn", StringComparison.OrdinalIgnoreCase)) mixamoEquiv = "mixamorig:Hips";
                    else if (name.StartsWith("ahpl_dyn", StringComparison.OrdinalIgnoreCase)) mixamoEquiv = "mixamorig:LeftUpLeg";
                    else if (name.StartsWith("ahpr_dyn", StringComparison.OrdinalIgnoreCase)) mixamoEquiv = "mixamorig:RightUpLeg";
                    else if (name.StartsWith("ashl_dyn", StringComparison.OrdinalIgnoreCase)) mixamoEquiv = "mixamorig:LeftShoulder";
                    else if (name.StartsWith("ashr_dyn", StringComparison.OrdinalIgnoreCase)) mixamoEquiv = "mixamorig:RightShoulder";
                    if (mixamoEquiv != null) playerNode = player.FindNodeIndex(mixamoEquiv);
                }

                map[i] = playerNode >= 0 && nodeToJoint.TryGetValue(playerNode, out var jointIdx)
                    ? jointIdx
                    : -1;
                if (map[i] >= 0) matched++;
            }
            Console.WriteLine($"[3DCUO] AttachmentRenderer joint remap: {matched}/{map.Length} matched");
            return map;
        }

        private static void DrawAsStatic(GraphicsDevice gd, SkinnedModelGlb model, AttachSlot slot,
            Matrix boneWorld, Matrix local, Matrix view, Matrix projection)
        {
            EnsureStaticEffect(gd);
            var attachWorld = local * boneWorld * Player3DRenderer.LastWorldMatrix;
            _staticEffect.World = attachWorld;
            _staticEffect.View = view;
            _staticEffect.Projection = projection;

            foreach (var sub in model.Submeshes)
            {
                if (!sub.Visible) continue;
                _staticEffect.Texture = sub.Texture;
                _staticEffect.TextureEnabled = sub.Texture != null;
                _staticEffect.DiffuseColor = new Vector3(sub.BaseColorFactor.X, sub.BaseColorFactor.Y, sub.BaseColorFactor.Z);
                _staticEffect.Alpha = sub.BaseColorFactor.W;

                gd.SetVertexBuffer(sub.Vertices);
                gd.Indices = sub.Indices;
                foreach (var pass in _staticEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
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
        }

        private static SkinnedModelGlb EnsureModel(GraphicsDevice gd, AttachSlot slot, int idx)
        {
            if (slot.Cache.TryGetValue(idx, out var m) && m != null) return m;
            if (slot.FailedLoads.Contains(idx)) return null;

            string rel = slot.Available[idx];
            string full = Path.Combine(AppContext.BaseDirectory, "Models", rel);
            if (!File.Exists(full))
            {
                slot.LastError = $"glb not found: {full}";
                slot.FailedLoads.Add(idx);
                return null;
            }
            try
            {
                var loaded = SkinnedModelGlb.Load(gd, full);
                loaded.ResetToBindPose();
                slot.Cache[idx] = loaded;
                Console.WriteLine($"[3DCUO] {slot.DisplayName} loaded [{idx}] {rel}: submeshes={loaded.Submeshes.Count}");
                return loaded;
            }
            catch (Exception ex)
            {
                slot.LastError = ex.Message;
                Console.WriteLine($"[3DCUO] {slot.DisplayName} load failed for {rel}: {ex}");
                slot.FailedLoads.Add(idx);
                return null;
            }
        }

        private static void EnsureSkinEffect(GraphicsDevice gd)
        {
            if (_skinEffect != null) return;
            string fxcPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "TextureSkinning.fxc");
            if (!File.Exists(fxcPath))
            {
                Console.WriteLine($"[3DCUO] AttachmentRenderer: skin shader not found at {fxcPath}");
                return;
            }
            _skinEffect = TextureSkinnedEffect.LoadFromFile(gd, fxcPath);
            TextureSkinnedEffectAccessor.AttachEffect = _skinEffect;
        }

        private static void EnsureStaticEffect(GraphicsDevice gd)
        {
            if (_staticEffect != null) return;
            _staticEffect = new BasicEffect(gd)
            {
                LightingEnabled = false,
                VertexColorEnabled = false,
                TextureEnabled = true,
            };
        }

        public static void Invalidate()
        {
            foreach (var slot in Slots)
            {
                foreach (var m in slot.Cache.Values) m?.Dispose();
                slot.Cache.Clear();
                slot.FailedLoads.Clear();
                slot.JointToPlayerNode.Clear();
                slot.Palette.Clear();
            }
            BaseMeshRegistry.Invalidate();
            _scanned = false;
        }

        // Switch the active species used for base-mesh fallback. Drops cached
        // base-mesh joint remaps/palettes so the next frame rebuilds against
        // the new model. Equipment caches are untouched.
        public static void SetCurrentSpecies(string species)
        {
            if (string.IsNullOrEmpty(species) || species == CurrentSpecies) return;
            CurrentSpecies = species;
            foreach (var slot in Slots)
            {
                slot.JointToPlayerNode.Remove(BASE_MESH_KEY);
                slot.Palette.Remove(BASE_MESH_KEY);
            }
            BaseMeshRegistry.Invalidate();
        }
    }
}
