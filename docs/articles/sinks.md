# Sink Configuration

> Full runnable example: [SinkConfiguration.EN.cs](https://github.com/Cyanflower/Lunarium.Logging/blob/main/example/RawCSharp/EN/SinkConfiguration.EN.cs)

## GlobalConfigurator

Must be called before the first `Build()`, and only once per process. Omitting it applies defaults automatically.

```csharp
GlobalConfigurator.Configure()
    .UseCustomTimezone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"))
    .UseJsonISO8601Timestamp()
    .UseTextCustomTimestamp("yyyy-MM-dd HH:mm:ss.fff zzz")
    .EnableAutoDestructuring()
    .Apply();
```

| Setting | Default |
|---------|---------|
| Time zone | Local time |
| JSON timestamp | ISO 8601 |
| Text timestamp | `"yyyy-MM-dd HH:mm:ss.fff"` |
| Auto destructuring | Disabled |
| JSON non-ASCII escaping | Preserve original characters |
| JSON indentation | Compact |

## FilterConfig

Each sink has an independent `FilterConfig`. Execution order:
1. Level check: `LogMinLevel ≤ level ≤ LogMaxLevel`
2. Include: context must match at least one prefix (empty = all pass)
3. Exclude: context matching any prefix is dropped

```csharp
// Level filter
new FilterConfig { LogMinLevel = LogLevel.Warning }

// Context include
new FilterConfig
{
    ContextFilterIncludes = ["Payment", "Order"],
    LogMinLevel = LogLevel.Debug,
}

// Context exclude
new FilterConfig
{
    ContextFilterExcludes = ["Proxy", "Heartbeat"],
}
```

## Console Sink

```csharp
builder.AddConsoleSink(
    toJson:  false,           // JSON output
    isColor: true,            // ANSI colors (auto-disabled when redirected)
    FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info },
    textOutputIncludeConfig: new TextOutputIncludeConfig
    {
        IncludeTimestamp  = true,
        IncludeLevel      = true,
        IncludeLoggerName = true,
        IncludeContext    = true,
    });
```

## File Sinks

```csharp
// Plain file (append)
builder.AddFileSink("Logs/app.log");

// Daily rotation
builder.AddTimedRotatingFileSink("Logs/app.log", maxFile: 30);

// Size-based rotation
builder.AddSizedRotatingFileSink("Logs/app.log", maxFileSizeMB: 10, maxFile: 5);

// Combined (size + daily)
builder.AddRotatingFileSink("Logs/app.log",
    maxFileSizeMB: 50,
    rotateOnNewDay: true,
    maxFile: 10);
```

The same path cannot be held by two active `FileTarget` instances simultaneously.  
Error/Critical entries trigger an immediate flush; all others flush every 10 seconds.

## Channel Sinks

```csharp
// String channel
builder.AddStringChannelSink(out ChannelReader<string> reader);

// UTF-8 byte array channel (avoids UTF-16 decode overhead)
builder.AddUtf8ByteChannelSink(out ChannelReader<byte[]> reader);

// Raw LogEntry channel
builder.AddLogEntryChannelSink(out ChannelReader<LogEntry> reader);

// Custom transform
builder.AddChannelSink(out ChannelReader<MyType> reader,
    transform: entry => new MyType(entry));
```

## ISinkConfig — Object-Based Registration

```csharp
var config = new ConsoleSinkConfig
{
    Enabled  = true,
    ToJson   = false,
    IsColor  = true,
    FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Info },
};
builder.AddSinkByConfig(config);
```

## Runtime Updates via LoggerManager

```csharp
// Disable a sink at runtime
LoggerManager.UpdateSinkConfig("MyApp", "console",
    new ConsoleSinkConfig { Enabled = false });

// Re-enable
LoggerManager.UpdateSinkConfig("MyApp", "console",
    new ConsoleSinkConfig { Enabled = true, FilterConfig = new() { LogMinLevel = LogLevel.Debug } });
```
