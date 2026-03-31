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
using Lunarium.Logging.Config.Models;
using Lunarium.Logging.Models;

namespace Lunarium.Logging.Tests.Config;

/// <summary>
/// Tests for FilterConfig record — default values and IgnoreFilterCase linkage.
/// </summary>
public class FilterConfigTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. Default values
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FilterConfig_DefaultValues_AreCorrect()
    {
        var cfg = new FilterConfig();
        cfg.LogMinLevel.Should().Be(LogLevel.Info);
        cfg.LogMaxLevel.Should().Be(LogLevel.Critical);
        cfg.ContextFilterIncludes.Should().BeNull();
        cfg.ContextFilterExcludes.Should().BeNull();
        cfg.IgnoreFilterCase.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. IgnoreFilterCase drives ComparisonType
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FilterConfig_IgnoreFilterCase_True_SetsOrdinalIgnoreCase()
    {
        var cfg = new FilterConfig { IgnoreFilterCase = true };
        cfg.ComparisonType.Should().Be(StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilterConfig_IgnoreFilterCase_False_SetsOrdinal()
    {
        var cfg = new FilterConfig { IgnoreFilterCase = false };
        cfg.ComparisonType.Should().Be(StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Custom level range
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FilterConfig_CustomLevelRange_SetCorrectly()
    {
        var cfg = new FilterConfig
        {
            LogMinLevel = LogLevel.Error,
            LogMaxLevel = LogLevel.Critical
        };
        cfg.LogMinLevel.Should().Be(LogLevel.Error);
        cfg.LogMaxLevel.Should().Be(LogLevel.Critical);
    }

}
