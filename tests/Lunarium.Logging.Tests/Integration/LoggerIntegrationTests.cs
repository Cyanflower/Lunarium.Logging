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
using FluentAssertions;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Target;
using Lunarium.Logging.Config.Models;

namespace Lunarium.Logging.Tests.Integration;

// ─────────────────────────────────────────────────────────────────────────────
// Collection definition — all tests in this collection share ONE fixture and
// run SEQUENTIALLY (no parallelism).  This is essential because tests in this
// collection share a single Logger fixture and rely on channel state ordering.
// ─────────────────────────────────────────────────────────────────────────────
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<LoggerFixture> { }

/// <summary>
/// Shared fixture — constructed ONCE per test process by xUnit's
/// ICollectionFixture mechanism.
/// </summary>
public class LoggerFixture : IAsyncDisposable
{
    // ── Stored as ILogger so default interface methods (Info/Debug/etc.) work ─
    public readonly ILogger Logger;
    internal readonly ILogger ConcreteLogger;

    // ── Per-scenario channels ───────────────────────────────────────────────
    public readonly Channel<string> GeneralChannel  = Channel.CreateUnbounded<string>();
    public readonly Channel<string> ErrorOnlyChannel = Channel.CreateUnbounded<string>();
    public readonly Channel<string> ServiceOnlyChannel = Channel.CreateUnbounded<string>();

    public LoggerFixture()
    {
        ConcreteLogger = new LoggerBuilder()
            .SetLoggerName("IntegrationTestLogger")
            .AddSink(
                new StringChannelTarget(GeneralChannel.Writer, toJson: false, isColor: false),
                new FilterConfig { LogMinLevel = LogLevel.Debug, LogMaxLevel = LogLevel.Critical })
            .AddSink(
                new StringChannelTarget(ErrorOnlyChannel.Writer, toJson: false, isColor: false),
                new FilterConfig { LogMinLevel = LogLevel.Error, LogMaxLevel = LogLevel.Critical })
            .AddSink(
                new StringChannelTarget(ServiceOnlyChannel.Writer, toJson: false, isColor: false),
                new FilterConfig
                {
                    LogMinLevel = LogLevel.Debug,
                    ContextFilterIncludes = ["Service"]
                })
            .Build();
        Logger = ConcreteLogger;
    }

    public async ValueTask DisposeAsync() => await ConcreteLogger.DisposeAsync();
}

/// <summary>
/// Reads from a Channel until a message containing <paramref name="marker"/>
/// is found, or the timeout expires.  Returns the matching message or null.
/// </summary>
file static class ChannelExtensions
{
    public static async Task<string?> FindAsync(
        this Channel<string> ch,
        string marker,
        int timeoutMs = 2000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await foreach (var msg in ch.Reader.ReadAllAsync(cts.Token))
            {
                if (msg.Contains(marker))
                    return msg;
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }
}

/// <summary>
/// Integration tests — use [Collection("Integration")] so all tests share
/// the fixture (which holds the singleton Logger) and run sequentially.
/// </summary>
[Collection("Integration")]
public class LoggerIntegrationTests
{
    // Receive fixture injected via collection (ICollectionFixture pattern)
    private readonly LoggerFixture _f;

    public LoggerIntegrationTests(LoggerFixture fixture) => _f = fixture;

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Basic log output reaches the general channel
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Log_Info_AppearsInGeneralChannel()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        _f.Logger.Info("Integration test message {Id}", id);

        var output = await _f.GeneralChannel.FindAsync(id);
        output.Should().NotBeNull();
        output!.Should().Contain("Integration test message");
        output!.Should().Contain(id);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Level filtering — Debug does NOT reach Error-only sink
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Log_Debug_NotInErrorOnlyChannel()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        _f.Logger.Debug("Debug filter test {Id}", id);

        // Give the pipeline time to process, then assert nothing arrived
        await Task.Delay(300);
        var found = await _f.ErrorOnlyChannel.FindAsync(id, timeoutMs: 100);
        found.Should().BeNull();
    }

    [Fact]
    public async Task Log_Error_AppearsInErrorOnlyChannel()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        _f.Logger.Error("Production error code {Id}", id);

        var output = await _f.ErrorOnlyChannel.FindAsync(id);
        output.Should().NotBeNull();
        output!.Should().Contain(id);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Context filtering — only "Service.*" context reaches the service sink
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Log_WithServiceContext_AppearsInServiceChannel()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var svcLogger = _f.Logger.ForContext("Service.Auth");
        svcLogger.Info("Auth check {Id}", id);

        var output = await _f.ServiceOnlyChannel.FindAsync(id);
        output.Should().NotBeNull();
        output!.Should().Contain(id);
    }

    [Fact]
    public async Task Log_WithOtherContext_NotInServiceChannel()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var otherLogger = _f.Logger.ForContext("Database.Query");
        otherLogger.Info("Query test {Id}", id);

        await Task.Delay(300);
        var found = await _f.ServiceOnlyChannel.FindAsync(id, timeoutMs: 100);
        found.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. ForContext nesting
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ForContext_Chained_ContextPathInOutput()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var logger = _f.Logger.ForContext("Service").ForContext("Payment");
        logger.Info("Payment {Id}", id);

        var output = await _f.GeneralChannel.FindAsync(id);
        output.Should().NotBeNull();
        output!.Should().Contain("[Service.Payment]");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Exception logging
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Log_WithException_ExceptionInOutput()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var ex = new InvalidOperationException($"sentinel-{id}");
        _f.Logger.Error(ex, "Request failed");

        var output = await _f.GeneralChannel.FindAsync($"sentinel-{id}");
        output.Should().NotBeNull();
        output!.Should().Contain("InvalidOperationException");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Message template with multiple properties
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Log_MultipleProperties_AllRenderedCorrectly()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        _f.Logger.Info("User {Name} (tag={Id}) logged in", "Bob", id);

        var output = await _f.GeneralChannel.FindAsync(id);
        output.Should().NotBeNull();
        output!.Should().Contain("User Bob (tag=");
        output!.Should().Contain(id);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Warning level — correct abbreviation in output
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Log_Warning_AppearsWithWrnAbbreviation()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        _f.Logger.Warning("Low disk sentinel {Id}", id);

        var output = await _f.GeneralChannel.FindAsync(id);
        output.Should().NotBeNull();
        output!.Should().Contain("[WRN]");
    }
}

/// <summary>
/// Direct (no LoggerBuilder) tests for ChannelTarget variants.
/// Uses the internal Logger constructor (InternalsVisibleTo) for direct sink testing.
/// Covers LogEntryChannelTarget (0%), DelegateChannelTarget&lt;T&gt; (0%), StringChannelTarget paths.
/// </summary>
public class ChannelTargetDirectTests
{
    private static LogEntry MakeEntry(LogLevel level = LogLevel.Info, string msg = "test")
    {
        var entry = new LogEntry(
            loggerName: "DirectTest",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("DirectTest"),
            timestamp: DateTimeOffset.UtcNow,
            logLevel: level,
            message: msg,
            properties: [],
            context: "",
            contextBytes: default,
            scope: "",
            messageTemplate: LogParser.ParseMessage(msg));
        entry.ParseMessage();
        return entry;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LogEntryChannelTarget
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LogEntryChannelTarget_Emit_TransparentlyPassesLogEntry()
    {
        var ch = Channel.CreateUnbounded<LogEntry>();
        var target = new LogEntryChannelTarget(ch.Writer);
        var entry = MakeEntry(LogLevel.Warning, "passthrough");

        target.Emit(entry);

        ch.Reader.TryRead(out var received).Should().BeTrue();
        received!.LogLevel.Should().Be(LogLevel.Warning);
        received!.Message.Should().Be("passthrough");
    }

    [Fact]
    public void LogEntryChannelTarget_Emit_PreservesAllFields()
    {
        var ch = Channel.CreateUnbounded<LogEntry>();
        var target = new LogEntryChannelTarget(ch.Writer);
        var ts = DateTimeOffset.UtcNow;
        var entry = new LogEntry(
            loggerName: "Logger",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Logger"),
            timestamp: ts,
            logLevel: LogLevel.Error,
            message: "msg",
            properties: [],
            context: "Ctx",
            contextBytes: default,
            scope: "",
            messageTemplate: LogParser.ParseMessage("msg"));

        target.Emit(entry);

        ch.Reader.TryRead(out var received).Should().BeTrue();
        received!.LoggerName.Should().Be("Logger");
        received!.Context.Should().Be("Ctx");
        received!.Timestamp.Should().Be(ts);
    }

    [Fact]
    public async Task LogEntryChannelTarget_Dispose_CompletesChannel()
    {
        var ch = Channel.CreateUnbounded<LogEntry>();
        var target = new LogEntryChannelTarget(ch.Writer);

        target.Dispose();

        await ch.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(1));
        ch.Reader.Completion.IsCompleted.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DelegateChannelTarget<T>
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DelegateChannelTarget_Emit_AppliesTransformDelegate()
    {
        var ch = Channel.CreateUnbounded<int>();
        var target = new DelegateChannelTarget<int>(ch.Writer, e => (int)e.LogLevel);

        target.Emit(MakeEntry(LogLevel.Error));

        ch.Reader.TryRead(out var result).Should().BeTrue();
        result.Should().Be((int)LogLevel.Error);
    }

    [Fact]
    public void DelegateChannelTarget_Emit_StringTransform_ProducesExpectedOutput()
    {
        var ch = Channel.CreateUnbounded<string>();
        var target = new DelegateChannelTarget<string>(ch.Writer, e => e.Message.ToUpperInvariant());

        target.Emit(MakeEntry(msg: "hello"));

        ch.Reader.TryRead(out var result).Should().BeTrue();
        result.Should().Be("HELLO");
    }

    [Fact]
    public async Task DelegateChannelTarget_Dispose_CompletesChannel()
    {
        var ch = Channel.CreateUnbounded<string>();
        var target = new DelegateChannelTarget<string>(ch.Writer, e => e.Message);

        target.Dispose();

        await ch.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(1));
        ch.Reader.Completion.IsCompleted.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // StringChannelTarget — isColor / toJson / dispose
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void StringChannelTarget_WithIsColorTrue_ProducesAnsiOutput()
    {
        var ch = Channel.CreateUnbounded<string>();
        var target = new StringChannelTarget(ch.Writer, toJson: false, isColor: true);

        target.Emit(MakeEntry(LogLevel.Info, "color test"));

        ch.Reader.TryRead(out var result).Should().BeTrue();
        result.Should().Contain("\x1b[");
    }

    [Fact]
    public void StringChannelTarget_WithToJsonTrue_ProducesJsonOutput()
    {
        var ch = Channel.CreateUnbounded<string>();
        var target = new StringChannelTarget(ch.Writer, toJson: true, isColor: false);

        target.Emit(MakeEntry(LogLevel.Info, "json test"));

        ch.Reader.TryRead(out var result).Should().BeTrue();
        result.Should().Contain("\"Level\"");
    }

    [Fact]
    public async Task StringChannelTarget_Dispose_CompletesChannel()
    {
        var ch = Channel.CreateUnbounded<string>();
        var target = new StringChannelTarget(ch.Writer, toJson: false, isColor: false);

        target.Dispose();

        await ch.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(1));
        ch.Reader.Completion.IsCompleted.Should().BeTrue();
    }
}
