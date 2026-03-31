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

// ============================================================
//  This file is an advanced usage example for Lunarium.Logging.
//  For reference only — not compiled into the library.
//  Covers custom Sinks (ILogTarget / ChannelTarget<T>), high-performance
//  destructuring interfaces (IDestructurable / IDestructured), AOT
//  configuration, and LoggerManager runtime dynamic configuration.
// ============================================================

using System.Text.Json.Serialization;
using System.Threading.Channels;
using Lunarium.Logging.Config;
using Lunarium.Logging.Target;
using Lunarium.Logging.Configuration;
using Lunarium.Logging.Extensions;
using Microsoft.Extensions.Hosting;

namespace Lunarium.Logging;

// ================================================================
//  Section 1: LogEntry Field Reference
//
//  LogEntry is an immutable log event object. It is created by the
//  Logger, pushed into a Channel, and passed to ILogTarget after
//  being filtered and parsed inside Sink.Emit().
// ================================================================

/*  LogEntry fields at a glance:

  | Field              | Type                    | Description                                                    |
  |--------------------|-------------------------|----------------------------------------------------------------|
  | LoggerName         | string                  | Logger instance name (set at build time via .SetLoggerName())  |
  | LoggerNameBytes    | ReadOnlyMemory<byte>    | UTF-8 pre-encoded bytes of LoggerName; Writer layer writes directly |
  | LogLevel           | LogLevel                | Log level (Debug/Info/Warning/Error/Critical)                  |
  | Timestamp          | DateTimeOffset          | Timestamp (according to GlobalConfigurator time zone setting)  |
  | Message            | string                  | Raw message template string (not yet rendered)                 |
  | Properties         | object?[]               | Array of message template property values                      |
  | Context            | string                  | Context string (set via ForContext)                            |
  | ContextBytes       | ReadOnlyMemory<byte>    | UTF-8 pre-encoded bytes of Context                             |
  | Scope              | string                  | MEL Scope info (populated by ILoggingBuilder)                  |
  | Exception          | Exception?              | Associated exception                                           |
  | MessageTemplate    | MessageTemplate         | Lazily parsed template (filled by Sink internally at Emit time)|
*/

// ================================================================
//  Section 2: Custom ILogTarget — The Lowest-Level Extension Point
//
//  Three extension paths:
//  A. Implement ILogTarget (synchronous, lowest level)
//  B. Inherit ChannelTarget<T> (async Channel bridge; only implement Transform)
//  C. Implement capability interfaces (IJsonTextTarget / ITextTarget)
//     — lets FilterConfig control the output format uniformly
// ================================================================

// ── Path A: Implement ILogTarget (in-memory buffer Sink example) ───────────────
public sealed class InMemoryLogTarget : ILogTarget
{
    private readonly List<LogEntry> _entries = new();
    private readonly Lock _lock = new();

    public void Emit(LogEntry entry)
    {
        lock (_lock)
            _entries.Add(entry);
    }

    /// <summary>Takes and clears all buffered log entries.</summary>
    public IReadOnlyList<LogEntry> TakeAll()
    {
        lock (_lock)
        {
            var snapshot = _entries.ToList();
            _entries.Clear();
            return snapshot;
        }
    }

    public void Dispose() { }
}

// Registration:
// var memTarget = new InMemoryLogTarget();
// var logger = new LoggerBuilder()
//     .SetLoggerName("Test")
//     .AddSink(memTarget, filterConfig: null, name: "memory")
//     .Build();

// ── Path B: Inherit ChannelTarget<T> (async database-write Sink example) ───────
// ChannelTarget<T> base class accepts a ChannelWriter<T>; the caller creates the
// Channel and retains the Reader.
// Subclasses only need to implement Transform(LogEntry) → T.
public sealed class DatabaseLogTarget : ChannelTarget<DatabaseLogRecord>
{
    private readonly string _connectionString;

    // Caller passes in the Channel's write end; the read end is retained by the caller
    // (see registration example below)
    public DatabaseLogTarget(string connectionString, ChannelWriter<DatabaseLogRecord> writer)
        : base(writer)
    {
        _connectionString = connectionString;
    }

    protected override DatabaseLogRecord Transform(LogEntry entry)
    {
        // Convert LogEntry to a database record object
        return new DatabaseLogRecord
        {
            Timestamp   = entry.Timestamp.UtcDateTime,
            Level       = entry.LogLevel.ToString(),
            Logger      = entry.LoggerName,
            Context     = entry.Context,
            Message     = entry.Message,       // raw template; can also be stored rendered
            Exception   = entry.Exception?.ToString(),
        };
    }
}

// Registration: caller creates the Channel, passes the Writer to the Target, and
// holds the Reader for background consumption
// var channel  = Channel.CreateBounded<DatabaseLogRecord>(1000);
// var dbTarget = new DatabaseLogTarget("Server=...;", channel.Writer);
// var logger   = new LoggerBuilder()
//     .SetLoggerName("MyApp")
//     .AddSink(dbTarget)
//     .Build();
//
// // Background consumer:
// _ = Task.Run(async () =>
// {
//     await foreach (var record in channel.Reader.ReadAllAsync())
//     {
//         await using var conn = new SqlConnection(_connectionString);
//         await conn.ExecuteAsync("INSERT INTO Logs ...", record);
//     }
// });

public sealed class DatabaseLogRecord
{
    public DateTime Timestamp { get; set; }
    public string Level     { get; set; } = "";
    public string Logger    { get; set; } = "";
    public string Context   { get; set; } = "";
    public string Message   { get; set; } = "";
    public string? Exception { get; set; }
}

// ================================================================
//  Section 3: IDestructurable — High-Performance Custom Destructuring ({@Object})
//
//  Faster than the JsonSerializer fallback path and fully AOT-compatible.
//  Writes directly into the output buffer via DestructureHelper
//  (wraps Utf8JsonWriter), with no intermediate string allocations.
//
//  Priority order: IDestructurable > IDestructured > JsonSerializer fallback
// ================================================================
public sealed class Order : IDestructurable
{
    public int    Id     { get; init; }
    public string Name   { get; init; } = "";
    public decimal Amount { get; init; }

    public void Destructure(DestructureHelper helper)
    {
        helper.WriteStartObject();
        helper.WriteNumber("id",     Id);
        helper.WriteString("name",   Name);
        helper.WriteNumber("amount", (double)Amount);
        helper.WriteEndObject();
    }
}

/*  DestructureHelper common methods quick reference:

  | Method                                | Description                              |
  |---------------------------------------|------------------------------------------|
  | WriteStartObject() / WriteEndObject() | Begin/end a JSON object                  |
  | WriteStartArray()  / WriteEndArray()  | Begin/end a JSON array                   |
  | WriteString(key, value)               | Write a string field                     |
  | WriteNumber(key, int/long/double)     | Write a numeric field                    |
  | WriteBoolean(key, bool)               | Write a boolean field                    |
  | WriteNull(key)                        | Write a null field                       |
  | WritePropertyName(key)                | Write a field name (follow with a value) |
  | WriteStringValue(value)               | Write a string array element             |
  | WriteNumberValue(int/long/double)     | Write a numeric array element            |
*/

// Usage example:
// var order = new Order { Id = 42, Name = "Book", Amount = 99.9m };
// logger.Info("New order {@Order}", order);
// JSON output: {"id":42,"name":"Book","amount":99.9}

// ================================================================
//  Section 4: IDestructured — Zero-Allocation Pre-Serialized Bytes
//
//  Ideal for static or immutable data: compute the UTF-8 JSON bytes
//  ahead of time, then embed them directly into the output buffer at
//  Emit time — no allocations whatsoever.
// ================================================================
public sealed class AppVersion : IDestructured
{
    // Static readonly bytes, reused for the lifetime of the process
    private static readonly byte[] _bytes =
        System.Text.Encoding.UTF8.GetBytes("""{"major":1,"minor":2,"patch":0}""");

    public ReadOnlyMemory<byte> Destructured() => _bytes;
}

// Usage example:
// logger.Info("Application version {@Version}", new AppVersion());
// JSON output: {"major":1,"minor":2,"patch":0}

// ================================================================
//  Section 5: AOT (Native AOT / Trimming) Configuration
//
//  The main library is marked IsAotCompatible=true, but {@Object}
//  destructuring relies on JsonSerializer in AOT environments, which
//  requires pre-registering a Source Generated JsonSerializerContext.
//  Types without a registration silently fall back to ToString() —
//  no runtime exceptions.
// ================================================================

// Step 1: Create a JsonSerializerContext for types that need {@} destructuring
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(DatabaseLogRecord))]
[JsonSerializable(typeof(List<Order>))]
internal partial class MyAppJsonContext : JsonSerializerContext { }

// Step 2: Register in GlobalConfigurator (before Build(), called only once)
public static class AotConfigurationExample
{
    public static void Configure()
    {
        GlobalConfigurator.Configure()
            // Register the Source Generated Context; {@Object} destructuring
            // follows the source-generated path under AOT
            .UseJsonTypeInfoResolver(MyAppJsonContext.Default)
            // Multiple contexts can be combined externally before passing in:
            // .UseJsonTypeInfoResolver(
            //     JsonTypeInfoResolver.Combine(MyAppJsonContext.Default, AnotherContext.Default))
            .Apply();
    }
}

// ================================================================
//  Section 6: LoggerManager — Runtime Dynamic Configuration
//
//  All Loggers created via Build() are automatically registered with LoggerManager.
//  Sink configuration (FilterConfig, Enabled, etc.) can be updated at runtime
//  without restarting the application.
//  The hot-reload feature of the Configuration package calls this API internally.
// ================================================================
public static class LoggerManagerExample
{
    public static void RuntimeUpdate()
    {
        // ── View currently registered Logger names ────────────────────────────
        var loggerNames = LoggerManager.GetLoggerList();
        // Returns: ["Runtime", "Analytics", ...]

        // ── Update a single Sink's configuration within a single Logger ───────
        // Scenario: temporarily enable Debug level on a Sink at runtime to
        // diagnose a production issue
        LoggerManager.UpdateSinkConfig(
            loggerName: "Runtime",
            sinkName:   "console",           // name specified at build time via the sinkName parameter
            sinkConfig: new ConsoleSinkConfig
            {
                Enabled      = true,
                FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Debug },
            });

        // ── Update all Sink configurations for a single Logger via LoggerConfig ─
        // Suitable for a full replacement after re-reading from a config file
        LoggerManager.UpdateLoggerConfig(
            loggerConfigs: new LoggerConfig
            {
                LoggerName = "Runtime",
                ConsoleSinks = new Dictionary<string, ConsoleSinkConfig>
                {
                    ["console"] = new() { FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Info } }
                },
                FileSinks = new Dictionary<string, FileSinkConfig>
                {
                    ["main"] = new()
                    {
                        LogFilePath  = "Logs/Runtime.log",
                        MaxFileSizeMB = 10,
                        MaxFile      = 5,
                        FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Info },
                    }
                }
            });

        // ── Bulk-update all registered Loggers via a LoggerConfig list ─────────
        // Typical use: push a new configuration to all Loggers when hot-reloading
        // LoggerManager.UpdateAllLoggerConfig(newLoggerConfigs);

        // ── Notes ──────────────────────────────────────────────────────────────
        // • UpdateSinkConfig / UpdateLoggerConfig only update the FilterConfig and
        //   Enabled flag of existing Sinks — they cannot add or remove Sinks
        //   (the Sink list is fixed at Build() time).
        // • Parameters that affect Target construction (e.g., LogFilePath) cannot
        //   be hot-updated; a restart is required.
        // • Thread safety: all methods are atomic operations (Interlocked.Exchange).
    }
}
