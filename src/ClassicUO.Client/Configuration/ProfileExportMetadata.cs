// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.Configuration
{
    /// <summary>
    /// Author-supplied metadata for a profile export.
    /// Passed to <see cref="ProfilePackageWriter.Write"/> to avoid parameter sprawl.
    /// </summary>
    internal sealed class ProfileExportMetadata
    {
        public string ProfileName  { get; init; } = string.Empty;
        public string Description  { get; init; } = string.Empty;
        public string[] Tags       { get; init; } = System.Array.Empty<string>();
        public uint AuthorSerial   { get; init; }
        public string Faction      { get; init; } = string.Empty;
    }
}
