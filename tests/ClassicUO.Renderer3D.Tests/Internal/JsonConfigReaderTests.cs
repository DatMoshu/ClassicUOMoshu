// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Tests for JsonConfigReader (Phase 4 shared helper).

using System.Text.Json;
using ClassicUO.Renderer.Renderer3D;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.Renderer3D.Tests.Internal
{
    /// <summary>
    /// Locks the parser-fallback contract every Phase 4 storage adapter depends on.
    /// JsonConfigReader's job is "return the parsed value if the JSON property exists
    /// with the right kind, otherwise return the supplied fallback." Adapters use this
    /// for every property load — when the contract drifts, six adapters silently load
    /// wrong values from older JSON files.
    /// </summary>
    public sealed class JsonConfigReaderTests
    {
        private enum SampleEnum { Alpha, Beta, Gamma }

        private static JsonElement Parse(string json)
        {
            return JsonDocument.Parse(json).RootElement;
        }

        // ===== ReadFloat =====

        [Fact]
        public void ReadFloat_PropertyPresent_ReturnsParsedValue()
        {
            JsonElement root = Parse("{ \"x\": 2.5 }");
            JsonConfigReader.ReadFloat(root, "x", -1f).Should().Be(2.5f);
        }

        [Fact]
        public void ReadFloat_PropertyMissing_ReturnsFallback()
        {
            JsonElement root = Parse("{ }");
            JsonConfigReader.ReadFloat(root, "x", 42f).Should().Be(42f);
        }

        [Fact]
        public void ReadFloat_PropertyWrongKind_ReturnsFallback()
        {
            // String value where a number is expected — adapter should NOT crash, just fall back.
            JsonElement root = Parse("{ \"x\": \"oops\" }");
            JsonConfigReader.ReadFloat(root, "x", 7.5f).Should().Be(7.5f);
        }

        // ===== ReadInt =====

        [Fact]
        public void ReadInt_PropertyPresent_ReturnsParsedValue()
        {
            JsonElement root = Parse("{ \"n\": 17 }");
            JsonConfigReader.ReadInt(root, "n", 0).Should().Be(17);
        }

        [Fact]
        public void ReadInt_PropertyMissing_ReturnsFallback()
        {
            JsonElement root = Parse("{ }");
            JsonConfigReader.ReadInt(root, "n", -1).Should().Be(-1);
        }

        [Fact]
        public void ReadInt_NegativeValue_RoundTrips()
        {
            JsonElement root = Parse("{ \"n\": -889122578 }");
            JsonConfigReader.ReadInt(root, "n", 0).Should().Be(-889122578);
        }

        // ===== ReadBool =====

        [Theory]
        [InlineData("{ \"flag\": true }",  true,  true)]
        [InlineData("{ \"flag\": false }", true,  false)]
        [InlineData("{ \"flag\": true }",  false, true)]
        [InlineData("{ \"flag\": false }", false, false)]
        public void ReadBool_PropertyPresent_ReturnsParsedValue(string json, bool fallback, bool expected)
        {
            JsonElement root = Parse(json);
            JsonConfigReader.ReadBool(root, "flag", fallback).Should().Be(expected);
        }

        [Fact]
        public void ReadBool_PropertyMissing_ReturnsFallback()
        {
            JsonElement root = Parse("{ }");
            JsonConfigReader.ReadBool(root, "flag", true).Should().BeTrue();
            JsonConfigReader.ReadBool(root, "flag", false).Should().BeFalse();
        }

        [Fact]
        public void ReadBool_PropertyWrongKind_ReturnsFallback()
        {
            JsonElement root = Parse("{ \"flag\": 1 }"); // JSON number, not bool
            JsonConfigReader.ReadBool(root, "flag", false).Should().BeFalse();
        }

        // ===== ReadString =====

        [Fact]
        public void ReadString_PropertyPresent_ReturnsParsedValue()
        {
            JsonElement root = Parse("{ \"s\": \"hello\" }");
            JsonConfigReader.ReadString(root, "s", "fallback").Should().Be("hello");
        }

        [Fact]
        public void ReadString_PropertyMissing_ReturnsFallback()
        {
            JsonElement root = Parse("{ }");
            JsonConfigReader.ReadString(root, "s", "fallback").Should().Be("fallback");
        }

        [Fact]
        public void ReadString_PropertyWrongKind_ReturnsFallback()
        {
            JsonElement root = Parse("{ \"s\": 42 }");
            JsonConfigReader.ReadString(root, "s", "fallback").Should().Be("fallback");
        }

        // ===== ReadVector3 =====

        [Fact]
        public void ReadVector3_FullObjectPresent_ReturnsParsedValue()
        {
            JsonElement root = Parse("{ \"dir\": { \"x\": 1.5, \"y\": -2, \"z\": 0.25 } }");
            JsonConfigReader.ReadVector3(root, "dir", Vector3.Zero)
                .Should().Be(new Vector3(1.5f, -2f, 0.25f));
        }

        [Fact]
        public void ReadVector3_PartialObject_FillsMissingComponentsFromFallback()
        {
            // Only x specified — y and z should pull from fallback.
            JsonElement root = Parse("{ \"dir\": { \"x\": 5.0 } }");
            JsonConfigReader.ReadVector3(root, "dir", new Vector3(0f, 10f, 20f))
                .Should().Be(new Vector3(5f, 10f, 20f));
        }

        [Fact]
        public void ReadVector3_PropertyMissing_ReturnsFallback()
        {
            JsonElement root = Parse("{ }");
            JsonConfigReader.ReadVector3(root, "dir", new Vector3(7f, 8f, 9f))
                .Should().Be(new Vector3(7f, 8f, 9f));
        }

        [Fact]
        public void ReadVector3_PropertyWrongKind_ReturnsFallback()
        {
            JsonElement root = Parse("{ \"dir\": 42 }");
            JsonConfigReader.ReadVector3(root, "dir", Vector3.One).Should().Be(Vector3.One);
        }

        // ===== ReadEnum =====

        [Fact]
        public void ReadEnum_PropertyPresent_ReturnsParsedValue()
        {
            JsonElement root = Parse("{ \"e\": \"Beta\" }");
            JsonConfigReader.ReadEnum<SampleEnum>(root, "e", SampleEnum.Alpha)
                .Should().Be(SampleEnum.Beta);
        }

        [Fact]
        public void ReadEnum_PropertyCaseInsensitive_StillParses()
        {
            JsonElement root = Parse("{ \"e\": \"gAMmA\" }");
            JsonConfigReader.ReadEnum<SampleEnum>(root, "e", SampleEnum.Alpha)
                .Should().Be(SampleEnum.Gamma);
        }

        [Fact]
        public void ReadEnum_PropertyMissing_ReturnsFallback()
        {
            JsonElement root = Parse("{ }");
            JsonConfigReader.ReadEnum<SampleEnum>(root, "e", SampleEnum.Gamma)
                .Should().Be(SampleEnum.Gamma);
        }

        [Fact]
        public void ReadEnum_PropertyInvalidName_ReturnsFallback()
        {
            JsonElement root = Parse("{ \"e\": \"Delta\" }"); // not in the enum
            JsonConfigReader.ReadEnum<SampleEnum>(root, "e", SampleEnum.Alpha)
                .Should().Be(SampleEnum.Alpha);
        }

        [Fact]
        public void ReadEnum_PropertyWrongKind_ReturnsFallback()
        {
            JsonElement root = Parse("{ \"e\": 1 }"); // number where string expected
            JsonConfigReader.ReadEnum<SampleEnum>(root, "e", SampleEnum.Beta)
                .Should().Be(SampleEnum.Beta);
        }
    }
}
