# QuickStart — Lunarium.Logging

> Full code version: [RawCSharp/QuickStart.EN.cs](RawCSharp/QuickStart.EN.cs)

---

## Installation

```xml
<PackageReference Include="Lunarium.Logging" Version="*" />
```

---

## Step 1: Create a Logger

```csharp
// GlobalConfigurator.Configure() is optional; if omitted, Build() automatically applies default values.
// The Logger should be held as a global singleton — created once at startup
// and shared via DI or a static property.
public static readonly ILogger Logger = new LoggerBuilder()
    .SetLoggerName("MyApp")   // Logger name shown in log output (optional)
    .AddConsoleSink()         // Simplest console Sink
    .Build();
```

> For more Sink types and configuration options, see [SinkConfiguration.EN.md](SinkConfiguration.EN.md).

---

## Step 2: Write Logs

Five levels from lowest to highest: `Debug < Info < Warning < Error < Critical`

The default `FilterConfig.LogMinLevel = Info`, so `Debug` entries are filtered out.

```csharp
Logger.Debug("Debug message — filtered out by default Info-level Sink");
Logger.Info("Service started successfully on port {Port}", 8080);
Logger.Warning("Memory usage exceeded {Percent}%, please monitor", 85);
Logger.Error("Database connection failed, retry attempt {Attempt}", 3);
Logger.Critical("Disk space critically low, system will halt");
```

---

## Step 3: Log Exceptions

All levels support an `Exception?` parameter overload; exception details are appended at the end of the log output.

```csharp
try { /* ... */ }
catch (Exception ex)
{
    Logger.Error(ex, "Error while processing order {OrderId}", 42);
}
```

---

## Step 4: Message Template Syntax Quick Reference

Property values are filled in order via `params object?[]`.

| Syntax | Description | Output example |
|--------|-------------|----------------|
| `{Property}` | Basic substitution (ToString) | `User 1001 logged in` |
| `{@Object}` | Destructure: serialize as JSON | `{"Id":42,"Amount":99.9}` |
| `{$Value}` | Stringify: force ToString | `Active` (enum name rather than integer) |
| `{Value,10}` | Right-align 10 characters | `[      INFO]` |
| `{Value,-10}` | Left-align 10 characters | `[INFO      ]` |
| `{Value:F2}` | Format string | `1234.50` |
| `{Value,10:F2}` | Alignment + format (order is fixed) | `[   1234.50]` |
| `{{ }}` | Literal brace escaping | `{will not be parsed}` |
| `{Name}` with `null` | Null value | `(null)` |

```csharp
Logger.Info("User {UserId} logged in", 1001);
Logger.Info("New order {@Order}", new { Id = 42, Amount = 99.9m });
Logger.Info("Status: {$Status}", MyEnum.Active);
Logger.Info("[{Level,8}] message", "INFO");
Logger.Info("Amount: {Amount:F2}", 1234.5);
Logger.Info("Literal: {{not parsed}}");
```

> Property name rules: first character must be a letter or `_`; subsequent characters may include letters, digits, underscores, or dots (a dot cannot be followed by a digit or another dot).

---

## Step 5: ForContext — Attach a Context Prefix

```csharp
// Specify by string
var orderLogger = Logger.ForContext("Order.Service");
orderLogger.Info("Processing order {OrderId}", 42);
// Context field: Order.Service

// Specify by type (uses the class name)
var payLogger = Logger.ForContext<PaymentService>();
payLogger.Info("Payment deducted successfully");
// Context field: PaymentService

// Multi-level nesting (auto-joined with dots, flattened to root Logger)
var child = Logger.ForContext("App").ForContext("Module");
child.Info("Message");
// Context field: App.Module
```

`ForContext()` returns a lightweight `LoggerWrapper`. Context/ContextBytes are pre-computed once at construction time; subsequent Log calls incur zero additional allocations.

---

## LoggerName vs. Context

| Field | When set | How it changes | Typical value |
|-------|----------|----------------|---------------|
| `LoggerName` | `.SetLoggerName()` at `Build()` | Fixed, immutable | `"Runtime"` (service level) |
| `Context` | Dynamically via `ForContext()` | Supports multi-level nesting | `"Payment.Processor"` (module level) |

---

## Lifecycle Considerations

- A Logger is a process-level singleton; a given `LoggerName` can only be `Build()` once
- `GlobalConfigurator.Configure()` can only be called once per process (protected by a configuration lock)
- Call `logger.Dispose()` on application shutdown to ensure all remaining entries in the Channel are flushed
