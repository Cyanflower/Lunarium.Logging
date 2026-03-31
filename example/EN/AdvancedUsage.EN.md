# AdvancedUsage — Custom Extensions + AOT + Runtime Configuration

> Full code version: [RawCSharp/AdvancedUsage.EN.cs](RawCSharp/AdvancedUsage.EN.cs)

---

## Section 1: LogEntry Field Reference

`LogEntry` is an immutable log event object passed to all `ILogTarget.Emit()` calls.

| Field | Type | Description |
|-------|------|-------------|
| `LoggerName` | `string` | Logger instance name |
| `LoggerNameBytes` | `ReadOnlyMemory<byte>` | UTF-8 pre-encoded bytes of LoggerName |
| `LogLevel` | `LogLevel` | Log level |
| `Timestamp` | `DateTimeOffset` | Timestamp (according to GlobalConfigurator time zone setting) |
| `Message` | `string` | Raw message template string (not yet rendered) |
| `Properties` | `object?[]` | Array of message template property values |
| `Context` | `string` | Context string (set via ForContext) |
| `ContextBytes` | `ReadOnlyMemory<byte>` | UTF-8 pre-encoded bytes of Context |
| `Scope` | `string` | MEL Scope information |
| `Exception` | `Exception?` | Associated exception |
| `MessageTemplate` | `MessageTemplate` | Lazily parsed template (filled by Sink at Emit time) |

> `MessageTemplate.MessageTemplateTokens` is `public`, available to custom Sinks for rendering messages and extracting properties.

---

## Section 2: Custom ILogTarget

Three extension paths are available:

| Path | Approach | Use case |
|------|----------|----------|
| A | Implement `ILogTarget` | Synchronous processing (in-memory buffer, direct writes) |
| B | Inherit `ChannelTarget<T>` | Asynchronous processing (database writes, network push) |
| C | Implement `IJsonTextTarget` / `ITextTarget` | Let FilterConfig control output format uniformly |

### Path A: Implement ILogTarget (in-memory buffer Sink)

```csharp
public sealed class InMemoryLogTarget : ILogTarget
{
    private readonly List<LogEntry> _entries = new();
    private readonly Lock _lock = new();

    public void Emit(LogEntry entry)
    {
        lock (_lock)
            _entries.Add(entry);
    }

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
var memTarget = new InMemoryLogTarget();
var logger = new LoggerBuilder()
    .SetLoggerName("Test")
    .AddSink(memTarget, filterConfig: null, name: "memory")
    .Build();
```

### Path B: Inherit ChannelTarget\<T\> (async database Sink)

Only implement `Transform(LogEntry) → T`. The base class accepts a `ChannelWriter<T>`; the caller creates the Channel and holds the Reader.

```csharp
public sealed class DatabaseLogTarget : ChannelTarget<DatabaseLogRecord>
{
    private readonly string _connectionString;

    public DatabaseLogTarget(string connectionString, ChannelWriter<DatabaseLogRecord> writer)
        : base(writer)
    {
        _connectionString = connectionString;
    }

    protected override DatabaseLogRecord Transform(LogEntry entry) =>
        new()
        {
            Timestamp = entry.Timestamp.UtcDateTime,
            Level     = entry.LogLevel.ToString(),
            Logger    = entry.LoggerName,
            Context   = entry.Context,
            Message   = entry.Message,
            Exception = entry.Exception?.ToString(),
        };
}

// Registration: caller creates the Channel, passes the Writer to the Target,
// and retains the Reader for background consumption
var channel  = Channel.CreateBounded<DatabaseLogRecord>(1000);
var dbTarget = new DatabaseLogTarget("Server=...;", channel.Writer);
var logger   = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddSink(dbTarget)
    .Build();

// Background consumer:
_ = Task.Run(async () =>
{
    await foreach (var record in channel.Reader.ReadAllAsync())
    {
        // await conn.ExecuteAsync("INSERT INTO Logs ...", record);
    }
});
```

---

## Section 3: IDestructurable — High-Performance Custom Destructuring

Writes directly into the output buffer via `DestructureHelper` (which wraps `Utf8JsonWriter`), with no intermediate string allocations. Fully AOT-compatible.

**Priority order: `IDestructurable` > `IDestructured` > `JsonSerializer` fallback (unregistered types under AOT degrade to `ToString()`)**

```csharp
public sealed class Order : IDestructurable
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
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

// Usage:
logger.Info("New order {@Order}", new Order { Id = 42, Name = "Book", Amount = 99.9m });
// JSON output: {"id":42,"name":"Book","amount":99.9}
```

**DestructureHelper common methods quick reference:**

| Method | Description |
|--------|-------------|
| `WriteStartObject()` / `WriteEndObject()` | Begin/end a JSON object |
| `WriteStartArray()` / `WriteEndArray()` | Begin/end a JSON array |
| `WriteString(key, value)` | Write a string field |
| `WriteNumber(key, int/long/double)` | Write a numeric field |
| `WriteBoolean(key, bool)` | Write a boolean field |
| `WriteNull(key)` | Write a null field |
| `WritePropertyName(key)` | Write a field name (follow with any value method) |
| `WriteStringValue(value)` | Write a string array element |
| `WriteNumberValue(int/long/double)` | Write a numeric array element |

---

## Section 4: IDestructured — Zero-Allocation Pre-Serialized Bytes

Ideal for static or immutable data: compute the UTF-8 JSON bytes ahead of time, then embed them directly into the output buffer at Emit time — no allocations whatsoever.

```csharp
public sealed class AppVersion : IDestructured
{
    // Static bytes reused for the lifetime of the process
    private static readonly byte[] _bytes =
        System.Text.Encoding.UTF8.GetBytes("""{"major":1,"minor":2,"patch":0}""");

    public ReadOnlyMemory<byte> Destructured() => _bytes;
}

// Usage:
logger.Info("Application version {@Version}", new AppVersion());
// JSON output: {"major":1,"minor":2,"patch":0}
```

---

## Section 5: AOT (Native AOT / Trimming) Configuration

The main library is marked `IsAotCompatible=true`. Under AOT, `{@Object}` destructuring requires pre-registering a Source Generated `JsonSerializerContext`; without it, types silently fall back to `ToString()` (no runtime exception).

**Step 1: Create a JsonSerializerContext**

```csharp
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(List<Order>))]
internal partial class MyAppJsonContext : JsonSerializerContext { }
```

**Step 2: Register in GlobalConfigurator**

```csharp
GlobalConfigurator.Configure()
    .UseJsonTypeInfoResolver(MyAppJsonContext.Default)
    // Combine multiple contexts:
    // .UseJsonTypeInfoResolver(
    //     JsonTypeInfoResolver.Combine(MyAppJsonContext.Default, AnotherContext.Default))
    .Apply();
```

> Types that implement `IDestructurable` or `IDestructured` are not subject to this restriction and do not need to be registered in the context.

---

## Section 6: LoggerManager — Runtime Dynamic Configuration

All Loggers created via `Build()` are automatically registered with `LoggerManager`. Sink configuration can be updated at runtime without restarting the application.

```csharp
// View currently registered Logger names
var names = LoggerManager.GetLoggerList();
// Returns: ["Runtime", "Analytics", ...]

// Update a single Sink's FilterConfig
LoggerManager.UpdateSinkConfig(
    loggerName: "Runtime",
    sinkName:   "console",   // name specified at build time via the sinkName parameter
    sinkConfig: new ConsoleSinkConfig
    {
        Enabled      = true,
        FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Debug },
    });

// Bulk-update all Sinks for a single Logger via LoggerConfig
LoggerManager.UpdateLoggerConfig(loggerConfigs: new LoggerConfig { ... });

// Bulk-update all registered Loggers (used when hot-reloading a config file)
LoggerManager.UpdateAllLoggerConfig(newLoggerConfigs);
```

**Notes:**

- `UpdateSinkConfig` / `UpdateLoggerConfig` only update the `FilterConfig` and `Enabled` flag of existing Sinks — they cannot add or remove Sinks (the Sink list is fixed at `Build()` time)
- Parameters that affect Target construction (e.g., `LogFilePath`) cannot be hot-updated; a restart is required
- All methods are thread-safe (`Interlocked.Exchange` atomic operations)
