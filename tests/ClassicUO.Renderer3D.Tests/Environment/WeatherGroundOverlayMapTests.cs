// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Tests for IWeatherGroundOverlayMap (session-66 Phase 4 pilot).

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Environment;
using FluentAssertions;
using Xunit;

namespace ClassicUO.Renderer3D.Tests.Environment
{
    /// <summary>
    /// Coverage for <see cref="WeatherGroundOverlayMap"/> from session 66, the Phase 4
    /// data-driven coupling pilot. Locks the per-frame lookup behavior that
    /// <c>GroundOverlayPass.UpdateGroundEffectEase</c> depends on each frame.
    /// </summary>
    public sealed class WeatherGroundOverlayMapTests
    {
        [Fact]
        public void Constructor_RejectsNullStorage()
        {
            Action act = () => new WeatherGroundOverlayMap(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void DefaultLoad_ProducesThreeMappingsMirroringPreviousHardcodedBlock()
        {
            // Defaults must match the pre-Phase-4 hardcoded if/else block byte-for-byte:
            // Rain → Wet (1.0), Storm → Wet (1.0), Snow → Snow (1.0). Anything else
            // falls through to no-mapping (target intensity → 0).
            var map = new WeatherGroundOverlayMap(FakeStorage.LegacyDefaults());
            map.EnsureLoaded();

            map.LoadedMappingCount.Should().Be(3);
            map.LastError.Should().BeNull();

            map.TryGet(WeatherKind.Rain, out WeatherGroundOverlayEntry rain).Should().BeTrue();
            rain.Mode.Should().Be(GroundEffectMode.Wet);
            rain.StrengthMultiplier.Should().Be(1.0f);

            map.TryGet(WeatherKind.Storm, out WeatherGroundOverlayEntry storm).Should().BeTrue();
            storm.Mode.Should().Be(GroundEffectMode.Wet);

            map.TryGet(WeatherKind.Snow, out WeatherGroundOverlayEntry snow).Should().BeTrue();
            snow.Mode.Should().Be(GroundEffectMode.Snow);
        }

        [Theory]
        [InlineData(WeatherKind.None)]
        [InlineData(WeatherKind.Sandstorm)]
        [InlineData(WeatherKind.Embers)]
        [InlineData(WeatherKind.Fog)]
        [InlineData(WeatherKind.BloodMoon)]
        [InlineData(WeatherKind.Blizzard)]
        [InlineData(WeatherKind.Tornado)]
        public void TryGet_UnmappedKinds_ReturnFalseSoCallerFadesIntensityToZero(WeatherKind kind)
        {
            var map = new WeatherGroundOverlayMap(FakeStorage.LegacyDefaults());

            bool got = map.TryGet(kind, out WeatherGroundOverlayEntry entry);

            got.Should().BeFalse();
            entry.Mode.Should().Be(GroundEffectMode.None);
            entry.StrengthMultiplier.Should().Be(0f);
        }

        [Fact]
        public void TryGet_CustomMultipliers_RoundtripThroughLoadedMap()
        {
            // A future "drizzle" entry might want 0.4 strength; lock that round-trip works.
            var map = new WeatherGroundOverlayMap(FakeStorage.WithMappings(
                new Dictionary<WeatherKind, WeatherGroundOverlayEntry>
                {
                    [WeatherKind.Rain]  = new WeatherGroundOverlayEntry(GroundEffectMode.Wet,  0.4f),
                    [WeatherKind.Storm] = new WeatherGroundOverlayEntry(GroundEffectMode.Wet,  1.0f),
                    [WeatherKind.Snow]  = new WeatherGroundOverlayEntry(GroundEffectMode.Snow, 0.85f),
                }));

            map.TryGet(WeatherKind.Rain, out WeatherGroundOverlayEntry rain).Should().BeTrue();
            rain.StrengthMultiplier.Should().Be(0.4f);

            map.TryGet(WeatherKind.Snow, out WeatherGroundOverlayEntry snow).Should().BeTrue();
            snow.StrengthMultiplier.Should().Be(0.85f);
        }

        [Fact]
        public void EnsureLoaded_IsIdempotent()
        {
            var counter = new CountingStorage();
            var map = new WeatherGroundOverlayMap(counter);

            map.EnsureLoaded();
            map.EnsureLoaded();
            map.EnsureLoaded();

            counter.LoadCallCount.Should().Be(1);
        }

        [Fact]
        public void LoadFailure_DegradesGracefullyTo_EmptyTableWithErrorMessage()
        {
            var map = new WeatherGroundOverlayMap(new FakeStorage(
                new WeatherGroundOverlayMapLoadResult(
                    success: false, errorMessage: "file missing",
                    configPath: "/missing/path.json", mappings: null)));

            map.EnsureLoaded();
            map.LoadedMappingCount.Should().Be(0);
            map.LastError.Should().Be("file missing");
            map.ConfigPath.Should().Be("/missing/path.json");

            // The renderer behavior when no mappings exist: every weather kind falls
            // through to the "fade to 0" branch — i.e. no ground overlay.
            map.TryGet(WeatherKind.Rain, out _).Should().BeFalse();
            map.TryGet(WeatherKind.Storm, out _).Should().BeFalse();
            map.TryGet(WeatherKind.Snow, out _).Should().BeFalse();
        }

        // ===== Test doubles =====

        private sealed class FakeStorage : IWeatherGroundOverlayMapStorage
        {
            private readonly WeatherGroundOverlayMapLoadResult _canned;
            public FakeStorage(WeatherGroundOverlayMapLoadResult canned) { _canned = canned; }
            public WeatherGroundOverlayMapLoadResult Load() => _canned;

            public static FakeStorage WithMappings(IReadOnlyDictionary<WeatherKind, WeatherGroundOverlayEntry> mappings)
                => new FakeStorage(new WeatherGroundOverlayMapLoadResult(
                    success: true, errorMessage: null,
                    configPath: "fake/weather-ground-overlay.json",
                    mappings: mappings));

            public static FakeStorage LegacyDefaults() => WithMappings(
                new Dictionary<WeatherKind, WeatherGroundOverlayEntry>
                {
                    [WeatherKind.Rain]  = new WeatherGroundOverlayEntry(GroundEffectMode.Wet,  1.0f),
                    [WeatherKind.Storm] = new WeatherGroundOverlayEntry(GroundEffectMode.Wet,  1.0f),
                    [WeatherKind.Snow]  = new WeatherGroundOverlayEntry(GroundEffectMode.Snow, 1.0f),
                });
        }

        private sealed class CountingStorage : IWeatherGroundOverlayMapStorage
        {
            public int LoadCallCount { get; private set; }
            public WeatherGroundOverlayMapLoadResult Load()
            {
                LoadCallCount++;
                return new WeatherGroundOverlayMapLoadResult(
                    success: true, errorMessage: null,
                    configPath: "p",
                    mappings: new Dictionary<WeatherKind, WeatherGroundOverlayEntry>());
            }
        }
    }
}
