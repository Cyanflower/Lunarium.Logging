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

using FluentAssertions;
using Lunarium.Logging.Filter;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;

namespace Lunarium.Logging.Tests.Filter;

/// <summary>
/// Tests for LoggerFilter — context prefix-based log filtering.
/// Uses a shared static LRU cache (2048 entries); all tests are idempotent.
/// </summary>
public class LoggerFilterTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static LogEntry MakeEntry(string context, LogLevel level = LogLevel.Info) =>
        new LogEntry(
            loggerName: "TestLogger",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("TestLogger"),
            timestamp: DateTimeOffset.UtcNow,
            logLevel: level,
            message: "test",
            properties: [],
            context: context,
            contextBytes: default,
            scope: "",
            messageTemplate: LogParser.EmptyMessageTemplate,
            exception: null);

    private static FilterConfig MakeConfig(
        LogLevel min = LogLevel.Debug,
        LogLevel max = LogLevel.Critical,
        List<string>? includes = null,
        List<string>? excludes = null,
        bool ignoreCase = false) =>
        new FilterConfig
        {
            LogMinLevel = min,
            LogMaxLevel = max,
            ContextFilterIncludes = includes,
            ContextFilterExcludes = excludes,
            IgnoreFilterCase = ignoreCase
        };

    private static bool ShouldEmit(LogEntry entry, FilterConfig cfg)
    {
        var filter = new LoggerFilter(cfg);
        return filter.ShouldEmit(entry);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Level filtering
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldEmit_LevelBelowMin_ReturnsFalse()
    {
        var entry = MakeEntry("App", LogLevel.Debug);
        var cfg = MakeConfig(min: LogLevel.Info);
        ShouldEmit(entry, cfg).Should().BeFalse();
    }

    [Fact]
    public void ShouldEmit_LevelAboveMax_ReturnsFalse()
    {
        var entry = MakeEntry("App", LogLevel.Critical);
        var cfg = MakeConfig(max: LogLevel.Warning);
        ShouldEmit(entry, cfg).Should().BeFalse();
    }

    [Fact]
    public void ShouldEmit_LevelAtMinBoundary_ReturnsTrue()
    {
        var entry = MakeEntry("App", LogLevel.Info);
        var cfg = MakeConfig(min: LogLevel.Info, max: LogLevel.Critical);
        ShouldEmit(entry, cfg).Should().BeTrue();
    }

    [Fact]
    public void ShouldEmit_LevelAtMaxBoundary_ReturnsTrue()
    {
        var entry = MakeEntry("App", LogLevel.Critical);
        var cfg = MakeConfig(min: LogLevel.Debug, max: LogLevel.Critical);
        ShouldEmit(entry, cfg).Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. No filter rules → all contexts pass
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("App")]
    [InlineData("App.Service.Module")]
    public void ShouldEmit_NoFilterRules_AlwaysTrue(string context)
    {
        var entry = MakeEntry(context);
        var cfg = MakeConfig();  // no includes, no excludes
        ShouldEmit(entry, cfg).Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Include rules
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldEmit_IncludePrefix_ContextMatches_ReturnsTrue()
    {
        var entry = MakeEntry("App.Service");
        var cfg = MakeConfig(includes: ["App"]);
        ShouldEmit(entry, cfg).Should().BeTrue();
    }

    [Fact]
    public void ShouldEmit_IncludePrefix_ContextExactMatch_ReturnsTrue()
    {
        var entry = MakeEntry("App");
        var cfg = MakeConfig(includes: ["App"]);
        ShouldEmit(entry, cfg).Should().BeTrue();
    }

    [Fact]
    public void ShouldEmit_IncludePrefix_ContextNoMatch_ReturnsFalse()
    {
        var entry = MakeEntry("Other.Module");
        var cfg = MakeConfig(includes: ["App"]);
        ShouldEmit(entry, cfg).Should().BeFalse();
    }

    [Fact]
    public void ShouldEmit_IncludePrefix_EmptyContext_ReturnsFalse()
    {
        var entry = MakeEntry("");
        var cfg = MakeConfig(includes: ["App"]);
        ShouldEmit(entry, cfg).Should().BeFalse();
    }

    [Fact]
    public void ShouldEmit_MultipleIncludes_AnyMatchSuffices()
    {
        var entry = MakeEntry("Service.B");
        var cfg = MakeConfig(includes: ["App", "Service"]);
        ShouldEmit(entry, cfg).Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Exclude rules
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldEmit_ExcludePrefix_ContextMatches_ReturnsFalse()
    {
        var entry = MakeEntry("App.Internal");
        var cfg = MakeConfig(excludes: ["App.Internal"]);
        ShouldEmit(entry, cfg).Should().BeFalse();
    }

    [Fact]
    public void ShouldEmit_ExcludePrefix_ContextNoMatch_ReturnsTrue()
    {
        var entry = MakeEntry("App.Public");
        var cfg = MakeConfig(excludes: ["App.Internal"]);
        ShouldEmit(entry, cfg).Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Include + Exclude combination
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldEmit_IncludeAndExclude_MatchesIncludeButNotExclude_ReturnsTrue()
    {
        var entry = MakeEntry("App.C");
        var cfg = MakeConfig(includes: ["App"], excludes: ["App.B"]);
        ShouldEmit(entry, cfg).Should().BeTrue();
    }

    [Fact]
    public void ShouldEmit_IncludeAndExclude_MatchesBoth_ReturnsFalse()
    {
        var entry = MakeEntry("App.B.Deep");
        var cfg = MakeConfig(includes: ["App"], excludes: ["App.B"]);
        ShouldEmit(entry, cfg).Should().BeFalse();
    }

    [Fact]
    public void ShouldEmit_IncludeAndExclude_MatchesNeitherInclude_ReturnsFalse()
    {
        var entry = MakeEntry("Other");
        var cfg = MakeConfig(includes: ["App"], excludes: ["App.B"]);
        ShouldEmit(entry, cfg).Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Case sensitivity
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldEmit_IgnoreCaseTrue_CaseInsensitiveMatch()
    {
        var entry = MakeEntry("APP.Module");
        var cfg = MakeConfig(includes: ["app"], ignoreCase: true);
        ShouldEmit(entry, cfg).Should().BeTrue();
    }

    [Fact]
    public void ShouldEmit_IgnoreCaseFalse_CaseSensitiveMismatch()
    {
        var entry = MakeEntry("APP.Module");
        var cfg = MakeConfig(includes: ["app"], ignoreCase: false);
        ShouldEmit(entry, cfg).Should().BeFalse();
    }

    [Fact]
    public void ShouldEmit_IgnoreCaseFalse_CaseSensitiveMatch()
    {
        var entry = MakeEntry("App.Module");
        var cfg = MakeConfig(includes: ["App"], ignoreCase: false);
        ShouldEmit(entry, cfg).Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Cache consistency — same input always produces same result
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldEmit_SameContext_CalledTwice_ConsistentResult()
    {
        // Second call hits the cache; result should be identical to first
        const string context = "CacheConsistency.Test.Module";
        var entry = MakeEntry(context);
        var cfg = MakeConfig(includes: ["CacheConsistency"]);
        var filter = new LoggerFilter(cfg);
        var first = filter.ShouldEmit(entry);
        var second = filter.ShouldEmit(entry);
        second.Should().Be(first);
    }
    // ─────────────────────────────────────────────────────────────────────────
    // 8. Cache eviction — when cache exceeds 2048 entries it is cleared
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldEmit_CacheEviction_AfterExceedingLimit_StillFiltersCorrectly()
    {
        // Fill the cache beyond the 2048 limit to trigger the Clear() path,
        // then verify that a new lookup still produces the correct result.
        var cfg = MakeConfig(includes: ["Prefix"]);
        var filter = new LoggerFilter(cfg);

        // Each unique context is a new cache entry
        for (int i = 0; i < 2050; i++)
        {
            var entry = MakeEntry($"Prefix.Module.{i}");
            filter.ShouldEmit(entry); // fills cache
        }

        // After eviction, a fresh lookup should still return the correct answer
        var freshEntry = MakeEntry("Prefix.AfterEviction");
        filter.ShouldEmit(freshEntry).Should().BeTrue();

        var outEntry = MakeEntry("Other.AfterEviction");
        filter.ShouldEmit(outEntry).Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. GetFilterConfig and UpdateConfig
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LoggerFilter_GetFilterConfig_ReturnsConstructorConfig()
    {
        var cfg = new FilterConfig { LogMinLevel = LogLevel.Warning, LogMaxLevel = LogLevel.Critical };
        var filter = new LoggerFilter(cfg);

        var returned = filter.GetFilterConfig();

        returned.Should().BeSameAs(cfg);
        returned.LogMinLevel.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void LoggerFilter_UpdateConfig_ChangesFilterBehavior()
    {
        // Start with include-only filter for "App"
        var initialCfg = MakeConfig(includes: ["App"]);
        var filter = new LoggerFilter(initialCfg);

        // Verify initial config blocks a non-matching context
        filter.ShouldEmit(MakeEntry("Other.CachedBefore")).Should().BeFalse("initial config blocks 'Other'");

        // Switch to permissive filter (no include/exclude rules)
        // Note: UpdateConfig only clears the cache when _cacheCount >= 2048.
        // New entries added after UpdateConfig will use the new config.
        filter.UpdateConfig(new FilterConfig());

        // Verify GetFilterConfig returns the new config
        filter.GetFilterConfig().ContextFilterIncludes.Should().BeNull("new config has no include rules");

        // A context that was NOT cached before UpdateConfig should use the new (permissive) rules
        filter.ShouldEmit(MakeEntry("AnyFreshContext.AfterUpdate")).Should().BeTrue("new config allows everything");
    }
}
