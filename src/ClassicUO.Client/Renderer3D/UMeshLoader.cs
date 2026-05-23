// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO migration smoke test — Option B (pre-bake) loader.
// Reads the .umesh v0 binary format produced by tools/scripts/glb-to-umesh.py.
// SharpGLTF-free by construction; pure BinaryReader + StbImageSharp for PNG.
//
// Format: see tools/scripts/glb-to-umesh.py header comment for the spec.

using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>Result of loading a baked .umesh file: GPU buffers + optional diffuse texture.</summary>
    internal sealed class UMesh : IDisposable
    {
        public VertexBuffer VertexBuffer;
        public IndexBuffer  IndexBuffer;
        public Texture2D    Texture;
        public int          VertexCount;
        public int          IndexCount;
        public int          PrimitiveCount;

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
            Texture?.Dispose();
            VertexBuffer = null;
            IndexBuffer  = null;
            Texture      = null;
        }
    }

    internal static class UMeshLoader
    {
        // 'UMSH' as little-endian u32: bytes 0x55 0x4D 0x53 0x48.
        private const uint MAGIC   = 0x48534D55u;
        private const uint VERSION = 0u;
        private const uint FLAG_HAS_TEXTURE = 1u << 0;
        private const int  HEADER_BYTES     = 32;
        private const int  VERTEX_BYTES     = 32; // VertexPositionNormalTexture

        /// <summary>Load a .umesh file into GPU resources. Throws on bad magic/version/data.</summary>
        public static UMesh Load(GraphicsDevice gd, string path)
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is empty", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("umesh not found", path);

            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            uint magic       = br.ReadUInt32();
            if (magic != MAGIC)
                throw new InvalidDataException($"bad magic 0x{magic:X8} (expected 0x{MAGIC:X8})");

            uint version     = br.ReadUInt32();
            if (version != VERSION)
                throw new InvalidDataException($"unsupported umesh version {version}");

            uint flags       = br.ReadUInt32();
            uint vertexCount = br.ReadUInt32();
            uint indexCount  = br.ReadUInt32();
            uint indexStride = br.ReadUInt32();
            uint textureLen  = br.ReadUInt32();
            _ = br.ReadUInt32(); // reserved

            if (vertexCount == 0)
                throw new InvalidDataException("vertexCount=0");
            if (indexCount == 0)
                throw new InvalidDataException("indexCount=0");
            if (indexStride != 2 && indexStride != 4)
                throw new InvalidDataException($"indexStride must be 2 or 4 (got {indexStride})");
            if ((indexCount % 3) != 0)
                throw new InvalidDataException($"indexCount must be a multiple of 3 (got {indexCount})");

            // Vertex buffer: VertexPositionNormalTexture, 32 bytes each.
            int vbBytes = checked((int)vertexCount * VERTEX_BYTES);
            byte[] vbData = br.ReadBytes(vbBytes);
            if (vbData.Length != vbBytes)
                throw new EndOfStreamException("truncated vertex stream");

            var vb = new VertexBuffer(
                gd,
                VertexPositionNormalTexture.VertexDeclaration,
                (int)vertexCount,
                BufferUsage.WriteOnly
            );
            vb.SetData(vbData);

            // Index buffer.
            int ibBytes = checked((int)indexCount * (int)indexStride);
            byte[] ibData = br.ReadBytes(ibBytes);
            if (ibData.Length != ibBytes)
                throw new EndOfStreamException("truncated index stream");

            var indexElement = indexStride == 2 ? IndexElementSize.SixteenBits : IndexElementSize.ThirtyTwoBits;
            var ib = new IndexBuffer(gd, indexElement, (int)indexCount, BufferUsage.WriteOnly);
            ib.SetData(ibData);

            // Skip pad bytes (index stream padded to 4-byte boundary by baker).
            int idxPad = (-ibBytes) & 3;
            if (idxPad > 0) br.ReadBytes(idxPad);

            // Optional embedded texture (PNG/JPEG).
            Texture2D tex = null;
            if ((flags & FLAG_HAS_TEXTURE) != 0 && textureLen > 0)
            {
                byte[] texBytes = br.ReadBytes((int)textureLen);
                if (texBytes.Length != (int)textureLen)
                    throw new EndOfStreamException("truncated texture stream");

                using var ms = new MemoryStream(texBytes, writable: false);
                tex = Texture2D.FromStream(gd, ms);
            }

            return new UMesh
            {
                VertexBuffer   = vb,
                IndexBuffer    = ib,
                Texture        = tex,
                VertexCount    = (int)vertexCount,
                IndexCount     = (int)indexCount,
                PrimitiveCount = (int)indexCount / 3,
            };
        }
    }
}
