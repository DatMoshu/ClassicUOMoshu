// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Tests for IRoofRegistryService (session-65 hybrid-facade data half).

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Statics;
using FluentAssertions;
using Xunit;
// Legacy ClassicUO.Renderer.Renderer3D also declares RoofTileTag/RoofMeshEntry as
// [Obsolete] structs for source-compat; alias the legacy enum and avoid importing
// the namespace so unqualified RoofTileTag/RoofMeshEntry bind to the domain types.
using RoofArchetype = ClassicUO.Renderer.Renderer3D.RoofArchetype;

namespace ClassicUO.Renderer3D.Tests.Statics
{
    /// <summary>
    /// Coverage for <see cref="RoofRegistryService"/> from session 65. The service is
    /// pure-state (no GPU, no events) so a fake <see cref="IRoofRegistryStorage"/>
    /// drives everything; no <c>GraphicsDevice</c> fixture needed.
    /// </summary>
    public sealed class RoofRegistryServiceTests
    {
        [Fact]
        public void Constructor_RejectsNullStorage()
        {
            Action act = () => new RoofRegistryService(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void EnsureLoaded_PopulatesCountsAndDiagnostics()
        {
            var storage = FakeStorage.WithEntries(
                tags: new Dictionary<ushort, RoofTileTag>
                {
                    [0x0001] = new RoofTileTag("1_0", "Wooden Shingles", RoofArchetype.SlopeS),
                },
                entries: new Dictionary<string, RoofMeshEntry>
                {
                    ["1_0|roof_slope"] = new RoofMeshEntry("roof_slope.glb", "wooden_shingles.png"),
                });

            var svc = new RoofRegistryService(storage);
            svc.EnsureLoaded();

            svc.LoadedTagCount.Should().Be(1);
            svc.LoadedManifestCount.Should().Be(1);
            svc.TagLoadError.Should().BeNull();
            svc.ManifestLoadError.Should().BeNull();
            svc.TagDataPath.Should().Be("path/to/tags.json");
            svc.MeshesDir.Should().Be("path/to/meshes");
            svc.ResolvedVersion.Should().Be("1.0.0");
        }

        [Fact]
        public void EnsureLoaded_IsIdempotent()
        {
            var storage = FakeStorage.CountingLoads(out CountingStorage counter);
            var svc = new RoofRegistryService(storage);

            svc.EnsureLoaded();
            svc.EnsureLoaded();
            svc.EnsureLoaded();

            counter.LoadCallCount.Should().Be(1);
        }

        [Fact]
        public void TryResolve_TagPresentAndManifestEntryPresent_ReturnsBoth()
        {
            var storage = FakeStorage.WithEntries(
                tags: new Dictionary<ushort, RoofTileTag>
                {
                    [0x00A1] = new RoofTileTag("8_0", "Ceramic Tiles 1", RoofArchetype.RidgeEW),
                },
                entries: new Dictionary<string, RoofMeshEntry>
                {
                    ["8_0|roof_ridge"] = new RoofMeshEntry("ridge.glb", "ceramic1.png"),
                });

            var svc = new RoofRegistryService(storage);

            bool found = svc.TryResolve(0x00A1, out RoofTileTag tag, out RoofMeshEntry entry);

            found.Should().BeTrue();
            tag.Family.Should().Be("8_0");
            tag.Archetype.Should().Be(RoofArchetype.RidgeEW);
            entry.MeshFile.Should().Be("ridge.glb");
            entry.AtlasFile.Should().Be("ceramic1.png");
        }

        [Fact]
        public void TryResolve_TagMissing_ReturnsFalseAndDefaults()
        {
            var storage = FakeStorage.WithEntries(
                tags: new Dictionary<ushort, RoofTileTag>(),
                entries: new Dictionary<string, RoofMeshEntry>());
            var svc = new RoofRegistryService(storage);

            bool found = svc.TryResolve(0xDEAD, out RoofTileTag tag, out RoofMeshEntry entry);

            found.Should().BeFalse();
            tag.Family.Should().BeNull();
            entry.MeshFile.Should().BeNull();
        }

        [Fact]
        public void TryResolve_TagPresentButManifestMissing_ReturnsFalseWithEntryDefault()
        {
            // Tag is present, but no manifest entry for "1_0|roof_slope". Models still
            // baking; renderer's fallback path triggers via the false return value.
            var storage = FakeStorage.WithEntries(
                tags: new Dictionary<ushort, RoofTileTag>
                {
                    [0x0001] = new RoofTileTag("1_0", "Wooden Shingles", RoofArchetype.SlopeS),
                },
                entries: new Dictionary<string, RoofMeshEntry>());

            var svc = new RoofRegistryService(storage);

            bool found = svc.TryResolve(0x0001, out RoofTileTag tag, out RoofMeshEntry entry);

            found.Should().BeFalse();
            tag.Family.Should().Be("1_0"); // tag is populated even when entry is missing
            entry.MeshFile.Should().BeNull();
        }

        [Fact]
        public void Invalidate_ResetsStateAndAllowsReload()
        {
            var storage = FakeStorage.CountingLoads(out CountingStorage counter);
            var svc = new RoofRegistryService(storage);

            svc.EnsureLoaded();
            counter.LoadCallCount.Should().Be(1);

            svc.Invalidate();
            svc.LoadedTagCount.Should().Be(0);
            svc.LoadedManifestCount.Should().Be(0);
            svc.TagDataPath.Should().BeNull();

            svc.EnsureLoaded();
            counter.LoadCallCount.Should().Be(2);
        }

        [Fact]
        public void LoadFailure_DegradesGracefullyWithErrorMessages()
        {
            var storage = new FakeStorage(new RoofRegistryLoadResult(
                tagsSuccess: false, manifestSuccess: false,
                tagLoadError: "tag json missing",
                manifestLoadError: "manifest json missing",
                tagDataPath: null, meshesDir: null, resolvedVersion: null,
                tagsByGraphic: null, entriesByFamilyMesh: null));

            var svc = new RoofRegistryService(storage);
            svc.EnsureLoaded();

            svc.TagLoadError.Should().Be("tag json missing");
            svc.ManifestLoadError.Should().Be("manifest json missing");
            svc.LoadedTagCount.Should().Be(0);
            svc.LoadedManifestCount.Should().Be(0);

            svc.TryResolve(0x0001, out _, out _).Should().BeFalse();
        }

        // ===== Test doubles =====

        private sealed class FakeStorage : IRoofRegistryStorage
        {
            private readonly RoofRegistryLoadResult _canned;
            public FakeStorage(RoofRegistryLoadResult canned) { _canned = canned; }
            public RoofRegistryLoadResult Load() => _canned;

            public static FakeStorage WithEntries(
                IReadOnlyDictionary<ushort, ClassicUO.Renderer.Statics.RoofTileTag> tags,
                IReadOnlyDictionary<string, ClassicUO.Renderer.Statics.RoofMeshEntry> entries)
                => new FakeStorage(new RoofRegistryLoadResult(
                    tagsSuccess: true, manifestSuccess: true,
                    tagLoadError: null, manifestLoadError: null,
                    tagDataPath: "path/to/tags.json",
                    meshesDir: "path/to/meshes",
                    resolvedVersion: "1.0.0",
                    tagsByGraphic: tags,
                    entriesByFamilyMesh: entries));

            public static IRoofRegistryStorage CountingLoads(out CountingStorage counter)
            {
                counter = new CountingStorage();
                return counter;
            }
        }

        private sealed class CountingStorage : IRoofRegistryStorage
        {
            public int LoadCallCount { get; private set; }
            public RoofRegistryLoadResult Load()
            {
                LoadCallCount++;
                return new RoofRegistryLoadResult(
                    tagsSuccess: true, manifestSuccess: true,
                    tagLoadError: null, manifestLoadError: null,
                    tagDataPath: "p", meshesDir: "m", resolvedVersion: "v",
                    tagsByGraphic: new Dictionary<ushort, ClassicUO.Renderer.Statics.RoofTileTag>(),
                    entriesByFamilyMesh: new Dictionary<string, ClassicUO.Renderer.Statics.RoofMeshEntry>());
            }
        }
    }
}
