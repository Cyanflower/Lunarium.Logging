# Advanced Usage

> Full runnable example: [AdvancedUsage.EN.cs](https://github.com/Cyanflower/Lunarium.Logging/blob/main/example/RawCSharp/EN/AdvancedUsage.EN.cs)

## Custom ILogTarget

Three extension paths:

| Path | Base | Use case |
|------|------|----------|
| A | `ILogTarget` | Synchronous, in-process (in-memory buffer, direct writes) |
| B | `ChannelTarget<T>` | Async (database writes, network push) |
| C | `IJsonTextTarget` / `ITextTarget` | Let `FilterConfig` control format uniformly |

### Path A: ILogTarget

```csharp
public sealed class InMemoryTarget : ILogTarget
{
    private readonly List<LogEntry> _entries = new();
    private readonly object _lock = new();

    public void Emit(LogEntry entry)
    {
        lock (_lock) _entries.Add(entry);
    }

    public void Dispose() { }
}

builder.AddSink(new InMemoryTarget(), cfg: new FilterConfig { LogMinLevel = LogLevel.Warning });
```

### Path B: ChannelTarget\<T\>

```csharp
public sealed class DatabaseTarget : ChannelTarget<LogRow>
{
    protected override LogRow Transform(LogEntry entry) =>
        new LogRow(entry.Timestamp, entry.LogLevel.ToString(), entry.Message);
}
```

## IDestructurable — Zero-Allocation Custom Serialization

Implement `IDestructurable` to write directly into the output buffer without intermediate strings:

```csharp
public sealed class Order : IDestructurable
{
    public int Id { get; init; }
    public decimal Amount { get; init; }

    public void Destructure(DestructureHelper helper)
    {
        helper.WriteStartObject();
        helper.WriteNumber("id", Id);
        helper.WriteNumber("amount", Amount);
        helper.WriteEndObject();
    }
}

logger.Info("New order: {@Order}", new Order { Id = 42, Amount = 99.9m });
// Output: {"id":42,"amount":99.9}
```

## IDestructured — Pre-Serialized Bytes

For static or cached objects, return pre-serialized UTF-8 JSON bytes for zero-copy embedding:

```csharp
public sealed class ServerInfo : IDestructured
{
    private static readonly ReadOnlyMemory<byte> _bytes =
        """{"host":"prod-01","region":"us-east"}"""u8.ToArray();

    public ReadOnlyMemory<byte> Destructured() => _bytes;
}
```

**Priority order** for `{@Object}`:
1. `IDestructurable` → calls `Destructure(helper)`
2. `IDestructured` → calls `Destructured()`
3. Fallback → `JsonSerializer.Serialize()`

## Native AOT

Register a source-generated `JsonSerializerContext` to enable AOT-safe `{@Object}` destructuring:

```csharp
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(User))]
internal partial class MyLogContext : JsonSerializerContext { }

GlobalConfigurator.Configure()
    .UseJsonTypeInfoResolver(MyLogContext.Default)
    .Apply();
```

Without registration, AOT falls back to `ToString()` silently. Types implementing `IDestructurable` or `IDestructured` are never affected.

## LoggerManager — Runtime Configuration

```csharp
// List all active loggers
IReadOnlyList<string> names = LoggerManager.GetLoggerList();

// Update a single sink's config at runtime
LoggerManager.UpdateSinkConfig("MyApp", "console",
    new ConsoleSinkConfig { Enabled = false });

// Reload all loggers from a new LoggingConfig
LoggerManager.UpdateAllLoggerConfig(newLoggingConfig);
```

## LogUtils

```csharp
// Get the current timestamp according to GlobalConfigurator settings
DateTimeOffset now = LogUtils.GetTimestamp();
```
