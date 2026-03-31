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

using System.Threading.Channels;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Target;
using Xunit;
using FluentAssertions;
using System.Text;

namespace Lunarium.Logging.Tests.Target;

public class ChannelTargetTests
{
    private static LogEntry MakeEntry(string msg = "hello") =>
        new LogEntry(
            loggerName: "Test",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Test"),
            timestamp: DateTimeOffset.UtcNow,
            logLevel: LogLevel.Info,
            message: msg,
            properties: [],
            context: "",
            contextBytes: default,
            scope: "",
            messageTemplate: LogParser.ParseMessage(msg));

    [Fact]
    public async Task ByteChannelTarget_ShouldWriteEncodedBytes()
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        var target = new ByteChannelTarget(channel.Writer, toJson: false, isColor: false);

        var entry = MakeEntry("byte test");
        target.Emit(entry);
        
        if (channel.Reader.TryRead(out var bytes))
        {
            var result = Encoding.UTF8.GetString(bytes);
            result.Should().Contain("byte test");
        }
        else
        {
            Assert.Fail("Failed to read from channel");
        }
    }

    [Fact]
    public async Task ByteChannelTarget_WithJson_ShouldWriteJsonBytes()
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        var target = new ByteChannelTarget(channel.Writer, toJson: true, isColor: false);
        
        var entry = MakeEntry("json byte test");
        target.Emit(entry);
        
        if (channel.Reader.TryRead(out var bytes))
        {
            var result = Encoding.UTF8.GetString(bytes);
            result.Should().Contain("\"OriginalMessage\":\"json byte test\"");
        }
        else
        {
            Assert.Fail("Failed to read from channel");
        }
    }

    [Fact]
    public void StringChannelTarget_ToJson_CanBeSet()
    {
        var channel = Channel.CreateUnbounded<string>();
        var target = new StringChannelTarget(channel.Writer, toJson: true, isColor: false);
        target.ToJson.Should().BeTrue();
    }

    [Fact]
    public void StringChannelTarget_ImplementsITextTarget()
    {
        var channel = Channel.CreateUnbounded<string>();
        var target = new StringChannelTarget(channel.Writer, toJson: false, isColor: false);
        target.Should().BeAssignableTo<ITextTarget>();
    }

    [Fact]
    public void ByteChannelTarget_ImplementsITextTarget()
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        var target = new ByteChannelTarget(channel.Writer, toJson: false, isColor: false);
        target.Should().BeAssignableTo<ITextTarget>();
    }

    [Fact]
    public void StringChannelTarget_TextOutputIncludeConfig_CanBeSet()
    {
        var channel = Channel.CreateUnbounded<string>();
        var config = new TextOutputIncludeConfig { IncludeLoggerName = false };
        var target = new StringChannelTarget(channel.Writer, toJson: false, isColor: false, textOutputIncludeConfig: config);
        target.GetTextOutputIncludeConfig().IncludeLoggerName.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UpdateTextOutputIncludeConfig paths
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void StringChannelTarget_UpdateTextOutputIncludeConfig_UpdatesValue()
    {
        var channel = Channel.CreateUnbounded<string>();
        var target = new StringChannelTarget(channel.Writer, toJson: false, isColor: false);

        target.UpdateTextOutputIncludeConfig(new TextOutputIncludeConfig { IncludeLevel = false });

        target.GetTextOutputIncludeConfig().IncludeLevel.Should().BeFalse();
    }

    [Fact]
    public void ByteChannelTarget_UpdateTextOutputIncludeConfig_UpdatesValue()
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        var target = new ByteChannelTarget(channel.Writer, toJson: false, isColor: false);

        target.UpdateTextOutputIncludeConfig(new TextOutputIncludeConfig { IncludeLevel = false });

        target.GetTextOutputIncludeConfig().IncludeLevel.Should().BeFalse();
    }

    [Fact]
    public void ByteChannelTarget_GetTextOutputIncludeConfig_ReturnsDefaultValues()
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        var target = new ByteChannelTarget(channel.Writer, toJson: false, isColor: false);

        var config = target.GetTextOutputIncludeConfig();

        config.IncludeTimestamp.Should().BeTrue();
        config.IncludeLevel.Should().BeTrue();
        config.IncludeLoggerName.Should().BeTrue();
        config.IncludeContext.Should().BeTrue();
    }

    [Fact]
    public async Task ByteChannelTarget_WithIsColor_ProducesAnsiOutput()
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        var target = new ByteChannelTarget(channel.Writer, toJson: false, isColor: true);

        var entry = MakeEntry("color test");
        target.Emit(entry);

        if (channel.Reader.TryRead(out var bytes))
        {
            var result = Encoding.UTF8.GetString(bytes);
            result.Should().Contain("\x1b[", "isColor=true should produce ANSI escape sequences");
        }
        else
        {
            Assert.Fail("Failed to read from channel");
        }
    }
}
