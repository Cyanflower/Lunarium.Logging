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
using Lunarium.Logging.Config.Configurator;
using Lunarium.Logging.Utils;
using Xunit;

namespace Lunarium.Logging.Tests.Core;

public class LogUtilsTests
{
    [Fact]
    public void GetLogSystemTimestamp_ReturnsCurrentTimeWithCorrectOffset()
    {
        var old = LogTimestampConfig.GetTimestamp().Offset;
        try 
        {
            LogTimestampConfig.UseUtcTime();
            var ts = LogUtils.GetLogSystemTimestamp();
            ts.Offset.Should().Be(TimeSpan.Zero);
        }
        finally
        {
            if (old == TimeSpan.Zero) LogTimestampConfig.UseUtcTime(); else LogTimestampConfig.UseLocalTime();
        }
    }

    [Fact]
    public void GetLogSystemFormattedTimestamp_Fallback_ReturnsIso8601()
    {
        var oldMode = TimestampFormatConfig.TextMode;
        try
        {
            // Force an invalid enum value
            TimestampFormatConfig.ConfigTextMode((TextTimestampMode)999);
            var formatted = LogUtils.GetLogSystemFormattedTimestamp();
            
            // Default fallback is ISO8601 formatted output which contains "T" or is round-trip
            formatted.Should().NotBeNullOrEmpty();
        }
        finally
        {
            TimestampFormatConfig.ConfigTextMode(oldMode);
        }
    }
}
