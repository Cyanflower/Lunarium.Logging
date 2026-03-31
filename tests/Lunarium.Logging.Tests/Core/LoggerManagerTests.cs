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
using Lunarium.Logging.Config.Models;
using Lunarium.Logging.Models;
using Lunarium.Logging.Target;

namespace Lunarium.Logging.Tests.Core;

/// <summary>
/// Tests for LoggerManager public API:
///   GetLoggerList / UpdateSinkConfig / UpdateLoggerConfig / UpdateAllLoggerConfig
///
/// All loggers use GUID-based unique names to avoid cross-test interference with the
/// static dictionary. Tests use LogEntryChannelTarget so assertions can inspect
/// LogLevel directly without string parsing.
/// </summary>
public class LoggerManagerTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string UniqueName(string prefix) =>
        $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>
    /// Builds a named Logger whose single sink (named <paramref name="sinkName"/>)
    /// forwards entries to a returned Channel without any transformation.
    /// </summary>
    private static (ILogger logger, Channel<LogEntry> ch) BuildNamed(
        string name, string sinkName, LogLevel min = LogLevel.Debug)
    {
        var ch = Channel.CreateUnbounded<LogEntry>();
        var logger = new LoggerBuilder()
            .SetLoggerName(name)
            .AddSink(
                new LogEntryChannelTarget(ch.Writer),
                new FilterConfig { LogMinLevel = min, LogMaxLevel = LogLevel.Critical },
                sinkName)
            .Build();
        return (logger, ch);
    }

    /// <summary>Reads one entry from the channel, returns null on timeout.</summary>
    private static async Task<LogEntry?> ReadOneAsync(Channel<LogEntry> ch, int ms = 2000)
    {
        using var cts = new CancellationTokenSource(ms);
        try { return await ch.Reader.ReadAsync(cts.Token); }
        catch (OperationCanceledException) { return null; }
    }

    /// <summary>
    /// Reads entries until one with the expected level is found, or until timeout.
    /// Skips entries with other levels.
    /// </summary>
    private static async Task<LogEntry?> ReadFirstMatchAsync(
        Channel<LogEntry> ch, LogLevel level, int ms = 3000)
    {
        using var cts = new CancellationTokenSource(ms);
        try
        {
            await foreach (var e in ch.Reader.ReadAllAsync(cts.Token))
                if (e.LogLevel == level) return e;
        }
        catch (OperationCanceledException) { }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. GetLoggerList
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLoggerList_ContainsRecentlyBuiltLogger()
    {
        var name = UniqueName("LM-List");
        var (logger, _) = BuildNamed(name, "s");

        LoggerManager.GetLoggerList().Should().Contain(name);

        await logger.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. UpdateSinkConfig — logger exists, filter changes
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSinkConfig_LoggerExists_UpdatesFilterBehavior()
    {
        var name = UniqueName("LM-USC");
        const string sinkName = "sink-usc";
        var (logger, ch) = BuildNamed(name, sinkName, LogLevel.Debug);

        // Confirm Debug passes before update
        logger.Log(LogLevel.Debug, message: "warmup");
        (await ReadOneAsync(ch)).Should().NotBeNull("warmup should arrive with Debug filter");

        // Restrict sink to Error+
        LoggerManager.UpdateSinkConfig(name, sinkName,
            new ConsoleSinkConfig { FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Error } });

        // Debug must be blocked; Error must pass
        logger.Log(LogLevel.Debug, message: "debug-blocked");
        logger.Log(LogLevel.Error, message: "error-passed");

        var errorEntry = await ReadFirstMatchAsync(ch, LogLevel.Error);
        errorEntry.Should().NotBeNull("Error should pass after UpdateSinkConfig");
        errorEntry!.Message.Should().Contain("error-passed");

        await logger.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. UpdateSinkConfig — logger not found
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateSinkConfig_LoggerNotFound_DoesNotThrow()
    {
        Action act = () => LoggerManager.UpdateSinkConfig(
            "NonExistentLogger-" + Guid.NewGuid(), "any-sink", new ConsoleSinkConfig());
        act.Should().NotThrow();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. UpdateSinkConfig — sink name not found in logger
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSinkConfig_SinkNotFound_DoesNotThrow()
    {
        var name = UniqueName("LM-SinkNF");
        var (logger, _) = BuildNamed(name, "real-sink");

        Action act = () => LoggerManager.UpdateSinkConfig(name, "nonexistent-sink",
            new ConsoleSinkConfig());
        act.Should().NotThrow();

        await logger.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. UpdateLoggerConfig — logger exists, sink filter updated
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateLoggerConfig_LoggerExists_UpdatesSinkFilter()
    {
        var name = UniqueName("LM-ULC");
        const string sinkName = "sink-ulc";
        var (logger, ch) = BuildNamed(name, sinkName, LogLevel.Debug);

        // Warm-up: ensure queue is drained
        logger.Log(LogLevel.Debug, message: "warmup");
        (await ReadOneAsync(ch)).Should().NotBeNull();

        // Update via LoggerConfig — restrict to Error+
        LoggerManager.UpdateLoggerConfig(new LoggerConfig
        {
            LoggerName = name,
            ConsoleSinks =
            {
                [sinkName] = new ConsoleSinkConfig
                {
                    FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Error }
                }
            }
        });

        logger.Log(LogLevel.Debug, message: "debug-blocked");
        logger.Log(LogLevel.Error, message: "error-passed");

        var errorEntry = await ReadFirstMatchAsync(ch, LogLevel.Error);
        errorEntry.Should().NotBeNull("Error should pass after UpdateLoggerConfig");

        await logger.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. UpdateLoggerConfig — logger not found
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateLoggerConfig_LoggerNotFound_DoesNotThrow()
    {
        Action act = () => LoggerManager.UpdateLoggerConfig(new LoggerConfig
        {
            LoggerName = "NonExistentLogger-" + Guid.NewGuid()
        });
        act.Should().NotThrow();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. UpdateLoggerConfig — sink not listed in config → disabled + disposed
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateLoggerConfig_SinkNotInConfig_DisablesAndDisposesSink()
    {
        var name = UniqueName("LM-Disable");
        const string sinkName = "sink-disable";
        var (logger, ch) = BuildNamed(name, sinkName);

        // Warm-up: confirm queue is processed
        logger.Log(LogLevel.Info, message: "warmup");
        (await ReadOneAsync(ch)).Should().NotBeNull();

        // Empty config: "sink-disable" is not listed → Logger disables + disposes it,
        // which calls TryComplete() on the channel writer
        LoggerManager.UpdateLoggerConfig(new LoggerConfig { LoggerName = name });

        // Channel is now completed — ReadAllAsync terminates with 0 remaining items
        var remaining = new List<LogEntry>();
        using var cts = new CancellationTokenSource(300);
        try
        {
            await foreach (var e in ch.Reader.ReadAllAsync(cts.Token))
                remaining.Add(e);
        }
        catch (OperationCanceledException) { }

        remaining.Should().BeEmpty("sink was disabled and its channel writer was completed");

        await logger.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. UpdateAllLoggerConfig — updates multiple loggers in one call
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAllLoggerConfig_UpdatesMultipleLoggers()
    {
        var name1 = UniqueName("LM-All1");
        var name2 = UniqueName("LM-All2");
        const string sinkName = "sink-all";
        var (logger1, ch1) = BuildNamed(name1, sinkName, LogLevel.Debug);
        var (logger2, ch2) = BuildNamed(name2, sinkName, LogLevel.Debug);

        // Warm-up both
        logger1.Log(LogLevel.Debug, message: "w1");
        logger2.Log(LogLevel.Debug, message: "w2");
        (await ReadOneAsync(ch1)).Should().NotBeNull();
        (await ReadOneAsync(ch2)).Should().NotBeNull();

        // Restrict both to Error+ via UpdateAllLoggerConfig
        LoggerManager.UpdateAllLoggerConfig(new List<LoggerConfig>
        {
            new()
            {
                LoggerName = name1,
                ConsoleSinks =
                {
                    [sinkName] = new ConsoleSinkConfig
                    {
                        FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Error }
                    }
                }
            },
            new()
            {
                LoggerName = name2,
                ConsoleSinks =
                {
                    [sinkName] = new ConsoleSinkConfig
                    {
                        FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Error }
                    }
                }
            }
        });

        logger1.Log(LogLevel.Debug, message: "blocked1");
        logger1.Log(LogLevel.Error, message: "passed1");
        logger2.Log(LogLevel.Debug, message: "blocked2");
        logger2.Log(LogLevel.Error, message: "passed2");

        var e1 = await ReadFirstMatchAsync(ch1, LogLevel.Error);
        var e2 = await ReadFirstMatchAsync(ch2, LogLevel.Error);

        e1.Should().NotBeNull("logger1 Error should pass after UpdateAllLoggerConfig");
        e2.Should().NotBeNull("logger2 Error should pass after UpdateAllLoggerConfig");

        await logger1.DisposeAsync();
        await logger2.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. UpdateLoggerConfig — FileSinks branch: sink matched via FileSinks dict
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateLoggerConfig_FileSinkInConfig_UpdatesSinkFilter()
    {
        var name = UniqueName("LM-FileSink");
        const string sinkName = "sink-file";
        var (logger, ch) = BuildNamed(name, sinkName, LogLevel.Debug);

        // Warm-up
        logger.Log(LogLevel.Debug, message: "warmup");
        (await ReadOneAsync(ch)).Should().NotBeNull();

        // Update via FileSinks (not ConsoleSinks) to exercise that branch in Logger.UpdateLoggerConfig
        LoggerManager.UpdateLoggerConfig(new LoggerConfig
        {
            LoggerName = name,
            FileSinks =
            {
                [sinkName] = new FileSinkConfig
                {
                    FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Error }
                }
            }
        });

        logger.Log(LogLevel.Debug, message: "debug-blocked");
        logger.Log(LogLevel.Error, message: "error-passed");

        var errorEntry = await ReadFirstMatchAsync(ch, LogLevel.Error);
        errorEntry.Should().NotBeNull("FileSinks path in UpdateLoggerConfig should update the sink filter");

        await logger.DisposeAsync();
    }
}
