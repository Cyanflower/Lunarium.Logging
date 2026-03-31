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
//  This file is a complete Sink configuration reference for Lunarium.Logging.
//  For reference only — not compiled into the library.
//  Covers GlobalConfigurator, all Sink types, FilterConfig,
//  TextOutputIncludeConfig, and the ISinkConfig object-based API,
//  followed by a real-world multi-Sink production example.
// ============================================================

using System.Threading.Channels;
using Lunarium.Logging.Target;

namespace Lunarium.Logging;

// ================================================================
//  Section 1: GlobalConfigurator — Global Configuration
//
//  • Must be called before the first Build(), and only once
//    (protected by a process-level lock).
//  • If not called, Build() automatically applies defaults (see comments below).
//  • Configure() returns a fluent ConfigurationBuilder;
//    call Apply() at the end to commit.
// ================================================================
public static class GlobalConfiguratorExample
{
    public static void Configure()
    {
        GlobalConfigurator.Configure()

            // ── Time zone ─────────────────────────────────────────────────────
            // Default: local time (UseLocalTimeZone)
            .UseCustomTimezone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"))
            // Other options:
            // .UseUtcTimeZone()          // UTC
            // .UseLocalTimeZone()        // System local time (default)

            // ── JSON Sink timestamp format ─────────────────────────────────────
            // Default: ISO 8601 ("O")
            .UseJsonISO8601Timestamp()
            // Other options:
            // .UseJsonUnixTimestamp()           // Unix seconds (long)
            // .UseJsonUnixMsTimestamp()          // Unix milliseconds (long)
            // .UseJsonCustomTimestamp("yyyy-MM-ddTHH:mm:ss")  // Custom format string

            // ── Text Sink timestamp format ─────────────────────────────────────
            // Default: "yyyy-MM-dd HH:mm:ss.fff"
            .UseTextCustomTimestamp("yyyy-MM-dd HH:mm:ss.fff zzz")
            // Other options:
            // .UseTextISO8601Timestamp()         // ISO 8601
            // .UseTextUnixTimestamp()            // Unix seconds
            // .UseTextUnixMsTimestamp()          // Unix milliseconds

            // ── Automatic collection destructuring ────────────────────────────
            // Default: disabled
            // When enabled, List<T>, Dictionary, arrays, etc. are serialized
            // as JSON without needing a manual {@} prefix.
            // When disabled, collections output their ToString() result (usually the type name).
            .EnableAutoDestructuring()

            // ── JSON escaping of non-ASCII characters ─────────────────────────
            // Default: preserve original characters (UnsafeRelaxedJsonEscaping)
            // .DisableUnsafeRelaxedJsonEscaping()    // Escape CJK/Emoji as \uXXXX
            // .EnableUnsafeRelaxedJsonEscaping()     // Explicitly keep original chars (same as default)

            // ── JSON indentation ──────────────────────────────────────────────
            // Default: compact (single line)
            // .UseIndentedJson()   // Pretty-print with line breaks; slightly larger output
            // .UseCompactJson()    // Compact (explicit; same as default)

            // ── AOT: register a JsonSerializerContext (see AdvancedUsage.EN.cs) ─
            // .UseJsonTypeInfoResolver(MyAppJsonContext.Default)

            .Apply();
    }
}

// ================================================================
//  Section 2: FilterConfig — Per-Sink Level and Context Filtering
//
//  Each Sink has its own FilterConfig; they do not affect each other.
//  Filter execution order:
//    1. Level check: LogMinLevel ≤ entry.Level ≤ LogMaxLevel
//    2. Include check: Context must start with one of the listed prefixes
//                      (empty list = all pass)
//    3. Exclude check: entries whose Context matches any exclude prefix are dropped
// ================================================================
public static class FilterConfigReference
{
    public static FilterConfig[] Examples =
    [
        // Example 1: Accept Info level and above (most common)
        new FilterConfig
        {
            LogMinLevel = LogLevel.Info,        // default value
            LogMaxLevel = LogLevel.Critical,    // default value
        },

        // Example 2: Exact level match (MinLevel == MaxLevel)
        new FilterConfig
        {
            LogMinLevel = LogLevel.Warning,
            LogMaxLevel = LogLevel.Warning,    // Warning only — Error/Critical excluded
        },

        // Example 3: Context Include whitelist
        //   Only accept entries whose Context starts with "Payment" or "Order"
        new FilterConfig
        {
            ContextFilterIncludes = ["Payment", "Order"],
            LogMinLevel = LogLevel.Debug,
        },

        // Example 4: Context Exclude blacklist
        //   Accept all entries except those from high-frequency modules "Proxy" and "Heartbeat"
        new FilterConfig
        {
            ContextFilterExcludes = ["Proxy", "Heartbeat"],
            LogMinLevel = LogLevel.Info,
        },

        // Example 5: Include + Exclude combined
        //   First Include (only "Runtime" prefix), then Exclude ("Runtime.Proxy")
        new FilterConfig
        {
            ContextFilterIncludes = ["Runtime"],
            ContextFilterExcludes = ["Runtime.Proxy"],
            LogMinLevel = LogLevel.Info,
        },

        // Example 6: Case-insensitive matching
        //   "payment", "Payment", and "PAYMENT" all match
        new FilterConfig
        {
            ContextFilterIncludes = ["Payment"],
            IgnoreFilterCase = true,
        },
    ];
}

// ================================================================
//  Section 3: TextOutputIncludeConfig — Field Visibility for Text/Color Output
//
//  Applies to ConsoleTarget, FileTarget (text mode), StringChannelTarget,
//  and ByteChannelTarget. Controls which prefix fields appear in each log line.
//
//  Default output format:
//    [2026-03-30 14:00:00.000] [INFO ] [MyApp] [Order.Service] message body
//     ^^^^timestamp^^^^        ^level^ ^logger name^  ^context^
// ================================================================
public static class TextOutputIncludeConfigReference
{
    public static TextOutputIncludeConfig[] Examples =
    [
        // Show all fields (default behavior)
        new TextOutputIncludeConfig
        {
            IncludeTimestamp  = true,
            IncludeLevel      = true,
            IncludeLoggerName = true,
            IncludeContext    = true,
        },

        // Minimal mode: show only level and message
        new TextOutputIncludeConfig
        {
            IncludeTimestamp  = false,
            IncludeLevel      = true,
            IncludeLoggerName = false,
            IncludeContext    = false,
        },

        // Hide LoggerName, keep Context (reduces redundancy when multiple Loggers are in use)
        new TextOutputIncludeConfig
        {
            IncludeTimestamp  = true,
            IncludeLevel      = true,
            IncludeLoggerName = false,
            IncludeContext    = true,
        },
    ];
}

// ================================================================
//  Section 4: All Sink Types at a Glance
//
//  Each Sink type has a corresponding AddXxxSink extension method that
//  can be chained on LoggerBuilder.
//  All methods accept optional FilterConfig and sinkName parameters.
// ================================================================
public static class AllSinkTypesExample
{
    public static ILogger Build()
    {
        ChannelReader<string> stringReader;
        ChannelReader<byte[]> bytesReader;
        ChannelReader<LogEntry> entryReader;

        return new LoggerBuilder()
            .SetLoggerName("MyApp")

            // ── ConsoleSink ────────────────────────────────────────────────────
            // Automatically uses color when output is not redirected.
            // Error/Critical → stderr; all other levels → stdout.
            // isColor=true (default): ANSI color. Auto-downgrade on redirect or NO_COLOR env var.
            // toJson=false (default): text format.
            .AddConsoleSink(
                isColor: true,
                toJson: false,
                textOutputIncludeConfig: new TextOutputIncludeConfig
                {
                    IncludeTimestamp  = true,
                    IncludeLevel      = true,
                    IncludeLoggerName = true,
                    IncludeContext    = true,
                },
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Debug })

            // ── FileSink (no rotation) ─────────────────────────────────────────
            // Append-only, no automatic rotation; suitable for small apps or short debugging sessions.
            .AddFileSink(
                logFilePath: "Logs/app.log")

            // ── SizedRotatingFileSink (rotate by file size) ────────────────────
            // Rotates when the file exceeds maxFileSizeMB. New file names include a full timestamp:
            //   app-2026-03-30-14-00-00.log
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/app-sized.log",
                maxFileSizeMB: 10,
                maxFile: 5,         // Keep at most 5 historical files; 0 = unlimited
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

            // ── TimedRotatingFileSink (rotate daily) ───────────────────────────
            // Rotates at 00:00 each day; file names include the date: app-2026-03-30.log
            .AddTimedRotatingFileSink(
                logFilePath: "Logs/app-daily.log",
                maxFile: 30,        // Keep at most 30 days of files
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

            // ── RotatingFileSink (combined rotation: size + daily) ─────────────
            // Rotates when either condition is met; file names include a full timestamp
            // (same as size-based rotation). rotateOnNewDay=true can be omitted —
            // AddRotatingFileSink is specifically designed for combined scenarios.
            .AddRotatingFileSink(
                logFilePath: "Logs/app-combined.log",
                maxFileSizeMB: 50,
                rotateOnNewDay: true,
                maxFile: 14)

            // ── StringChannelSink ─────────────────────────────────────────────
            // Formats log entries as strings and writes them to a Channel for external consumers
            // (UI, WebSocket, etc.). Each Emit causes one string heap allocation;
            // if the consumer writes directly to network/file, prefer Utf8ByteChannel.
            .AddStringChannelSink(
                out stringReader,
                capacity: 1000,     // Bounded Channel capacity; new writes are dropped when full (DropWrite)
                isColor: false,
                toJson: false,
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

            // ── Utf8ByteChannelSink ───────────────────────────────────────────
            // Formats log entries as byte[] and writes to a Channel, skipping UTF-8→string decoding.
            // Ideal for consumers that write directly to network/file (HttpClient.SendAsync, FileStream.Write, etc.).
            .AddUtf8ByteChannelSink(
                out bytesReader,
                capacity: 1000,
                toJson: true,       // JSON format; false = plain text
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

            // ── LogEntryChannelSink ───────────────────────────────────────────
            // Pushes LogEntry objects directly into a Channel with zero formatting overhead.
            // Ideal for consumers that need fully structured data (custom processing, database writes, etc.).
            .AddLogEntryChannelSink(
                out entryReader,
                capacity: 500,
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Warning })

            .Build();

        // Channel consumption example (background task):
        // _ = Task.Run(async () =>
        // {
        //     await foreach (var line in stringReader.ReadAllAsync())
        //         Console.WriteLine(line);
        // });
        //
        // _ = Task.Run(async () =>
        // {
        //     await foreach (var entry in entryReader.ReadAllAsync())
        //     {
        //         // Access structured fields directly; decide how to handle them
        //         Console.WriteLine($"[{entry.LogLevel}] {entry.LoggerName} {entry.Message}");
        //     }
        // });
    }
}

// ================================================================
//  Section 5: ISinkConfig — Object-Based Sink Configuration
//
//  The AddXxxSink() methods above use "direct parameter" style, which is
//  suitable for hard-coded configuration in source code.
//  If Sink configuration comes from an external source (JSON file, database,
//  config center), use ISinkConfig implementations:
//    ConsoleSinkConfig / FileSinkConfig (built-in) / HttpSinkConfig (Http package)
//
//  Register via AddSinkByConfig(ISinkConfig) — the builder is agnostic
//  of the underlying implementation.
// ================================================================
public static class ISinkConfigExample
{
    public static ILogger Build()
    {
        // Load as POCO objects from an external source, then pass them in
        ISinkConfig[] sinkConfigs =
        [
            new ConsoleSinkConfig
            {
                ToJson   = false,
                IsColor  = true,
                Enabled  = true,
                FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Debug },
            },

            new FileSinkConfig
            {
                LogFilePath    = "Logs/Runtime.log",
                MaxFileSizeMB  = 10,
                RotateOnNewDay = false,
                MaxFile        = 5,
                ToJson         = false,
                Enabled        = true,
                FilterConfig   = new FilterConfig { LogMinLevel = LogLevel.Info },
            },

            new FileSinkConfig
            {
                LogFilePath    = "Logs/Error.log",
                MaxFileSizeMB  = 5,
                RotateOnNewDay = true,
                MaxFile        = 30,
                Enabled        = true,
                FilterConfig   = new FilterConfig
                {
                    LogMinLevel = LogLevel.Error,
                    LogMaxLevel = LogLevel.Critical,
                },
            },

            // HttpSinkConfig from the Http package also implements ISinkConfig
            // and can be registered here in a unified way:
            // new Lunarium.Logging.Http.Config.HttpSinkConfig
            // {
            //     Endpoint   = new Uri("http://localhost:5341/api/events/raw"),
            //     HttpClient = sharedHttpClient,
            //     BatchSize  = 100,
            //     Enabled    = true,
            // },
        ];

        var builder = new LoggerBuilder().SetLoggerName("MyApp");
        foreach (var cfg in sinkConfigs)
            builder.AddSinkByConfig(cfg);
        return builder.Build();
    }
}

// ================================================================
//  Section 6: Real-World Multi-Sink Production Scenario
//
//  A typical production configuration:
//    • Main log file (Runtime.log)     — all logs except high-frequency modules
//    • Module-specific logs            — high-frequency modules archived separately
//    • Error.log / Warning.log         — global error/warning aggregation for quick triage
//    • Audit log (daily rotation)      — date-organized audit records
//    • Console                         — real-time observation during development,
//                                        level controllable via environment variable
//    • ChannelSink                     — push to UI or WebSocket broadcast
// ================================================================
public static class ProductionLoggerConfigurator
{
    /// <summary>
    /// Builds the global singleton logger.
    ///
    /// <para><b>Environment variables:</b></para>
    /// <list type="table">
    /// <item><term>FILE_LOG_LEVEL</term><description>Minimum level for file Sinks. Default: Info.</description></item>
    /// <item><term>CONSOLE_LOG_LEVEL</term><description>Minimum level for console Sink. Default: Info.</description></item>
    /// </list>
    /// </summary>
    public static ILogger Build(out ChannelReader<string> outReader, string? context = null)
    {
        // Read log levels from environment variables
        // (can be replaced with appsettings.json, command-line args, etc.)
        var fileLogLevel    = ParseEnvLevel("FILE_LOG_LEVEL",    LogLevel.Info);
        var consoleLogLevel = ParseEnvLevel("CONSOLE_LOG_LEVEL", LogLevel.Info);

        // Global configuration (called only once)
        GlobalConfigurator.Configure()
            .UseCustomTimezone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"))
            .UseJsonISO8601Timestamp()
            .UseTextCustomTimestamp("yyyy-MM-dd HH:mm:ss.fff zzz")
            .EnableAutoDestructuring()
            .Apply();

        var rootLogger = new LoggerBuilder()
            .SetLoggerName("Runtime")

            // Main log: excludes high-frequency modules, size-rotating, keeps 5 files
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/Runtime/Runtime.log",
                maxFileSizeMB: 10,
                maxFile: 5,
                FilterConfig: new FilterConfig
                {
                    ContextFilterExcludes = ["Runtime.ProxyService", "Runtime.LoginService"],
                    LogMinLevel = fileLogLevel,
                })

            // ProxyService-specific: only this prefix, with more retained files
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/ProxyService/ProxyService.log",
                maxFileSizeMB: 10,
                maxFile: 10,
                FilterConfig: new FilterConfig
                {
                    ContextFilterIncludes = ["Runtime.ProxyService"],
                    LogMinLevel = fileLogLevel,
                })

            // LoginService-specific: archived separately for security auditing
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/Login/Login.log",
                maxFileSizeMB: 10,
                maxFile: 10,
                FilterConfig: new FilterConfig
                {
                    ContextFilterIncludes = ["Runtime.LoginService"],
                    LogMinLevel = fileLogLevel,
                })

            // Global error aggregation: no context filter, all modules' Error+ go here
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/EW/Error.log",
                maxFileSizeMB: 10,
                maxFile: 5,
                FilterConfig: new FilterConfig
                {
                    LogMinLevel = LogLevel.Error,
                    // LogMaxLevel defaults to Critical — captures both Error and Critical
                })

            // Exact Warning level only (MinLevel == MaxLevel)
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/EW/Warning.log",
                maxFileSizeMB: 10,
                maxFile: 5,
                FilterConfig: new FilterConfig
                {
                    LogMinLevel = LogLevel.Warning,
                    LogMaxLevel = LogLevel.Warning,
                })

            // Audit log: daily rotation, keep 30 days
            .AddTimedRotatingFileSink(
                logFilePath: "Logs/Audit/Audit.log",
                maxFile: 30,
                FilterConfig: new FilterConfig
                {
                    ContextFilterIncludes = ["Runtime.Audit"],
                    LogMinLevel = LogLevel.Info,
                })

            // Combined rotation: triggers on >50 MB or new day
            .AddRotatingFileSink(
                logFilePath: "Logs/System/System.log",
                maxFileSizeMB: 50,
                rotateOnNewDay: true,
                maxFile: 14,
                FilterConfig: new FilterConfig
                {
                    ContextFilterIncludes = ["Runtime.System"],
                    LogMinLevel = fileLogLevel,
                })

            // Console: real-time observation during development; auto-downgrade on redirect
            .AddConsoleSink(
                FilterConfig: new FilterConfig { LogMinLevel = consoleLogLevel })

            // ChannelSink: push to UI/WebSocket; JSON takes priority over isColor
            .AddStringChannelSink(
                out var reader,
                capacity: 1000,
                isColor: true,
                toJson: true,
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

            .Build();

        outReader = reader;
        return context is not null ? rootLogger.ForContext(context) : rootLogger;
    }

    private static LogLevel ParseEnvLevel(string envKey, LogLevel defaultLevel)
    {
        var raw = Environment.GetEnvironmentVariable(envKey);
        return !string.IsNullOrEmpty(raw) && Enum.TryParse<LogLevel>(raw, true, out var parsed)
            ? parsed
            : defaultLevel;
    }
}
