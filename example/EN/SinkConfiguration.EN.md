# SinkConfiguration — Complete Sink Configuration Reference

> Full code version: [RawCSharp/SinkConfiguration.EN.cs](RawCSharp/SinkConfiguration.EN.cs)

---

## Section 1: GlobalConfigurator — Global Configuration

Must be called before the first `Build()`, and only once per process. If not called, `Build()` automatically applies default values.

```csharp
GlobalConfigurator.Configure()
    // Time zone (default: local time)
    .UseCustomTimezone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"))
    // .UseUtcTimeZone()
    // .UseLocalTimeZone()

    // JSON Sink timestamp format (default: ISO 8601)
    .UseJsonISO8601Timestamp()
    // .UseJsonUnixTimestamp()      // Unix seconds (long)
    // .UseJsonUnixMsTimestamp()    // Unix milliseconds (long)
    // .UseJsonCustomTimestamp("yyyy-MM-ddTHH:mm:ss")

    // Text Sink timestamp format (default: "yyyy-MM-dd HH:mm:ss.fff")
    .UseTextCustomTimestamp("yyyy-MM-dd HH:mm:ss.fff zzz")
    // .UseTextISO8601Timestamp()
    // .UseTextUnixTimestamp()
    // .UseTextUnixMsTimestamp()

    // Automatic collection destructuring (default: disabled)
    // When enabled, List<T>, Dictionary, arrays, etc. are serialized as JSON
    // without needing a manual {@} prefix.
    .EnableAutoDestructuring()

    // JSON escaping of non-ASCII characters (default: preserve original characters)
    // .DisableUnsafeRelaxedJsonEscaping()    // Escape CJK/Emoji as \uXXXX
    // .EnableUnsafeRelaxedJsonEscaping()     // Explicitly keep original chars (same as default)

    // JSON indentation (default: compact single line)
    // .UseIndentedJson()
    // .UseCompactJson()

    .Apply();
```

**Default values summary:**

| Setting | Default |
|---------|---------|
| Time zone | Local time |
| JSON timestamp | ISO 8601 (`"O"`) |
| Text timestamp | `"yyyy-MM-dd HH:mm:ss.fff"` |
| Auto destructuring | Disabled |
| JSON non-ASCII escaping | Preserve original characters |
| JSON indentation | Compact single line |

---

## Section 2: FilterConfig — Level and Context Filtering

Each Sink has its own independent FilterConfig; they do not affect each other.

**Filter execution order:**
1. Level check: `LogMinLevel ≤ entry.LogLevel ≤ LogMaxLevel`
2. Include check: Context prefix must match at least one Include entry (empty list = all pass)
3. Exclude check: entries whose Context matches any Exclude prefix are dropped

```csharp
// Accept Info and above (most common)
new FilterConfig { LogMinLevel = LogLevel.Info }

// Exact single-level match
new FilterConfig { LogMinLevel = LogLevel.Warning, LogMaxLevel = LogLevel.Warning }

// Context Include whitelist
new FilterConfig
{
    ContextFilterIncludes = ["Payment", "Order"],
    LogMinLevel = LogLevel.Debug,
}

// Context Exclude blacklist
new FilterConfig
{
    ContextFilterExcludes = ["Proxy", "Heartbeat"],
    LogMinLevel = LogLevel.Info,
}

// Include + Exclude combined (Include first, then Exclude)
new FilterConfig
{
    ContextFilterIncludes = ["Runtime"],
    ContextFilterExcludes = ["Runtime.Proxy"],
    LogMinLevel = LogLevel.Info,
}

// Case-insensitive matching
new FilterConfig
{
    ContextFilterIncludes = ["Payment"],
    IgnoreFilterCase = true,
}
```

---

## Section 3: TextOutputIncludeConfig — Field Visibility for Text Output

Applies to ConsoleSink, FileSink (text mode), StringChannelSink, and Utf8ByteChannelSink.

Default output format:
```
[2026-03-30 14:00:00.000] [INFO ] [MyApp] [Order.Service] message body
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `IncludeTimestamp` | bool | true | Whether to show the timestamp |
| `IncludeLevel` | bool | true | Whether to show the log level |
| `IncludeLoggerName` | bool | true | Whether to show the Logger name |
| `IncludeContext` | bool | true | Whether to show the Context |

```csharp
// Minimal mode: show only level and message
new TextOutputIncludeConfig
{
    IncludeTimestamp  = false,
    IncludeLevel      = true,
    IncludeLoggerName = false,
    IncludeContext    = false,
}
```

---

## Section 4: All Sink Types

### ConsoleSink

Automatically uses color when output is not redirected; `Error`/`Critical` → stderr, all others → stdout.

```csharp
.AddConsoleSink(
    isColor: true,    // color (auto-downgrade on redirect or NO_COLOR env var)
    toJson: false,    // false = text; true = JSON
    textOutputIncludeConfig: new TextOutputIncludeConfig { ... },
    FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })
```

### FileSink (no rotation)

```csharp
.AddFileSink(logFilePath: "Logs/app.log")
```

### SizedRotatingFileSink (rotate by file size)

Rotates when the file exceeds `maxFileSizeMB`; new file names include a full timestamp: `app-2026-03-30-14-00-00.log`

```csharp
.AddSizedRotatingFileSink(
    logFilePath: "Logs/app.log",
    maxFileSizeMB: 10,
    maxFile: 5,        // Keep at most 5 historical files; 0 = unlimited
    FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })
```

### TimedRotatingFileSink (rotate daily)

Rotates at 00:00 each day; file names include the date: `app-2026-03-30.log`

```csharp
.AddTimedRotatingFileSink(
    logFilePath: "Logs/app.log",
    maxFile: 30,       // Keep at most 30 days of files
    FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })
```

### RotatingFileSink (combined rotation: size + daily)

Rotates when either condition is met; file names include a full timestamp.

```csharp
.AddRotatingFileSink(
    logFilePath: "Logs/app.log",
    maxFileSizeMB: 50,
    rotateOnNewDay: true,
    maxFile: 14)
```

### StringChannelSink

Formats log entries as `string` and pushes them into a Channel for external consumers (UI, WebSocket, etc.). Each Emit causes one string heap allocation.

```csharp
.AddStringChannelSink(
    out ChannelReader<string> reader,
    capacity: 1000,
    isColor: false,
    toJson: false,
    FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })
```

### Utf8ByteChannelSink

Formats log entries as `byte[]` and pushes them into a Channel, skipping UTF-8→string decoding. Ideal for consumers that write directly to network/file.

```csharp
.AddUtf8ByteChannelSink(
    out ChannelReader<byte[]> reader,
    capacity: 1000,
    toJson: true)
```

### LogEntryChannelSink

Pushes `LogEntry` objects directly into a Channel with zero formatting overhead. Ideal for scenarios requiring fully structured data.

```csharp
.AddLogEntryChannelSink(
    out ChannelReader<LogEntry> reader,
    capacity: 500,
    FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Warning })
```

**Performance comparison of the three ChannelSink variants:**

| Sink | Formatting | Downstream use case |
|------|------------|---------------------|
| `StringChannelSink` | UTF-8 → string (one allocation) | Display strings directly |
| `Utf8ByteChannelSink` | UTF-8 bytes → byte[] (one copy) | Write directly to network/file |
| `LogEntryChannelSink` | No formatting | Custom processing logic |

---

## Section 5: ISinkConfig — Object-Based Configuration

Suitable when Sink configuration comes from an external source (JSON file, database, config center); register via `AddSinkByConfig()` in a unified way.

```csharp
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
        Enabled        = true,
        FilterConfig   = new FilterConfig { LogMinLevel = LogLevel.Info },
    },
    new FileSinkConfig
    {
        LogFilePath    = "Logs/Error.log",
        MaxFileSizeMB  = 5,
        RotateOnNewDay = true,
        MaxFile        = 30,
        FilterConfig   = new FilterConfig
        {
            LogMinLevel = LogLevel.Error,
            LogMaxLevel = LogLevel.Critical,
        },
    },
    // HttpSinkConfig from the Http package also implements ISinkConfig:
    // new Lunarium.Logging.Http.Config.HttpSinkConfig { Endpoint = ..., HttpClient = ... }
];

var builder = new LoggerBuilder().SetLoggerName("MyApp");
foreach (var cfg in sinkConfigs)
    builder.AddSinkByConfig(cfg);
var logger = builder.Build();
```

---

## Section 6: Real-World Multi-Sink Production Scenario

A typical production configuration: main log + per-module archives + global error/warning aggregation + console + ChannelSink.

> See the `ProductionLoggerConfigurator` class in [RawCSharp/SinkConfiguration.EN.cs](RawCSharp/SinkConfiguration.EN.cs) for the full code.

```csharp
var logger = new LoggerBuilder()
    .SetLoggerName("Runtime")

    // Main log: excludes high-frequency modules, rotates by size
    .AddSizedRotatingFileSink("Logs/Runtime/Runtime.log", 10, 5,
        FilterConfig: new FilterConfig
        {
            ContextFilterExcludes = ["Runtime.ProxyService", "Runtime.LoginService"],
            LogMinLevel = LogLevel.Info,
        })

    // Per-module archive: only accepts entries with the matching Context prefix
    .AddSizedRotatingFileSink("Logs/ProxyService/ProxyService.log", 10, 10,
        FilterConfig: new FilterConfig
        {
            ContextFilterIncludes = ["Runtime.ProxyService"],
            LogMinLevel = LogLevel.Info,
        })

    // Global error aggregation: no Context filter — all modules' Error+ entries go here
    .AddSizedRotatingFileSink("Logs/EW/Error.log", 10, 5,
        FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Error })

    // Exact Warning level only (MinLevel == MaxLevel)
    .AddSizedRotatingFileSink("Logs/EW/Warning.log", 10, 5,
        FilterConfig: new FilterConfig
        {
            LogMinLevel = LogLevel.Warning,
            LogMaxLevel = LogLevel.Warning,
        })

    // Console: real-time observation during development
    .AddConsoleSink(FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

    // ChannelSink: push to UI or WebSocket
    .AddStringChannelSink(out var reader, capacity: 1000, toJson: true,
        FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

    .Build();
```
