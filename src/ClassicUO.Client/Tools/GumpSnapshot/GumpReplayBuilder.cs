// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Text;
using ClassicUO.Game;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.IO;
using ClassicUO.Network;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Tools.GumpSnapshot
{
    /// <summary>
    /// Reconstructs a client-side <see cref="Gump"/> from a raw 0xDD packet
    /// dump produced by the server-side <c>[DumpAllGumps</c> command. Mirrors
    /// the logic of <c>PacketHandlers.OpenCompressedGump</c> but reads from a
    /// file instead of the network, and routes to
    /// <c>PacketHandlers.CreateGump</c> (now internal) to share the layout
    /// parser with the production path.
    /// </summary>
    internal static class GumpReplayBuilder
    {
        public static Gump BuildFromBin(World world, string binPath)
        {
            if (!File.Exists(binPath))
            {
                Log.Error($"[gump-replay] bin not found: {binPath}");
                return null;
            }

            byte[] bytes;
            try { bytes = File.ReadAllBytes(binPath); }
            catch (Exception ex) { Log.Error($"[gump-replay] read failed: {ex}"); return null; }

            if (bytes.Length < 24 || bytes[0] != 0xDD)
            {
                Log.Error($"[gump-replay] not a 0xDD packet (opcode=0x{bytes[0]:X2}, len={bytes.Length})");
                return null;
            }

            // 0xDD layout (matches PacketHandlers.OpenCompressedGump):
            //   [0]   opcode 0xDD
            //   [1-2] packet length (BE)
            //   [3-6] sender serial (BE)
            //   [7-10] gump id (BE)
            //   [11-14] x (BE)
            //   [15-18] y (BE)
            //   [19-22] compressed-len + 4 (BE)
            //   [23-26] decompressed-len (BE)
            //   [27+]   zlib(layout) bytes
            //   then    uint linesCount (BE)
            //           if linesCount > 0:
            //             compressed-len+4 (BE), decompressed-len (BE), zlib(lines)
            //           each line: ushort length (BE), unicode-BE chars

            int pos = 3;

            uint sender = ReadUInt32BE(bytes, ref pos);
            uint gumpID = ReadUInt32BE(bytes, ref pos);
            uint x = ReadUInt32BE(bytes, ref pos);
            uint y = ReadUInt32BE(bytes, ref pos);
            uint clen = ReadUInt32BE(bytes, ref pos) - 4;
            int dlen = (int)ReadUInt32BE(bytes, ref pos);

            string layout;
            try
            {
                var dec = new byte[dlen];
                var rc = ZLib.Decompress(bytes.AsSpan(pos, (int)clen), dec.AsSpan(0, dlen));
                if (rc != ZLib.ZLibError.Ok)
                {
                    Log.Error($"[gump-replay] layout decompress failed: {rc}");
                    return null;
                }
                layout = Encoding.UTF8.GetString(dec, 0, dlen);
            }
            catch (Exception ex)
            {
                Log.Error($"[gump-replay] layout decompress threw: {ex}");
                return null;
            }
            pos += (int)clen;

            uint linesNum = ReadUInt32BE(bytes, ref pos);
            string[] lines = new string[linesNum];

            if (linesNum > 0)
            {
                try
                {
                    clen = ReadUInt32BE(bytes, ref pos) - 4;
                    dlen = (int)ReadUInt32BE(bytes, ref pos);
                    var dec = new byte[dlen];
                    var rc = ZLib.Decompress(bytes.AsSpan(pos, (int)clen), dec.AsSpan(0, dlen));
                    if (rc != ZLib.ZLibError.Ok)
                    {
                        Log.Error($"[gump-replay] lines decompress failed: {rc}");
                        return null;
                    }
                    pos += (int)clen;

                    int lp = 0;
                    for (int i = 0; i < linesNum && lp + 2 <= dlen; i++)
                    {
                        int length = (dec[lp] << 8) | dec[lp + 1];
                        lp += 2;
                        if (length > 0 && lp + length * 2 <= dlen)
                        {
                            lines[i] = Encoding.BigEndianUnicode.GetString(dec, lp, length * 2);
                            lp += length * 2;
                        }
                        else
                        {
                            lines[i] = string.Empty;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[gump-replay] lines decompress threw: {ex}");
                    return null;
                }
            }

            try
            {
                return PacketHandlers.CreateGump(world, sender, gumpID, (int)x, (int)y, layout, lines);
            }
            catch (Exception ex)
            {
                Log.Error($"[gump-replay] CreateGump threw: {ex}");
                return null;
            }
        }

        private static uint ReadUInt32BE(byte[] buf, ref int pos)
        {
            uint v = ((uint)buf[pos] << 24) | ((uint)buf[pos + 1] << 16) | ((uint)buf[pos + 2] << 8) | buf[pos + 3];
            pos += 4;
            return v;
        }
    }
}
