# Quick Start

> Full runnable example: [QuickStart.EN.cs](https://github.com/Cyanflower/Lunarium.Logging/blob/main/example/RawCSharp/EN/QuickStart.EN.cs)

## Install

```xml
<PackageReference Include="Lunarium.Logging" Version="*" />
```

## Create a Logger

```csharp
public static readonly ILogger Logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddConsoleSink()
    .AddFileSink("logs/app.log")
    .Build();
```

The Logger is a process-level singleton — create it once at startup and share via DI or a static field.

## Write Logs

Five levels from lowest to highest: `Debug < Info < Warning < Error < Critical`.  
The default `FilterConfig.LogMinLevel` is `Info`, so `Debug` entries are filtered out by default.

```csharp
Logger.Info("Server started on port {Port}", 8080);
// [2026-03-31 12:00:00.000] [INF] [MyApp] Server started on port 8080

Logger.Warning("High memory usage: {UsageMB} MB", 1024);
// [2026-03-31 12:00:00.000] [WRN] [MyApp] High memory usage: 1024 MB

Logger.Error("Database connection failed, attempt {Attempt}", 3);
// [2026-03-31 12:00:00.000] [ERR] [MyApp] Database connection failed, attempt 3
```

## Log Exceptions

All levels support an `Exception?` parameter overload — exception details are appended to the output.

```csharp
try { /* ... */ }
catch (Exception ex)
{
    Logger.Error(ex, "Failed to process order {OrderId}", 42);
    // [2026-03-31 12:00:00.000] [ERR] [MyApp] Failed to process order 42   (stderr)
    // System.Exception: Connection timeout
    //    at ...
}
```

## Structured Destructuring

```csharp
// {@} — render as structured JSON
Logger.Info("Order created: {@Order}", order);
// [2026-03-31 12:00:00.000] [INF] [MyApp] Order created: {"Id":42,"Total":99.99}

// {$} — force ToString()
Logger.Info("Status: {$Status}", myEnum);
// [2026-03-31 12:00:00.000] [INF] [MyApp] Status: Active

// Alignment and formatting
Logger.Info("Price: {Amount,10:F2} USD", 42.5);
// [2026-03-31 12:00:00.000] [INF] [MyApp] Price:      42.50 USD
```

## Message Template Syntax

| Syntax | Description |
|--------|-------------|
| `{Property}` | Basic substitution (`ToString`) |
| `{@Object}` | Destructure as JSON |
| `{$Value}` | Force `ToString` (e.g. enum names) |
| `{Value,10}` | Right-align 10 chars |
| `{Value,-10}` | Left-align 10 chars |
| `{Value:F2}` | Format string |
| `{Value,10:F2}` | Alignment + format (order is fixed) |
| `{{ }}` | Literal brace escape |

## ForContext — Context and Scoping

```csharp
// Attach a context by string
ILogger orderLog = Logger.ForContext("Order.Processor");
orderLog.Info("Processing order {Id}", orderId);
// [2026-03-31 12:00:00.000] [INF] [MyApp] [Order.Processor] Processing order 1001

// Or by type — uses the class name
ILogger payLog = Logger.ForContext<PaymentService>();

// Chain contexts — auto-joined with dots, flattened to root Logger
ILogger validatorLog = orderLog.ForContext("Validator");
validatorLog.Info("Validating order {Id}", orderId);
// [2026-03-31 12:00:00.000] [INF] [MyApp] [Order.Processor.Validator] Validating order 1001
```

`ForContext()` returns a lightweight `LoggerWrapper`. Context bytes are pre-computed at construction time — subsequent Log calls incur zero additional allocations.

## File Rotation

File rotation is built in — no extra packages needed:

```csharp
ILogger logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddTimedRotatingFileSink("logs/app.log", maxFile: 30)       // rotate daily
    .AddSizedRotatingFileSink("logs/app.log", maxFileSizeMB: 50) // rotate by size
    .AddRotatingFileSink("logs/app.log",                         // both strategies
        maxFileSizeMB: 50, rotateOnNewDay: true, maxFile: 30)
    .Build();
```

## Lifecycle

- `GlobalConfigurator.Configure()` must be called before the first `Build()`, and only once per process.
- Call `logger.Dispose()` on shutdown to flush all pending entries from the Channel.
