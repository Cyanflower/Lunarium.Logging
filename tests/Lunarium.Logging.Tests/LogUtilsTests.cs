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

using Lunarium.Logging.Config.Configurator;
using Lunarium.Logging.Utils;

namespace Lunarium.Logging.Tests;

/// <summary>
/// Tests for LogUtils — the public timestamp utility API.
/// Exercises GetLogSystemTimestamp() and GetLogSystemFormattedTimestamp()
/// across all four TextTimestampMode values by using the internal
/// TimestampFormatConfig.ConfigTextMode() / ConfigTextCustomFormat() methods.
/// </summary>
public class LogUtilsTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. GetLogSystemTimestamp — returns DateTimeOffset from configured zone
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetLogSystemTimestamp_ReturnsDateTimeOffset()
    {
        var before = DateTimeOffset.UtcNow;
        var ts = LogUtils.GetLogSystemTimestamp();
        var after = DateTimeOffset.UtcNow;
        // The returned timestamp must be within the measurement window
        ts.ToUnixTimeSeconds().Should().BeGreaterThanOrEqualTo(before.ToUnixTimeSeconds());
        ts.ToUnixTimeSeconds().Should().BeLessThanOrEqualTo(after.ToUnixTimeSeconds());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. GetLogSystemFormattedTimestamp — exercises all TextTimestampMode arms
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetLogSystemFormattedTimestamp_UnixMode_ReturnsLongString()
    {
        TimestampFormatConfig.ConfigTextMode(TextTimestampMode.Unix);
        var result = LogUtils.GetLogSystemFormattedTimestamp();
        // Unix seconds: should parse as a long
        result.Should().MatchRegex(@"^\d+$");
        long.Parse(result).Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetLogSystemFormattedTimestamp_UnixMsMode_ReturnsLongerNumber()
    {
        TimestampFormatConfig.ConfigTextMode(TextTimestampMode.UnixMs);
        var result = LogUtils.GetLogSystemFormattedTimestamp();
        result.Should().MatchRegex(@"^\d+$");
        // UnixMs have more digits than Unix seconds (>10^12 since epoch)
        var ms = long.Parse(result);
        ms.Should().BeGreaterThan(1_000_000_000_000L);
    }

    [Fact]
    public void GetLogSystemFormattedTimestamp_ISO8601Mode_ReturnsIsoString()
    {
        TimestampFormatConfig.ConfigTextMode(TextTimestampMode.ISO8601);
        var result = LogUtils.GetLogSystemFormattedTimestamp();
        DateTimeOffset.TryParse(result, out _).Should().BeTrue();
    }

    [Fact]
    public void GetLogSystemFormattedTimestamp_CustomMode_UsesCustomFormat()
    {
        TimestampFormatConfig.ConfigTextMode(TextTimestampMode.Custom);
        TimestampFormatConfig.ConfigTextCustomFormat("yyyy/MM/dd");
        var result = LogUtils.GetLogSystemFormattedTimestamp();
        // e.g. "2026/03/10"
        result.Should().MatchRegex(@"^\d{4}/\d{2}/\d{2}$");
    }
}
