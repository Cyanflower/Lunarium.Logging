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
using Lunarium.Logging.Config;
using Lunarium.Logging.Core;

namespace Lunarium.Logging.Tests.Models;

/// Tests for Sink, ConsoleSinkConfig, FileSinkConfig.
/// </summary>
public class LogConfigTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. Sink — constructors and Deconstruct overloads
    // ─────────────────────────────────────────────────────────────────────────

    private static ILogTarget MakeSink()
        => new StringChannelTarget(System.Threading.Channels.Channel.CreateUnbounded<string>().Writer, toJson: false, isColor: false);

    [Fact]
    public void Sink_Ctor_WithConfig_StoresAll()
    {
        var target = MakeSink();
        var cfg = new FilterConfig { LogMinLevel = LogLevel.Warning };
        var s = new Sink(target, cfg);
        s.Target.Should().BeSameAs(target);
        s.LoggerFilter.GetFilterConfig().LogMinLevel.Should().Be(LogLevel.Warning);
        s.LoggerFilter.Should().NotBeNull();
    }

    [Fact]
    public void Sink_Ctor_WithoutConfig_UsesDefaults()
    {
        var target = MakeSink();
        var s = new Sink(target, (FilterConfig?)null);
        s.Target.Should().BeSameAs(target);
        s.LoggerFilter.GetFilterConfig().Should().NotBeNull();
        s.LoggerFilter.Should().NotBeNull();
    }

    [Fact]
    public void Sink_Deconstruct_TwoOut()
    {
        var target = MakeSink();
        var cfg = new FilterConfig();
        var sink = new Sink(target, cfg);
        var t = sink.Target;
        var c = sink.LoggerFilter.GetFilterConfig();
        t.Should().BeSameAs(target);
        c.Should().Be(cfg);
    }

    [Fact]
    public void Sink_Deconstruct_ThreeOut()
    {
        var target = MakeSink();
        var cfg = new FilterConfig();
        var sink = new Sink(target, cfg);
        var t = sink.Target;
        var c = sink.LoggerFilter.GetFilterConfig();
        var f = sink.LoggerFilter;
        t.Should().BeSameAs(target);
        c.Should().Be(cfg);
        f.Should().NotBeNull();
    }

    [Fact]
    public void Sink_Ctor_WithExplicitName_StoresName()
    {
        var target = MakeSink();
        var sink = new Sink(target, "analytics-sink");
        sink.Name.Should().Be("analytics-sink");
    }

    [Fact]
    public void Sink_Ctor_WithEmptyName_GeneratesGuidName()
    {
        var target = MakeSink();
        var sink = new Sink(target, (FilterConfig?)null, "");
        Guid.TryParse(sink.Name, out _).Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. ConsoleSinkConfig — public record with optional FilterConfig
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ConsoleSinkConfig_DefaultConstruction()
    {
        var cfg = new ConsoleSinkConfig();
        cfg.FilterConfig.Should().BeNull();
    }

    [Fact]
    public void ConsoleSinkConfig_WithOutputConfig()
    {
        var outCfg = new FilterConfig { LogMinLevel = LogLevel.Error };
        var cfg = new ConsoleSinkConfig { FilterConfig = outCfg };
        cfg.FilterConfig.Should().NotBeNull();
        cfg.FilterConfig!.LogMinLevel.Should().Be(LogLevel.Error);
    }

    [Fact]
    public void ConsoleSinkConfig_IsISinkConfig()
    {
        var cfg = new ConsoleSinkConfig();
        cfg.Should().BeAssignableTo<ISinkConfig>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. FileSinkConfig — record with required LogFilePath and defaults
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileSinkConfig_DefaultValues()
    {
        var cfg = new FileSinkConfig { LogFilePath = "logs/app.log" };
        cfg.LogFilePath.Should().Be("logs/app.log");
        cfg.MaxFileSizeMB.Should().Be(0);
        cfg.RotateOnNewDay.Should().BeFalse();
        cfg.MaxFile.Should().Be(0);
        cfg.FilterConfig.Should().BeNull();
    }

    [Fact]
    public void FileSinkConfig_AllPropertiesSet()
    {
        var outCfg = new FilterConfig { LogMinLevel = LogLevel.Info };
        var cfg = new FileSinkConfig
        {
            LogFilePath = "logs/test.log",
            MaxFileSizeMB = 50.5,
            RotateOnNewDay = true,
            MaxFile = 7,
            FilterConfig = outCfg
        };
        cfg.LogFilePath.Should().Be("logs/test.log");
        cfg.MaxFileSizeMB.Should().Be(50.5);
        cfg.RotateOnNewDay.Should().BeTrue();
        cfg.MaxFile.Should().Be(7);
        cfg.FilterConfig.Should().BeSameAs(outCfg);
    }

    [Fact]
    public void FileSinkConfig_IsISinkConfig()
    {
        var cfg = new FileSinkConfig { LogFilePath = "x" };
        cfg.Should().BeAssignableTo<ISinkConfig>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. ISinkConfig.CreateTarget() — ToJson propagation (IJsonTextTarget)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ConsoleSinkConfig_CreateTarget_WithToJsonTrue_ReturnsTargetWithToJsonTrue()
    {
        var cfg = new ConsoleSinkConfig { ToJson = true };
        var target = (ConsoleTarget)cfg.CreateTarget();
        target.ToJson.Should().BeTrue();
    }

    [Fact]
    public void ConsoleSinkConfig_CreateTarget_WithToJsonFalse_ReturnsTargetWithToJsonFalse()
    {
        var cfg = new ConsoleSinkConfig { ToJson = false };
        var target = (ConsoleTarget)cfg.CreateTarget();
        target.ToJson.Should().BeFalse();
    }

    [Fact]
    public void ConsoleSinkConfig_CreateTarget_WithToJsonNull_DefaultsToFalse()
    {
        var cfg = new ConsoleSinkConfig(); // ToJson = null (default)
        var target = (ConsoleTarget)cfg.CreateTarget();
        target.ToJson.Should().BeFalse(); // null ?? false
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. ISinkConfig.CreateTarget() — IsColor propagation + FilterConfig storage
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ConsoleSinkConfig_CreateTarget_WithIsColorFalse_ReturnsTargetWithIsColorFalse()
    {
        var cfg = new ConsoleSinkConfig { IsColor = false };
        var target = (ConsoleTarget)cfg.CreateTarget();
        target.IsColor.Should().BeFalse();
    }

    [Fact]
    public void ConsoleSinkConfig_CreateTarget_WithIsColorTrue_ReturnsTargetWithIsColorTrue()
    {
        var cfg = new ConsoleSinkConfig { IsColor = true };
        var target = (ConsoleTarget)cfg.CreateTarget();
        target.IsColor.Should().BeTrue();
    }

    [Fact]
    public void ConsoleSinkConfig_CreateTarget_WithIsColorNull_DefaultsToTrue()
    {
        var cfg = new ConsoleSinkConfig(); // IsColor = null (default)
        var target = (ConsoleTarget)cfg.CreateTarget();
        target.IsColor.Should().BeTrue(); // null ?? true — matches ConsoleTarget's natural default
    }

    [Fact]
    public void Sink_WithNonITextTargetAndFilterConfig_DoesNotThrow()
    {
        // Substitute.For<ILogTarget>() does not implement ITextTarget — Sink should not throw
        var target = Substitute.For<ILogTarget>();
        var cfg = new FilterConfig();
        Action act = () => _ = new Sink(target, cfg);
        act.Should().NotThrow();
    }

    [Fact]
    public void ConsoleTarget_WithTextOutputIncludeConfig_StoresConfig()
    {
        var customConfig = new TextOutputIncludeConfig { IncludeLoggerName = false, IncludeTimestamp = true };
        var target = new ConsoleTarget(textOutputIncludeConfig: customConfig);
        target.GetTextOutputIncludeConfig().IncludeLoggerName.Should().BeFalse();
        target.GetTextOutputIncludeConfig().IncludeTimestamp.Should().BeTrue();
    }

    [Fact]
    public void ConsoleTarget_DefaultTextOutputIncludeConfig_AllEnabled()
    {
        var target = new ConsoleTarget();
        target.GetTextOutputIncludeConfig().IncludeLoggerName.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5b. FileSinkConfig.CreateTarget() — ToJson/IsColor propagation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileSinkConfig_CreateTarget_WithToJsonTrue_ReturnsTargetWithToJsonTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lunarium-test-{Guid.NewGuid():N}.log");
        var cfg = new FileSinkConfig { LogFilePath = path, ToJson = true };
        using var target = (FileTarget)cfg.CreateTarget();
        target.ToJson.Should().BeTrue();
    }

    [Fact]
    public void FileSinkConfig_CreateTarget_WithIsColorTrue_ReturnsTargetWithIsColorTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lunarium-test-{Guid.NewGuid():N}.log");
        var cfg = new FileSinkConfig { LogFilePath = path, IsColor = true };
        using var target = (FileTarget)cfg.CreateTarget();
        target.IsColor.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Sink.Emit — filter pass / block
    // ─────────────────────────────────────────────────────────────────────────

    private static LogEntry MakeLogEntry(LogLevel level = LogLevel.Info, string msg = "hello")
        => new LogEntry(
            loggerName: "Test",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Test"),
            timestamp: DateTimeOffset.UtcNow,
            logLevel: level,
            message: msg,
            properties: [],
            context: "",
            contextBytes: default,
            scope: "",
            messageTemplate: LogParser.EmptyMessageTemplate);

    [Fact]
    public void Sink_Emit_WhenFilterPasses_CallsTargetEmit()
    {
        var mockTarget = Substitute.For<ILogTarget>();
        var cfg = new FilterConfig { LogMinLevel = LogLevel.Debug };
        var sink = new Sink(mockTarget, cfg);
        sink.Emit(MakeLogEntry(LogLevel.Info));
        mockTarget.Received(1).Emit(Arg.Any<LogEntry>());
    }

    [Fact]
    public void Sink_Emit_WhenFilterBlocks_DoesNotCallTargetEmit()
    {
        var mockTarget = Substitute.For<ILogTarget>();
        var cfg = new FilterConfig { LogMinLevel = LogLevel.Warning };
        var sink = new Sink(mockTarget, cfg);
        sink.Emit(MakeLogEntry(LogLevel.Debug));
        mockTarget.DidNotReceive().Emit(Arg.Any<LogEntry>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Sink — second constructor (name-only) and UpdateFilterConfig
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sink_Ctor_WithNameOnly_UsesDefaultFilter()
    {
        var target = MakeSink();
        var sink = new Sink(target, "my-named-sink");

        sink.Name.Should().Be("my-named-sink");
        sink.LoggerFilter.Should().NotBeNull();
        // Default filter allows Info level
        sink.LoggerFilter.GetFilterConfig().LogMinLevel.Should().Be(LogLevel.Info);
    }

    [Fact]
    public void Sink_UpdateSinkConfig_ChangesFilterResult()
    {
        var mockTarget = Substitute.For<ILogTarget>();
        var restrictiveCfg = new FilterConfig { LogMinLevel = LogLevel.Warning };
        var sink = new Sink(mockTarget, restrictiveCfg);

        // Debug is below Warning — should be blocked
        sink.Emit(MakeLogEntry(LogLevel.Debug));
        mockTarget.DidNotReceive().Emit(Arg.Any<LogEntry>());

        // Relax the filter to allow Debug
        sink.UpdateSinkConfig(new ConsoleSinkConfig { FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Debug } });

        // Now Debug should pass
        sink.Emit(MakeLogEntry(LogLevel.Debug));
        mockTarget.Received(1).Emit(Arg.Any<LogEntry>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. Default construction after removing 'required' keyword
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileSinkConfig_DefaultConstruction_LogFilePathIsEmpty()
    {
        var cfg = new FileSinkConfig();
        cfg.LogFilePath.Should().Be(string.Empty);
    }

    [Fact]
    public void LoggerConfig_DefaultConstruction_LoggerNameIsEmpty()
    {
        var cfg = new LoggerConfig();
        cfg.LoggerName.Should().Be(string.Empty);
        cfg.ConsoleSinks.Should().BeEmpty();
        cfg.FileSinks.Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. Sink — Enabled flag, EnableSink(), DisableSink()
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sink_Emit_WhenDisabled_DoesNotCallTarget()
    {
        var mockTarget = Substitute.For<ILogTarget>();
        var sink = new Sink(mockTarget, new FilterConfig { LogMinLevel = LogLevel.Debug });
        sink.DisableSink(); // Enabled = false

        sink.Emit(MakeLogEntry(LogLevel.Info));

        mockTarget.DidNotReceive().Emit(Arg.Any<LogEntry>());
    }

    [Fact]
    public void Sink_DisableSink_SetsEnabledFalse()
    {
        var sink = new Sink(Substitute.For<ILogTarget>(), new FilterConfig());
        sink.DisableSink();
        sink.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Sink_EnableSink_SetsEnabledTrue()
    {
        var sink = new Sink(Substitute.For<ILogTarget>(), new FilterConfig());
        sink.DisableSink();
        sink.EnableSink();
        sink.Enabled.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. Sink.UpdateSinkConfig — null FilterConfig resets to default
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sink_UpdateSinkConfig_NullFilterConfig_ResetsToDefaultFilter()
    {
        var sink = new Sink(Substitute.For<ILogTarget>(),
            new FilterConfig { LogMinLevel = LogLevel.Error });

        // ConsoleSinkConfig.FilterConfig defaults to null
        sink.UpdateSinkConfig(new ConsoleSinkConfig());

        // After update the filter should revert to the default (Info min level)
        sink.LoggerFilter.GetFilterConfig().LogMinLevel.Should().Be(LogLevel.Info);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. Sink.UpdateSinkConfig — Enabled=false disables sink
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sink_UpdateSinkConfig_EnabledFalse_DisablesSink()
    {
        var sink = new Sink(Substitute.For<ILogTarget>(), new FilterConfig());
        sink.UpdateSinkConfig(new ConsoleSinkConfig { Enabled = false });
        sink.Enabled.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12. Sink.UpdateSinkConfig — ITextTarget, TextOutput=null resets to default
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sink_UpdateSinkConfig_ITextTarget_NullTextOutput_ResetsToDefaultConfig()
    {
        var ch = System.Threading.Channels.Channel.CreateUnbounded<string>();
        var textTarget = new StringChannelTarget(ch.Writer, toJson: false, isColor: false,
            textOutputIncludeConfig: new TextOutputIncludeConfig { IncludeTimestamp = false });

        var sink = new Sink(textTarget, new FilterConfig());

        // ConsoleSinkConfig.TextOutput defaults to null → resets to new TextOutputIncludeConfig()
        sink.UpdateSinkConfig(new ConsoleSinkConfig());

        textTarget.GetTextOutputIncludeConfig().IncludeTimestamp.Should()
            .BeTrue("null TextOutput resets to default where IncludeTimestamp=true");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 13. Sink.UpdateSinkConfig — ITextTarget, non-null TextOutput applies config
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sink_UpdateSinkConfig_ITextTarget_WithTextOutput_UpdatesTargetConfig()
    {
        var ch = System.Threading.Channels.Channel.CreateUnbounded<string>();
        var textTarget = new StringChannelTarget(ch.Writer, toJson: false, isColor: false);
        var sink = new Sink(textTarget, new FilterConfig());

        var customOutput = new TextOutputIncludeConfig
        {
            IncludeTimestamp = false,
            IncludeLevel = false
        };
        sink.UpdateSinkConfig(new ConsoleSinkConfig { TextOutput = customOutput });

        textTarget.GetTextOutputIncludeConfig().IncludeTimestamp.Should().BeFalse();
        textTarget.GetTextOutputIncludeConfig().IncludeLevel.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 14. Sink.Dispose — delegates to Target.Dispose()
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sink_Dispose_CallsTargetDispose()
    {
        var mockTarget = Substitute.For<ILogTarget>();
        var sink = new Sink(mockTarget, new FilterConfig());

        sink.Dispose();

        mockTarget.Received(1).Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 15. Sink second constructor — null name generates a Guid-based name
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sink_SecondCtor_NullName_GeneratesGuidName()
    {
        var target = MakeSink();
        var sink = new Sink(target, (string?)null);
        Guid.TryParse(sink.Name, out _).Should().BeTrue();
    }
}
