// Copyright 2026 Cyanflower
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Lunarium.Logging.Config.GlobalConfig;

namespace Lunarium.Logging.Tests.Config;

/// <summary>
/// Tests for GlobalConfigurator, the fluent global-config API.
///
/// IMPORTANT: Both GlobalConfigLock.Configured (internal static bool) and
/// GlobalConfigurator._isConfiguring (private static bool) are static state.
/// Full isolation requires resetting both before each test, which we do
/// via a direct field write for the internal one and reflection for the private one.
///
/// This collection is non-parallel to prevent races with other tests touching
/// these statics.
/// </summary>
[CollectionDefinition("GlobalConfigurator", DisableParallelization = true)]
public class GlobalConfiguratorCollectionDef { }

[Collection("GlobalConfigurator")]
public class GlobalConfiguratorTests
{
    private static readonly FieldInfo? _isConfiguringField =
        typeof(GlobalConfigurator).GetField("_isConfiguring",
            BindingFlags.Static | BindingFlags.NonPublic);

    private static readonly FieldInfo? _customResolverField =
        typeof(JsonSerializationConfig).GetField("_customResolver",
            BindingFlags.Static | BindingFlags.NonPublic);

    private static readonly FieldInfo? _optionsField =
        typeof(JsonSerializationConfig).GetField("_options",
            BindingFlags.Static | BindingFlags.NonPublic);

    /// <summary>
    /// Reset ALL static guard state so Configure() is allowed again.
    /// Must be called at the START of every test (and optionally at the end for hygiene).
    /// </summary>
    private static void ResetAll()
    {
        GlobalConfigLock.Configured = false;
        _isConfiguringField?.SetValue(null, false);
        _customResolverField?.SetValue(null, null);
        _optionsField?.SetValue(null, null);
        // Reset JsonSerializationConfig to defaults
        JsonSerializationConfig.ConfigUnsafeRelaxedJsonEscaping(true);
        JsonSerializationConfig.ConfigWriteIndented(false);
    }

    // ─── Test helpers ─────────────────────────────────────────────────────────

    private sealed class StubResolver : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Double-configure guard
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Configure_WhenAlreadyConfigured_Throws()
    {
        ResetAll();
        GlobalConfigurator.Configure().Apply();

        Action act = () => GlobalConfigurator.Configure();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already*");

        ResetAll();
    }

    [Fact]
    public void ApplyConfiguration_WhenNotConfiguring_Throws()
    {
        ResetAll();
        // Since it's internal we invoke it directly via the type
        Action act = () => GlobalConfigurator.ApplyConfiguration();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No configuration in progress*");
    }

    [Fact]
    public void AddConfigOperation_WhenNotConfiguring_Throws()
    {
        ResetAll();
        Action act = () => GlobalConfigurator.AddConfigOperation(() => { });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Configure_WhenNotYetConfigured_ReturnsBuilder()
    {
        ResetAll();
        var builder = GlobalConfigurator.Configure();
        builder.Should().NotBeNull();
        builder.Apply();
        ResetAll();
    }

    [Fact]
    public void Configure_WhileInProgress_Throws()
    {
        ResetAll();
        // Start Configure but don't Apply yet
        var builder = GlobalConfigurator.Configure();
        // Second Configure() should throw "in progress"
        Action act = () => GlobalConfigurator.Configure();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*progress*");
        // Clean up
        builder.Apply();
        ResetAll();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Apply() — locks the config
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_SetsConfiguredToTrue()
    {
        ResetAll();
        GlobalConfigLock.Configured.Should().BeFalse();
        GlobalConfigurator.Configure().Apply();
        GlobalConfigLock.Configured.Should().BeTrue();
        ResetAll();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Timezone configuration
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UseUtcTimeZone_SetsTimestampToUtc()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseUtcTimeZone().Apply();
        LogTimestampConfig.GetTimestamp().Offset.Should().Be(TimeSpan.Zero);
        ResetAll();
    }

    [Fact]
    public void UseLocalTimeZone_SetsTimestampToLocal()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseLocalTimeZone().Apply();
        var ts = LogTimestampConfig.GetTimestamp();
        ts.Offset.Should().Be(TimeZoneInfo.Local.GetUtcOffset(ts.DateTime));
        ResetAll();
    }

    [Fact]
    public void UseCustomTimezone_SetsTimestampToGivenZone()
    {
        ResetAll();
        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        GlobalConfigurator.Configure().UseCustomTimezone(tokyo).Apply();
        var ts = LogTimestampConfig.GetTimestamp();
        ts.Offset.Should().Be(tokyo.GetUtcOffset(DateTime.UtcNow));
        ResetAll();
    }

    [Fact]
    public void UseCustomTimezone_NullArg_Throws()
    {
        ResetAll();
        // UseCustomTimezone immediately calls ArgumentNullException.ThrowIfNull
        // before queuing an operation — so Configure() has been called but
        // the builder method throws. _isConfiguring remains true.
        Action act = () => GlobalConfigurator.Configure().UseCustomTimezone(null!);
        act.Should().Throw<ArgumentNullException>();
        ResetAll(); // clears both statics for the next test
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. JSON timestamp format
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UseJsonUnixTimestamp_SetsJsonModeToUnix()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseJsonUnixTimestamp().Apply();
        TimestampFormatConfig.JsonMode.Should().Be(JsonTimestampMode.Unix);
        ResetAll();
    }

    [Fact]
    public void UseJsonUnixMsTimestamp_SetsJsonModeToUnixMs()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseJsonUnixMsTimestamp().Apply();
        TimestampFormatConfig.JsonMode.Should().Be(JsonTimestampMode.UnixMs);
        ResetAll();
    }

    [Fact]
    public void UseJsonISO8601Timestamp_SetsJsonModeToISO8601()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseJsonISO8601Timestamp().Apply();
        TimestampFormatConfig.JsonMode.Should().Be(JsonTimestampMode.ISO8601);
        ResetAll();
    }

    [Fact]
    public void UseJsonCustomTimestamp_SetsJsonModeToCustomAndStoresFormat()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseJsonCustomTimestamp("yyyy/MM/dd").Apply();
        TimestampFormatConfig.JsonMode.Should().Be(JsonTimestampMode.Custom);
        TimestampFormatConfig.JsonCustomFormat.Should().Be("yyyy/MM/dd");
        ResetAll();
    }

    [Fact]
    public void UseJsonCustomTimestamp_NullOrWhiteSpace_Throws()
    {
        ResetAll();
        // ArgumentException thrown immediately from ArgumentException.ThrowIfNullOrWhiteSpace
        Action act = () => GlobalConfigurator.Configure().UseJsonCustomTimestamp("  ");
        act.Should().Throw<ArgumentException>();
        ResetAll();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Text timestamp format
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UseTextUnixTimestamp_SetsTextModeToUnix()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseTextUnixTimestamp().Apply();
        TimestampFormatConfig.TextMode.Should().Be(TextTimestampMode.Unix);
        ResetAll();
    }

    [Fact]
    public void UseTextUnixMsTimestamp_SetsTextModeToUnixMs()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseTextUnixMsTimestamp().Apply();
        TimestampFormatConfig.TextMode.Should().Be(TextTimestampMode.UnixMs);
        ResetAll();
    }

    [Fact]
    public void UseTextISO8601Timestamp_SetsTextModeToISO8601()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseTextISO8601Timestamp().Apply();
        TimestampFormatConfig.TextMode.Should().Be(TextTimestampMode.ISO8601);
        ResetAll();
    }

    [Fact]
    public void UseTextCustomTimestamp_SetsTextModeAndStoresFormat()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseTextCustomTimestamp("dd/MM/yyyy").Apply();
        TimestampFormatConfig.TextMode.Should().Be(TextTimestampMode.Custom);
        TimestampFormatConfig.TextCustomFormat.Should().Be("dd/MM/yyyy");
        ResetAll();
    }

    [Fact]
    public void UseTextCustomTimestamp_NullOrWhiteSpace_Throws()
    {
        ResetAll();
        Action act = () => GlobalConfigurator.Configure().UseTextCustomTimestamp("");
        act.Should().Throw<ArgumentException>();
        ResetAll();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Auto-destructuring
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EnableAutoDestructuring_SetsAutoDestructureCollectionsTrue()
    {
        ResetAll();
        GlobalConfigurator.Configure().EnableAutoDestructuring().Apply();
        DestructuringConfig.AutoDestructureCollections.Should().BeTrue();
        ResetAll();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. JSON serialization options
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PreserveChineseCharacters_ConfiguresNonEscapedChinese()
    {
        ResetAll();
        GlobalConfigurator.Configure().EnableUnsafeRelaxedJsonEscaping().Apply();
        JsonSerializationConfig.Options.Should().NotBeNull();
        ResetAll();
    }

    [Fact]
    public void EscapeChineseCharacters_CanBeChained()
    {
        ResetAll();
        Action act = () => GlobalConfigurator.Configure().DisableUnsafeRelaxedJsonEscaping().Apply();
        act.Should().NotThrow();
        ResetAll();
    }

    [Fact]
    public void UseIndentedJson_SetsWriteIndentedTrue()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseIndentedJson().Apply();
        JsonSerializationConfig.Options.WriteIndented.Should().BeTrue();
        // Restore after test
        JsonSerializationConfig.ConfigWriteIndented(false);
        ResetAll();
    }

    [Fact]
    public void UseCompactJson_SetsWriteIndentedFalse()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseCompactJson().Apply();
        JsonSerializationConfig.Options.WriteIndented.Should().BeFalse();
        ResetAll();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. UseJsonTypeInfoResolver
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UseJsonTypeInfoResolver_WithNullIJsonTypeInfoResolver_ThrowsArgumentNullException()
    {
        ResetAll();
        var builder = GlobalConfigurator.Configure();
        Action act = () => builder.UseJsonTypeInfoResolver((IJsonTypeInfoResolver)null!);
        act.Should().Throw<ArgumentNullException>();
        ResetAll();
    }

    [Fact]
    public void UseJsonTypeInfoResolver_WithNullJsonSerializerContext_ThrowsArgumentNullException()
    {
        ResetAll();
        var builder = GlobalConfigurator.Configure();
        Action act = () => builder.UseJsonTypeInfoResolver((JsonSerializerContext)null!);
        act.Should().Throw<ArgumentNullException>();
        ResetAll();
    }

    [Fact]
    public void UseJsonTypeInfoResolver_WithIJsonTypeInfoResolver_IsAddedToOptionsChain()
    {
        ResetAll();
        var resolver = new StubResolver();
        GlobalConfigurator.Configure()
            .UseJsonTypeInfoResolver(resolver)
            .Apply();
        JsonSerializationConfig.Options.TypeInfoResolverChain
            .Should().Contain(resolver);
        ResetAll();
    }

    [Fact]
    public void UseJsonTypeInfoResolver_WithJsonSerializerContext_IsAddedToOptionsChain()
    {
        ResetAll();
        var context = TestLogContext.Default;
        GlobalConfigurator.Configure()
            .UseJsonTypeInfoResolver(context)
            .Apply();
        JsonSerializationConfig.Options.TypeInfoResolverChain
            .Should().Contain(context);
        ResetAll();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. Chaining — multiple settings in one call chain
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Configure_MultipleSettings_AllApplied()
    {
        ResetAll();
        GlobalConfigurator.Configure()
            .UseUtcTimeZone()
            .UseJsonUnixTimestamp()
            .UseTextISO8601Timestamp()
            .EnableAutoDestructuring()
            .UseCompactJson()
            .Apply();

        LogTimestampConfig.GetTimestamp().Offset.Should().Be(TimeSpan.Zero);
        TimestampFormatConfig.JsonMode.Should().Be(JsonTimestampMode.Unix);
        TimestampFormatConfig.TextMode.Should().Be(TextTimestampMode.ISO8601);
        DestructuringConfig.AutoDestructureCollections.Should().BeTrue();
        JsonSerializationConfig.Options.WriteIndented.Should().BeFalse();
        ResetAll();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. ApplyDefaultIfNotConfigured — only applies once
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyDefaultIfNotConfigured_WhenNotConfigured_SetsDefaults()
    {
        ResetAll();
        GlobalConfigurator.ApplyDefaultIfNotConfigured();

        GlobalConfigLock.Configured.Should().BeTrue();
        TimestampFormatConfig.JsonMode.Should().Be(JsonTimestampMode.ISO8601);
        TimestampFormatConfig.TextMode.Should().Be(TextTimestampMode.Custom);
        ResetAll();
    }

    [Fact]
    public void ApplyDefaultIfNotConfigured_WhenAlreadyConfigured_IsNoop()
    {
        ResetAll();
        GlobalConfigurator.Configure().UseJsonUnixTimestamp().Apply();

        // Should not overwrite the custom Unix setting
        GlobalConfigurator.ApplyDefaultIfNotConfigured();

        TimestampFormatConfig.JsonMode.Should().Be(JsonTimestampMode.Unix);
        ResetAll();
    }

}

[JsonSerializable(typeof(string))]
internal partial class TestLogContext : JsonSerializerContext { }
