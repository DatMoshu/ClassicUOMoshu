// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — Phase 3: skinned glTF model loader + animation runtime.
// Uses SharpGLTF.Core to parse a .glb. Hand-rolled hierarchy + LERP/SLERP
// animation evaluation. Outputs a Matrix[] of skin transforms suitable for
// FNA's SkinnedEffect (which expects worldTransform * inverseBindMatrix per
// joint, then multiplied by Mesh.World on the GPU).
//
// Vertex format: VertexSkinned (position, normal, texcoord, blendIndices,
// blendWeights) — 52 bytes per vert.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using SharpGLTF.Schema2;

namespace ClassicUO.Renderer.Renderer3D
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct VertexSkinned : IVertexType
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord;
        public Byte4 BlendIndices;
        public Vector4 BlendWeights;

        public static readonly VertexDeclaration Decl = new VertexDeclaration(
            new VertexElement(0,  VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(32, VertexElementFormat.Byte4,  VertexElementUsage.BlendIndices, 0),
            new VertexElement(36, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0)
        );

        public VertexDeclaration VertexDeclaration => Decl;
    }

    /// <summary>One drawable primitive: own VBO/IBO + own material.</summary>
    internal sealed class Submesh : IDisposable
    {
        public string MeshName;          // glTF mesh name (e.g. "Beta_Surface")
        public string MaterialName;
        public VertexBuffer Vertices;
        public IndexBuffer Indices;
        public int IndexCount;
        public Texture2D Texture;        // null if no texture
        public Vector4 BaseColorFactor = Vector4.One;
        public Vector4 OriginalBaseColorFactor = Vector4.One;  // glTF-authored value; used by "Reset" in PlayerMaterialGump
        public bool Visible = true;      // runtime toggle (e.g., hide joint-balls mesh)

        public void Dispose()
        {
            Vertices?.Dispose();
            Indices?.Dispose();
            Texture?.Dispose();
        }
    }

    internal sealed class SkinnedModelGlb : IDisposable
    {
        public List<Submesh> Submeshes = new();
        public BoundingBox Bounds;

        // Skeleton:
        //   AllNodes[] are every node in the file (named hierarchy).
        //   JointNodeIndex[i] tells which node is joint i for the skin.
        //   InverseBindMatrix[i] is the inverse-bind for joint i.
        public Node[] AllNodes;
        public int[] JointNodeIndex;
        public Matrix[] InverseBindMatrices;

        public Anim[] Animations;
        public int ActiveAnim = 0;
        public float AnimTime;

        // Cross-fade target (set by Player3DRenderer when state changes).
        // When BlendWeight > 0, Update() blends from ActiveAnim toward TargetAnim
        // using per-node TRS lerp. Caller is responsible for ramping BlendWeight
        // from 0->1 over the desired blend duration, then assigning TargetAnim
        // to ActiveAnim and resetting BlendWeight=0.
        public int TargetAnim = -1;
        public float TargetAnimTime;
        public float BlendWeight; // 0 = ActiveAnim only, 1 = TargetAnim only

        // Diagnostic sink for texture-decode tracing. Writes to stdout AND to
        // a sidecar log file next to the exe so we can read it after the run
        // even when console isn't captured. PROTOTYPE-ONLY.
        private static readonly object _texLogLock = new();
        private static string _texLogPath;
        internal static void TexDebugLog(string line)
        {
            Console.WriteLine($"[3DCUO][tex] {line}");
            lock (_texLogLock)
            {
                if (_texLogPath == null)
                {
                    try
                    {
                        var dir = Path.Combine(AppContext.BaseDirectory, "Logs");
                        Directory.CreateDirectory(dir);
                        _texLogPath = Path.Combine(dir, "texture-decode.log");
                        File.WriteAllText(_texLogPath, $"# texture decode trace started {DateTime.Now:O}\n");
                    }
                    catch { _texLogPath = ""; }
                }
                if (!string.IsNullOrEmpty(_texLogPath))
                {
                    try { File.AppendAllText(_texLogPath, line + "\n"); } catch { }
                }
            }
        }

        public int FindAnimByName(string name)
        {
            if (Animations == null || string.IsNullOrEmpty(name)) return -1;
            // Exact match first.
            for (int i = 0; i < Animations.Length; i++)
                if (string.Equals(Animations[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            // Substring fallback so callers can ask for "Idle" and match
            // exporter-decorated names like "Idle_Armature" or "Idle_001".
            for (int i = 0; i < Animations.Length; i++)
                if (Animations[i].Name != null &&
                    Animations[i].Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            return -1;
        }

        // Per-frame mutable state (allocated once at load — NEVER on the hot path).
        private Matrix[] _localMatrices;
        private Matrix[] _worldMatrices;
        public Matrix[] SkinMatrices;

        // Animation-eval scratch buffers (sized once at load to AllNodes.Length).
        // Hoisted out of EvaluateAnimation/EvaluateAnimationBlended which used
        // to `new` 6 / 12 arrays per frame per model. At ~50 nodes that was
        // ~22 KB/frame of garbage per player; multi-mobile multiplies it.
        private Vector3[] _evalT_a;
        private Quaternion[] _evalR_a;
        private Vector3[] _evalS_a;
        private bool[] _evalHasT_a;
        private bool[] _evalHasR_a;
        private bool[] _evalHasS_a;
        private Vector3[] _evalT_b;
        private Quaternion[] _evalR_b;
        private Vector3[] _evalS_b;
        private bool[] _evalHasT_b;
        private bool[] _evalHasR_b;
        private bool[] _evalHasS_b;

        // Pre-computed parent-before-child traversal order (built at load by
        // ComputeTopologicalOrder). RecomputeWorldAndSkin walks this once
        // instead of running an 8-pass safety loop with a `new bool[]` per call.
        private int[] _topoOrder;

        // Read-only access to the animated world matrix of every node, in node-array order.
        // Used by HatRenderer to attach the hat to the head bone.
        public Matrix[] WorldMatrices => _worldMatrices;

        public int FindNodeIndex(string name)
        {
            if (AllNodes == null || string.IsNullOrEmpty(name)) return -1;
            // Exact match first.
            for (int i = 0; i < AllNodes.Length; i++)
                if (string.Equals(AllNodes[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            // Substring fallback (e.g. "Head" matches "mixamorig:Head").
            for (int i = 0; i < AllNodes.Length; i++)
                if (AllNodes[i].Name != null && AllNodes[i].Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            return -1;
        }

        public struct Node
        {
            public string Name;
            public int Parent;          // -1 for root
            public Vector3 T;           // bind pose translation
            public Quaternion R;        // bind pose rotation
            public Vector3 S;           // bind pose scale
        }

        public sealed class Anim
        {
            public string Name;
            public float Duration;
            public List<Channel> Channels = new();
        }

        public struct Channel
        {
            public int TargetNode;
            public ChannelKind Kind;
            public float[] Times;
            public float[] Values; // 3 floats for T/S, 4 for R
        }

        public enum ChannelKind { Translation, Rotation, Scale }

        public static SkinnedModelGlb Load(GraphicsDevice gd, string path)
        {
            // SharpGLTF validates strictly by default and rejects FBX2glTF output
            // that uses sparse accessors for morph targets (Synty AHEDBlends) or
            // non-normalized TANGENT vectors (Synty backpacks/shoulders). We
            // don't read morph targets or tangents on static attachments anyway,
            // so skip validation entirely — we'd rather best-effort load than
            // refuse the whole asset over a non-fatal spec edge case.
            var settings = new SharpGLTF.Schema2.ReadSettings
            {
                Validation = SharpGLTF.Validation.ValidationMode.Skip,
            };
            var root = ModelRoot.Load(path, settings);
            var inst = new SkinnedModelGlb();

            // ---- Nodes ----
            var nodes = root.LogicalNodes.ToArray();
            var nodeIndexById = new Dictionary<Node_t, int>(); // SharpGLTF.Node -> our index
            inst.AllNodes = new SkinnedModelGlb.Node[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                nodeIndexById[new Node_t(n)] = i;
                var lt = n.LocalTransform;
                inst.AllNodes[i] = new SkinnedModelGlb.Node
                {
                    Name = n.Name ?? $"node_{i}",
                    T = ToXna(lt.Translation),
                    R = ToXna(lt.Rotation),
                    S = ToXna(lt.Scale),
                    Parent = -1,
                };
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                var parent = nodes[i].VisualParent;
                if (parent != null && nodeIndexById.TryGetValue(new Node_t(parent), out var pi))
                    inst.AllNodes[i].Parent = pi;
            }

            inst._localMatrices = new Matrix[nodes.Length];
            inst._worldMatrices = new Matrix[nodes.Length];

            // Allocate animation-eval scratch buffers once. Sized to node count.
            inst._evalT_a = new Vector3[nodes.Length];
            inst._evalR_a = new Quaternion[nodes.Length];
            inst._evalS_a = new Vector3[nodes.Length];
            inst._evalHasT_a = new bool[nodes.Length];
            inst._evalHasR_a = new bool[nodes.Length];
            inst._evalHasS_a = new bool[nodes.Length];
            inst._evalT_b = new Vector3[nodes.Length];
            inst._evalR_b = new Quaternion[nodes.Length];
            inst._evalS_b = new Vector3[nodes.Length];
            inst._evalHasT_b = new bool[nodes.Length];
            inst._evalHasR_b = new bool[nodes.Length];
            inst._evalHasS_b = new bool[nodes.Length];

            // Pre-compute parent-before-child topo order once. glTF nodes
            // are not guaranteed parent-first in array order, so the old code
            // ran an O(N) outer pass up to 8 times per frame allocating a
            // bool[N] each time. We do it once here and walk it linearly per
            // frame.
            inst._topoOrder = ComputeTopologicalOrder(inst.AllNodes);

            // ---- All meshes + all primitives, one Submesh per primitive ----
            var minB = new Vector3(float.MaxValue);
            var maxB = new Vector3(float.MinValue);
            int meshes = 0, prims = 0, skipped = 0;

            foreach (var mesh in root.LogicalMeshes)
            {
                meshes++;
                foreach (var prim in mesh.Primitives)
                {
                    var positions = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
                    if (positions == null || positions.Count == 0) { skipped++; continue; }

                    var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
                    var uvs = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                    var joints = prim.GetVertexAccessor("JOINTS_0")?.AsVector4Array();
                    var weights = prim.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array();

                    int vcount = positions.Count;
                    var verts = new VertexSkinned[vcount];
                    for (int i = 0; i < vcount; i++)
                    {
                        var p = ToXna(positions[i]);
                        if (p.X < minB.X) minB.X = p.X; if (p.Y < minB.Y) minB.Y = p.Y; if (p.Z < minB.Z) minB.Z = p.Z;
                        if (p.X > maxB.X) maxB.X = p.X; if (p.Y > maxB.Y) maxB.Y = p.Y; if (p.Z > maxB.Z) maxB.Z = p.Z;

                        verts[i].Position = p;
                        verts[i].Normal = normals != null ? ToXna(normals[i]) : Vector3.Up;
                        verts[i].TexCoord = uvs != null ? ToXna(uvs[i]) : Vector2.Zero;
                        if (joints != null && weights != null)
                        {
                            var j = joints[i];
                            verts[i].BlendIndices = new Byte4((byte)j.X, (byte)j.Y, (byte)j.Z, (byte)j.W);
                            verts[i].BlendWeights = ToXna(weights[i]);
                        }
                        else
                        {
                            verts[i].BlendIndices = new Byte4(0, 0, 0, 0);
                            verts[i].BlendWeights = new Vector4(1, 0, 0, 0);
                        }
                    }

                    var idxAcc = prim.GetIndices();
                    int icount = idxAcc != null ? idxAcc.Count : vcount;
                    var meshName = mesh.Name ?? $"mesh_{meshes-1}";
                    // Mixamo "Beta_*" meshes ship with anim GLBs as skeleton-donor
                    // geometry only — they have no diffuse texture and rendering
                    // them covers the modular Synty attachments. Hide by default;
                    // can still be re-enabled via PlayerMaterialGump.
                    bool defaultVisible = !meshName.StartsWith("Beta_", StringComparison.OrdinalIgnoreCase);
                    var sub = new Submesh
                    {
                        MeshName = meshName,
                        MaterialName = prim.Material?.Name ?? "(default)",
                        Vertices = new VertexBuffer(gd, VertexSkinned.Decl, vcount, BufferUsage.WriteOnly),
                        IndexCount = icount,
                        Visible = defaultVisible,
                    };
                    sub.Vertices.SetData(verts);

                    if (vcount <= 65535)
                    {
                        var idx = new ushort[icount];
                        if (idxAcc != null) { for (int i = 0; i < icount; i++) idx[i] = (ushort)idxAcc[i]; }
                        else { for (int i = 0; i < vcount; i++) idx[i] = (ushort)i; }
                        sub.Indices = new IndexBuffer(gd, IndexElementSize.SixteenBits, icount, BufferUsage.WriteOnly);
                        sub.Indices.SetData(idx);
                    }
                    else
                    {
                        var idx = new int[icount];
                        if (idxAcc != null) { for (int i = 0; i < icount; i++) idx[i] = (int)idxAcc[i]; }
                        else { for (int i = 0; i < vcount; i++) idx[i] = i; }
                        sub.Indices = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, icount, BufferUsage.WriteOnly);
                        sub.Indices.SetData(idx);
                    }

                    // Material: BaseColor texture + factor (Vec4).
                    if (prim.Material != null)
                    {
                        var baseColor = prim.Material.FindChannel("BaseColor");
                        if (baseColor.HasValue)
                        {
                            // Color factor (always present; defaults to (1,1,1,1)).
                            var p4 = baseColor.Value.Parameters;
                            if (p4 != null && p4.Count > 0)
                            {
                                var c = p4[0].Value;
                                if (c is System.Numerics.Vector4 v4)
                                {
                                    sub.BaseColorFactor = ToXna(v4);
                                    sub.OriginalBaseColorFactor = sub.BaseColorFactor;
                                }
                            }

                            // Texture (optional).
                            if (baseColor.Value.Texture != null)
                            {
                                var img = baseColor.Value.Texture.PrimaryImage;
                                if (img != null)
                                {
                                    var bytes = img.Content.Content.ToArray();
                                    var mime = img.Content.MimeType ?? "?";
                                    string firstBytes = bytes.Length >= 8
                                        ? string.Join(" ", bytes.Take(8).Select(b => b.ToString("X2")))
                                        : "(short)";
                                    try
                                    {
                                        using var ms = new MemoryStream(bytes);
                                        sub.Texture = Texture2D.FromStream(gd, ms);
                                        TexDebugLog(
                                            $"OK '{img.Name}' {bytes.Length}B mime={mime} hdr={firstBytes} -> " +
                                            $"{sub.Texture?.Width}x{sub.Texture?.Height} fmt={sub.Texture?.Format} mat='{sub.MaterialName}'");
                                    }
                                    catch (Exception ex)
                                    {
                                        TexDebugLog(
                                            $"FAIL '{img.Name}' {bytes.Length}B mime={mime} hdr={firstBytes} " +
                                            $"mat='{sub.MaterialName}': {ex.GetType().Name}: {ex.Message}");
                                    }
                                }
                            }
                        }

                        // Synty source FBXs export Maya-Lambert "default colors"
                        // (e.g. 0.4,0.4,0.4 / 0.8,0.8,0.8) into baseColorFactor.
                        // Per glTF spec we multiply texture * factor, which darkens
                        // the palette texture. Synty's intent is texture = final
                        // color, so when a texture exists, neutralize the factor
                        // to (1,1,1,1). PlayerMaterialGump still has the original
                        // value via OriginalBaseColorFactor for reset/tinting.
                        if (sub.Texture != null)
                        {
                            sub.BaseColorFactor = Vector4.One;
                        }
                    }

                    Console.WriteLine(
                        $"[3DCUO]   submesh '{sub.MeshName}' / mat '{sub.MaterialName}': verts={vcount} idx={icount} " +
                        $"hasTex={sub.Texture != null} hasUVs={uvs != null} hasSkin={joints != null} factor={sub.BaseColorFactor}");

                    inst.Submeshes.Add(sub);
                    prims++;
                }
            }

            if (inst.Submeshes.Count == 0) throw new InvalidDataException("glb has no usable primitives");

            inst.Bounds = new BoundingBox(minB, maxB);

            Console.WriteLine($"[3DCUO] glb loaded {prims} submeshes across {meshes} meshes ({skipped} skipped)");

            // ---- Skin (bone list + inverse bind matrices) ----
            var skin = root.LogicalSkins.FirstOrDefault();
            if (skin != null)
            {
                int jc = skin.JointsCount;
                inst.JointNodeIndex = new int[jc];
                inst.InverseBindMatrices = new Matrix[jc];
                inst.SkinMatrices = new Matrix[jc];
                for (int i = 0; i < jc; i++)
                {
                    var (joint, ibm) = skin.GetJoint(i);
                    inst.JointNodeIndex[i] = nodeIndexById[new Node_t(joint)];
                    inst.InverseBindMatrices[i] = ToXnaMatrix(ibm);
                }
            }
            else
            {
                inst.JointNodeIndex = Array.Empty<int>();
                inst.InverseBindMatrices = Array.Empty<Matrix>();
                inst.SkinMatrices = Array.Empty<Matrix>();
            }

            // (Albedo texture is grabbed inside the primitive loop above — first
            //  BaseColor encountered wins.)

            // ---- Animations ----
            var anims = new List<Anim>();
            foreach (var a in root.LogicalAnimations)
            {
                var ours = new Anim { Name = a.Name ?? "anim" };
                float duration = 0f;
                foreach (var ch in a.Channels)
                {
                    if (!nodeIndexById.TryGetValue(new Node_t(ch.TargetNode), out var ti)) continue;
                    if (ch.GetTranslationSampler() is { } ts)
                    {
                        var (times, values) = SamplePairsVec3(ts);
                        ours.Channels.Add(new Channel { TargetNode = ti, Kind = ChannelKind.Translation, Times = times, Values = values });
                        if (times.Length > 0) duration = MathF.Max(duration, times[^1]);
                    }
                    if (ch.GetRotationSampler() is { } rs)
                    {
                        var (times, values) = SamplePairsVec4(rs);
                        ours.Channels.Add(new Channel { TargetNode = ti, Kind = ChannelKind.Rotation, Times = times, Values = values });
                        if (times.Length > 0) duration = MathF.Max(duration, times[^1]);
                    }
                    if (ch.GetScaleSampler() is { } ss)
                    {
                        var (times, values) = SamplePairsVec3(ss);
                        ours.Channels.Add(new Channel { TargetNode = ti, Kind = ChannelKind.Scale, Times = times, Values = values });
                        if (times.Length > 0) duration = MathF.Max(duration, times[^1]);
                    }
                }
                ours.Duration = duration;
                anims.Add(ours);
            }
            inst.Animations = anims.ToArray();

            return inst;
        }

        private static (float[] times, float[] values) SamplePairsVec3(SharpGLTF.Schema2.IAnimationSampler<System.Numerics.Vector3> s)
        {
            var keys = s.GetLinearKeys().ToArray();
            var times = new float[keys.Length];
            var vals = new float[keys.Length * 3];
            for (int i = 0; i < keys.Length; i++)
            {
                times[i] = keys[i].Key;
                vals[i*3+0] = keys[i].Value.X;
                vals[i*3+1] = keys[i].Value.Y;
                vals[i*3+2] = keys[i].Value.Z;
            }
            return (times, vals);
        }

        private static (float[] times, float[] values) SamplePairsVec4(SharpGLTF.Schema2.IAnimationSampler<System.Numerics.Quaternion> s)
        {
            var keys = s.GetLinearKeys().ToArray();
            var times = new float[keys.Length];
            var vals = new float[keys.Length * 4];
            for (int i = 0; i < keys.Length; i++)
            {
                times[i] = keys[i].Key;
                vals[i*4+0] = keys[i].Value.X;
                vals[i*4+1] = keys[i].Value.Y;
                vals[i*4+2] = keys[i].Value.Z;
                vals[i*4+3] = keys[i].Value.W;
            }
            return (times, vals);
        }

        // ---- Per-frame evaluation ----

        public void Update(float dt)
        {
            if (Animations == null || Animations.Length == 0) { ComputeBindPose(); return; }
            var anim = Animations[Math.Clamp(ActiveAnim, 0, Animations.Length - 1)];
            if (anim.Duration > 0f)
                AnimTime = (AnimTime + dt) % anim.Duration;

            // Single anim path — no blend.
            if (TargetAnim < 0 || TargetAnim >= Animations.Length || BlendWeight <= 0f)
            {
                EvaluateAnimation(anim, AnimTime);
                RecomputeWorldAndSkin();
                return;
            }

            // Cross-fade path — sample both, lerp per-node TRS.
            var anim2 = Animations[TargetAnim];
            if (anim2.Duration > 0f)
                TargetAnimTime = (TargetAnimTime + dt) % anim2.Duration;
            EvaluateAnimationBlended(anim, AnimTime, anim2, TargetAnimTime, MathHelper.Clamp(BlendWeight, 0f, 1f));
            RecomputeWorldAndSkin();
        }

        public void ResetToBindPose()
        {
            ComputeBindPose();
        }

        private void ComputeBindPose()
        {
            for (int i = 0; i < AllNodes.Length; i++)
            {
                ref var n = ref AllNodes[i];
                _localMatrices[i] = MakeTRS(n.T, n.R, n.S);
            }
            RecomputeWorldAndSkin();
        }

        private void EvaluateAnimation(Anim a, float t)
        {
            // Use pre-allocated scratch buffers; clear `has*` flags only.
            // The TRS arrays themselves don't need clearing — we read them
            // gated on the corresponding has-flag.
            int n = AllNodes.Length;
            Array.Clear(_evalHasT_a, 0, n);
            Array.Clear(_evalHasR_a, 0, n);
            Array.Clear(_evalHasS_a, 0, n);

            foreach (var ch in a.Channels)
            {
                int idx = ch.TargetNode;
                switch (ch.Kind)
                {
                    case ChannelKind.Translation:
                        _evalT_a[idx] = SampleVec3(ch, t); _evalHasT_a[idx] = true; break;
                    case ChannelKind.Rotation:
                        _evalR_a[idx] = SampleQuat(ch, t); _evalHasR_a[idx] = true; break;
                    case ChannelKind.Scale:
                        _evalS_a[idx] = SampleVec3(ch, t); _evalHasS_a[idx] = true; break;
                }
            }
            for (int i = 0; i < n; i++)
            {
                ref var nd = ref AllNodes[i];
                var T = _evalHasT_a[i] ? _evalT_a[i] : nd.T;
                var R = _evalHasR_a[i] ? _evalR_a[i] : nd.R;
                var S = _evalHasS_a[i] ? _evalS_a[i] : nd.S;
                _localMatrices[i] = MakeTRS(T, R, S);
            }
        }

        // Sample per-node TRS overrides for an anim into the supplied buffers.
        // Caller is responsible for clearing the has* flags before invoking.
        private void SampleAnimTRS(Anim a, float t,
            Vector3[] outT, Quaternion[] outR, Vector3[] outS,
            bool[] hasT, bool[] hasR, bool[] hasS)
        {
            int n = AllNodes.Length;
            Array.Clear(hasT, 0, n);
            Array.Clear(hasR, 0, n);
            Array.Clear(hasS, 0, n);
            foreach (var ch in a.Channels)
            {
                int idx = ch.TargetNode;
                switch (ch.Kind)
                {
                    case ChannelKind.Translation: outT[idx] = SampleVec3(ch, t); hasT[idx] = true; break;
                    case ChannelKind.Rotation:    outR[idx] = SampleQuat(ch, t); hasR[idx] = true; break;
                    case ChannelKind.Scale:       outS[idx] = SampleVec3(ch, t); hasS[idx] = true; break;
                }
            }
        }

        private void EvaluateAnimationBlended(Anim a, float ta, Anim b, float tb, float w)
        {
            int n = AllNodes.Length;
            // Reuses the _evalT_a/_evalT_b instance scratch — zero per-frame allocs.
            SampleAnimTRS(a, ta, _evalT_a, _evalR_a, _evalS_a, _evalHasT_a, _evalHasR_a, _evalHasS_a);
            SampleAnimTRS(b, tb, _evalT_b, _evalR_b, _evalS_b, _evalHasT_b, _evalHasR_b, _evalHasS_b);

            for (int i = 0; i < n; i++)
            {
                ref var nd = ref AllNodes[i];
                Vector3 T_a = _evalHasT_a[i] ? _evalT_a[i] : nd.T;
                Quaternion R_a = _evalHasR_a[i] ? _evalR_a[i] : nd.R;
                Vector3 S_a = _evalHasS_a[i] ? _evalS_a[i] : nd.S;
                Vector3 T_b = _evalHasT_b[i] ? _evalT_b[i] : nd.T;
                Quaternion R_b = _evalHasR_b[i] ? _evalR_b[i] : nd.R;
                Vector3 S_b = _evalHasS_b[i] ? _evalS_b[i] : nd.S;
                var T = Vector3.Lerp(T_a, T_b, w);
                var R = Quaternion.Slerp(R_a, R_b, w);
                var S = Vector3.Lerp(S_a, S_b, w);
                _localMatrices[i] = MakeTRS(T, R, S);
            }
        }

        private static Vector3 SampleVec3(Channel ch, float t)
        {
            if (ch.Times.Length == 0) return Vector3.Zero;
            int i = FindKey(ch.Times, t);
            if (i >= ch.Times.Length - 1) return new Vector3(ch.Values[(ch.Times.Length-1)*3+0], ch.Values[(ch.Times.Length-1)*3+1], ch.Values[(ch.Times.Length-1)*3+2]);
            float t0 = ch.Times[i], t1 = ch.Times[i+1];
            float a = t1 > t0 ? (t - t0) / (t1 - t0) : 0f;
            var v0 = new Vector3(ch.Values[i*3+0], ch.Values[i*3+1], ch.Values[i*3+2]);
            var v1 = new Vector3(ch.Values[(i+1)*3+0], ch.Values[(i+1)*3+1], ch.Values[(i+1)*3+2]);
            return Vector3.Lerp(v0, v1, a);
        }

        private static Quaternion SampleQuat(Channel ch, float t)
        {
            if (ch.Times.Length == 0) return Quaternion.Identity;
            int i = FindKey(ch.Times, t);
            if (i >= ch.Times.Length - 1) return new Quaternion(ch.Values[(ch.Times.Length-1)*4+0], ch.Values[(ch.Times.Length-1)*4+1], ch.Values[(ch.Times.Length-1)*4+2], ch.Values[(ch.Times.Length-1)*4+3]);
            float t0 = ch.Times[i], t1 = ch.Times[i+1];
            float a = t1 > t0 ? (t - t0) / (t1 - t0) : 0f;
            var q0 = new Quaternion(ch.Values[i*4+0], ch.Values[i*4+1], ch.Values[i*4+2], ch.Values[i*4+3]);
            var q1 = new Quaternion(ch.Values[(i+1)*4+0], ch.Values[(i+1)*4+1], ch.Values[(i+1)*4+2], ch.Values[(i+1)*4+3]);
            return Quaternion.Slerp(q0, q1, a);
        }

        private static int FindKey(float[] times, float t)
        {
            // Linear search; keyframe arrays are typically ≤ 60 entries.
            for (int i = 0; i < times.Length - 1; i++)
                if (times[i + 1] > t) return i;
            return times.Length - 1;
        }

        private void RecomputeWorldAndSkin()
        {
            // Walk in pre-computed topological order — every parent is visited
            // before any of its children. Single linear pass, no scratch alloc.
            for (int k = 0; k < _topoOrder.Length; k++)
            {
                int i = _topoOrder[k];
                int p = AllNodes[i].Parent;
                _worldMatrices[i] = p == -1
                    ? _localMatrices[i]
                    : _localMatrices[i] * _worldMatrices[p];
            }

            for (int i = 0; i < JointNodeIndex.Length; i++)
            {
                int ni = JointNodeIndex[i];
                SkinMatrices[i] = InverseBindMatrices[i] * _worldMatrices[ni];
            }
        }

        // Build a node-traversal order such that every parent appears before any
        // of its children (topological sort over the parent-pointer DAG). Run
        // once at load. Algorithm: depth-from-root by walking parent chains and
        // bucketing by depth, then emit nodes in ascending depth order. Stable
        // and O(N * avg-tree-depth).
        private static int[] ComputeTopologicalOrder(Node[] nodes)
        {
            int n = nodes.Length;
            var depth = new int[n];
            int maxDepth = 0;
            for (int i = 0; i < n; i++)
            {
                int d = 0;
                int cur = nodes[i].Parent;
                // Cap parent-walk at n to defend against malformed cycles.
                int safety = 0;
                while (cur != -1 && safety++ < n)
                {
                    d++;
                    cur = nodes[cur].Parent;
                }
                depth[i] = d;
                if (d > maxDepth) maxDepth = d;
            }
            var order = new int[n];
            int write = 0;
            for (int d = 0; d <= maxDepth; d++)
            {
                for (int i = 0; i < n; i++)
                    if (depth[i] == d) order[write++] = i;
            }
            return order;
        }

        // ---- Helpers ----

        private static Matrix MakeTRS(Vector3 T, Quaternion R, Vector3 S)
        {
            return Matrix.CreateScale(S) * Matrix.CreateFromQuaternion(R) * Matrix.CreateTranslation(T);
        }

        private static Vector2 ToXna(System.Numerics.Vector2 v) => new Vector2(v.X, v.Y);
        private static Vector3 ToXna(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        private static Vector4 ToXna(System.Numerics.Vector4 v) => new Vector4(v.X, v.Y, v.Z, v.W);
        private static Quaternion ToXna(System.Numerics.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
        private static Matrix ToXnaMatrix(System.Numerics.Matrix4x4 m) => new Matrix(
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44);

        // Wrapper to make SharpGLTF.Schema2.Node usable as a dictionary key.
        // (The class doesn't override GetHashCode/Equals based on logical identity in older versions.)
        private readonly struct Node_t : IEquatable<Node_t>
        {
            private readonly object _ref;
            public Node_t(object n) { _ref = n; }
            public bool Equals(Node_t other) => ReferenceEquals(_ref, other._ref);
            public override bool Equals(object obj) => obj is Node_t n && Equals(n);
            public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_ref);
        }

        public void Dispose()
        {
            if (Submeshes != null)
            {
                foreach (var s in Submeshes) s.Dispose();
                Submeshes.Clear();
            }
        }
    }
}
