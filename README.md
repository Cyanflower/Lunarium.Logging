# Lunarium.Logging

[![Build](https://github.com/Cyanflower/Lunarium.Logging/actions/workflows/ci.yml/badge.svg)](https://github.com/Cyanflower/Lunarium.Logging/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Lunarium.Logging.svg)](https://www.nuget.org/packages/Lunarium.Logging)
[![Coverage](https://img.shields.io/badge/coverage-92%25-brightgreen)](https://github.com/Cyanflower/Lunarium.Logging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

A lightweight, high-performance structured logging library for .NET — zero external dependencies, zero hot-path allocations, and native AOT compatible.

> Designed for developers who want structured message template logging without pulling in multiple packages or wrestling with complex sink configurations.

---

## Why Lunarium.Logging?

Many structured logging libraries split basic functionality across multiple packages — separate installs for the core, console sink, file sink, and formatters. Lunarium.Logging ships everything you need for everyday logging in a single, self-contained package with no external dependencies and no assembly sprawl. The optional extension packages (`Hosting`, `Configuration`, `Http`) are genuinely optional — the core library works on its own.

---

## Highlights

- **Zero dependencies** — The core library is complete on its own. Console, File, and Channel sinks are all built in. No extra packages required for everyday use.
- **Zero hot-path allocations** — Filter cache hits at ~9 ns, template cache hits at ~11–19 ns, full log call at ~186–212 ns. See [benchmarks](#performance) for details.
- **Native AOT compatible** — First-class AOT and trimming support throughout. Register a source-generated `JsonSerializerContext` for `{@Object}` destructuring, or implement `IDestructurable`/`IDestructured` for fully reflection-free structured output.
- **Structured message templates** — `{Property}`, `{@Object}`, `{Value,10:F2}` syntax with alignment, formatting, and destructuring support.
- **Simple, intuitive API** — Fluent builder, sensible defaults, no ceremony.

---

## Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| `Lunarium.Logging` | Core library — structured logging, sinks, filters | [![NuGet](https://img.shields.io/nuget/v/Lunarium.Logging.svg)](https://www.nuget.org/packages/Lunarium.Logging) |
| `Lunarium.Logging.Hosting` | `IHostBuilder` / `ILoggingBuilder` integration, MEL bridge | [![NuGet](https://img.shields.io/nuget/v/Lunarium.Logging.Hosting.svg)](https://www.nuget.org/packages/Lunarium.Logging.Hosting) |
| `Lunarium.Logging.Configuration` | `appsettings.json` integration with hot-reload support | [![NuGet](https://img.shields.io/nuget/v/Lunarium.Logging.Configuration.svg)](https://www.nuget.org/packages/Lunarium.Logging.Configuration) |
| `Lunarium.Logging.Http` | HTTP batch sink — supports Seq (CLEF), Loki, and custom endpoints | [![NuGet](https://img.shields.io/nuget/v/Lunarium.Logging.Http.svg)](https://www.nuget.org/packages/Lunarium.Logging.Http) |

---

## Quick Start

```sh
dotnet add package Lunarium.Logging
```

```csharp
using Lunarium.Logging;

ILogger logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddConsoleSink()
    .AddFileSink("logs/app.log")
    .Build();

logger.Info("Server started on port {Port}", 8080);
// [2026-03-31 12:00:00.000] [INF] [MyApp] Server started on port 8080

logger.Warning("High memory usage: {UsageMB} MB", 1024);
// [2026-03-31 12:00:00.000] [WRN] [MyApp] High memory usage: 1024 MB

logger.Error(ex, "Request failed for user {UserId}", userId);
// [2026-03-31 12:00:00.000] [ERR] [MyApp] Request failed for user 42   (stderr)
// System.Exception: Connection timeout
//    at ...
```

File rotation is built in — no extra packages needed:

```csharp
ILogger logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddTimedRotatingFileSink("logs/app.log", maxFile: 30)        // rotate daily
    .AddSizedRotatingFileSink("logs/app.log", maxFileSizeMB: 50)  // rotate by size
    .AddRotatingFileSink("logs/app.log",                          // both strategies
        maxFileSizeMB: 50, rotateOnNewDay: true, maxFile: 30)
    .Build();
```

### Structured Destructuring

```csharp
// {@} — render as structured JSON
logger.Info("Order created: {@Order}", order);
// [2026-03-31 12:00:00.000] [INF] [MyApp] Order created: {"Id":42,"Total":99.99}

// {$} — force ToString()
logger.Info("Status: {$Status}", myEnum);
// [2026-03-31 12:00:00.000] [INF] [MyApp] Status: Active

// Alignment and formatting
logger.Info("Price: {Amount,10:F2} USD", 42.5);
// [2026-03-31 12:00:00.000] [INF] [MyApp] Price:      42.50 USD
```

### Context and Scoping

```csharp
// Attach a source context by string
ILogger orderLog = logger.ForContext("Order.Processor");
orderLog.Info("Processing order {Id}", orderId);
// [2026-03-31 12:00:00.000] [INF] [MyApp] [Order.Processor] Processing order 1001

// Or by type — uses the class name as the context string
ILogger payLog = logger.ForContext<PaymentService>();

// Chain contexts: "Order.Processor" → ForContext("Validator") = "Order.Processor.Validator"
ILogger validatorLog = orderLog.ForContext("Validator");
validatorLog.Info("Validating order {Id}", orderId);
// [2026-03-31 12:00:00.000] [INF] [MyApp] [Order.Processor.Validator] Validating order 1001
```

![Colored console output](assets/ConsoleSample.png)

Use it standalone as shown above, or integrate it into ASP.NET Core / Generic Host as a full drop-in replacement for the default `Microsoft.Extensions.Logging` provider — see [Integration with Generic Host](#integration-with-generic-host).

---

## Integration with Generic Host

```sh
dotnet add package Lunarium.Logging.Hosting
```

```csharp
Host.CreateDefaultBuilder(args)
    .UseLunariumLog(sinks => sinks
        .AddConsoleSink()
        .AddFileSink("logs/app.log"))
    .Build()
    .Run();
```

Use `ILogger<T>` from DI as usual — Lunarium.Logging acts as the MEL provider.

---

## appsettings.json Configuration

```sh
dotnet add package Lunarium.Logging.Configuration
```

```json
{
  "LunariumLogging": {
    "GlobalConfig": {
      "TextTimestampMode": "Custom",
      "TextCustomTimestampFormat": "yyyy-MM-dd HH:mm:ss.fff"
    },
    "LoggerConfigs": [
      {
        "LoggerName": "MyApp",
        "ConsoleSinks": {
          "main-console": {
            "IsColor": true,
            "FilterConfig": {
              "LogMinLevel": "Info"
            }
          }
        },
        "FileSinks": {
          "app": {
            "LogFilePath": "Logs/app.log",
            "MaxFileSizeMB": 10.0,
            "RotateOnNewDay": true,
            "MaxFile": 30,
            "ToJson": true
          }
        }
      }
    ]
  }
}
```

Supports **hot-reload** — filter changes apply at runtime without restarting.

---

## HTTP Sinks (Seq / Loki)

```sh
dotnet add package Lunarium.Logging.Http
```

```csharp
// Seq (CLEF format)
new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddSeqSink(
        httpClient:    new HttpClient(),
        seqEndpoint:   "http://localhost:5341/api/events/raw",
        apiKey:        "your-seq-api-key")
    .Build();

// Loki
new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddLokiSink(
        httpClient:    new HttpClient(),
        lokiEndpoint:  "http://localhost:3100/loki/api/v1/push",
        labels:        new Dictionary<string, string>
        {
            ["app"]         = "my-service",
            ["environment"] = "production",
        })
    .Build();
```

Provides lightweight HTTP sinks using native HttpClient for batching logs to Seq (CLEF format) or Grafana Loki.

---

## Performance

Benchmarks run on i7-8750H, .NET 10.0, Release mode.

**Hot path (filter + parser cache hits)**

| Scenario | Time | Allocations |
|----------|------|-------------|
| Filter cache hit | ~9 ns | 0 |
| Template cache hit | ~11–19 ns | 0 |
| Full log call (no properties) | ~186 ns | 113 B |
| Full log call (5 properties) | ~212 ns | 227 B |

**Writer rendering**

| Format | Time | Allocations |
|--------|------|-------------|
| Plain text | ~320–600 ns | 32 B |
| JSON | ~470–750 ns | 64 B |

The 32–64 B allocation in rendering comes from the `WriterPool` return path, not per-call heap pressure. Filter and parser caches operate at zero allocation on the hot path.

For detailed analysis and cross-platform comparisons, see the benchmark reports:
- [Performance Analysis](BenchmarkReports/Latest/EN/PerformanceAnalysis.md)
- [Platform Differences](BenchmarkReports/Latest/EN/PlatformDifferences.md)

---

## Examples

Full annotated examples are available in the [`example/`](example/) directory, each provided as a Markdown guide and a raw C# file.

| Example | Description |
|---------|-------------|
| [Quick Start](example/EN/QuickStart.EN.md) | Log levels, exceptions, message template syntax, `ForContext` |
| [Sink Configuration](example/EN/SinkConfiguration.EN.md) | All sink types, `FilterConfig`, `ISinkConfig`, `GlobalConfigurator` |
| [Hosting Integration](example/EN/HostingIntegration.EN.md) | Generic Host, DI, MEL bridge, `UseLunariumLog` |
| [Configuration Integration](example/EN/ConfigurationIntegration.EN.md) | `appsettings.json` binding, hot-reload |
| [HTTP Sink](example/EN/HttpSink.EN.md) | Seq, Loki, custom serializers, `AddHttpSink` |
| [Advanced Usage](example/EN/AdvancedUsage.EN.md) | Custom `ILogTarget`, `IDestructurable`/`IDestructured`, AOT, `LoggerManager` |

Raw C# source files (no Markdown) are in [`example/RawCSharp/`](example/RawCSharp/).

---

## Native AOT

Lunarium.Logging is marked `IsAotCompatible=true` and passes AOT/trimming analysis at build time.

`{@Object}` destructuring relies on `JsonSerializer` under the hood. In AOT environments, register a source-generated `JsonSerializerContext` so serialization is fully reflection-free. Types not registered will silently fall back to `ToString()` — no runtime exception is thrown.

**Step 1 — declare a `JsonSerializerContext` for your types:**

```csharp
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(UserRecord))]
[JsonSerializable(typeof(List<Order>))]
internal partial class MyAppJsonContext : JsonSerializerContext { }
```

**Step 2 — register it before calling `Build()`:**

```csharp
GlobalConfigurator.Configure()
    .UseJsonTypeInfoResolver(MyAppJsonContext.Default)
    .Apply();

// Multiple contexts can be combined:
// .UseJsonTypeInfoResolver(
//     JsonTypeInfoResolver.Combine(MyAppJsonContext.Default, AnotherContext.Default))
```

Alternatively, implement `IDestructurable` or `IDestructured` for structured output that requires no serializer at all.

---

## Requirements

- .NET 8, 9, or 10
- No external NuGet dependencies (core library)

---

## Test Coverage

776 tests across three test projects.

| Project | Tests |
|---------|------:|
| `Lunarium.Logging.Tests` | 653 |
| `Lunarium.Logging.Http.Tests` | 102 |
| `Lunarium.Logging.IntegrationTests` | 21 |

| Metric | Coverage |
|--------|--------:|
| Line | 92.1% |
| Branch | 88.8% |
| Method | 98.7% |

Per-package breakdown:

| Package | Line Coverage |
|---------|-------------:|
| `Lunarium.Logging` | 91.3% |
| `Lunarium.Logging.Hosting` | 100% |
| `Lunarium.Logging.Configuration` | 98.1% |
| `Lunarium.Logging.Http` | 92.8% |

---

## Attributions

The message template parsing logic in [`src/Lunarium.Logging/Parser/LogParser.cs`](src/Lunarium.Logging/Parser/LogParser.cs) is an original state-machine implementation, but is conceptually inspired by the design of [Serilog](https://github.com/serilog/serilog). See [`ATTRIBUTIONS.md`](ATTRIBUTIONS.md) for full details.

---

## License

Apache 2.0 — see [LICENSE](LICENSE).
