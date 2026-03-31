# HostingIntegration — Generic Host + DI + MEL Bridge

> Full code version: [RawCSharp/HostingIntegration.EN.cs](RawCSharp/HostingIntegration.EN.cs)
> Required NuGet package: `Lunarium.Logging.Hosting`

---

## Section 1: IHostBuilder Integration (Generic Host / Worker Service)

```csharp
var host = Host.CreateDefaultBuilder()
    .UseLunariumLog(
        configureSinks: builder => builder
            .SetLoggerName("MyApp")
            .AddConsoleSink()
            .AddSizedRotatingFileSink("Logs/app.log", maxFileSizeMB: 10, maxFile: 5),
        configureGlobal: global => global
            .UseCustomTimezone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"))
            .UseJsonISO8601Timestamp())
    .ConfigureServices((ctx, services) =>
    {
        services.AddHostedService<MyWorker>();
    })
    .Build();

host.Run();
```

---

## Section 2: ILoggingBuilder Integration (WebApplication / Minimal API)

For the `WebApplication.CreateBuilder()` pattern used in ASP.NET Core 6+.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Clear default providers (recommended to avoid duplicate console output)
builder.Logging.ClearProviders();

// Register Lunarium as the MEL provider
builder.Logging.UseLunariumLog(
    configureSinks: b => b
        .SetLoggerName("WebApp")
        .AddConsoleSink()
        .AddSizedRotatingFileSink("Logs/web.log", maxFileSizeMB: 20, maxFile: 5),
    configureGlobal: g => g
        .UseCustomTimezone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai")));

var app = builder.Build();
app.Run();
```

---

## Section 3: Register an Existing Logger Instance (AddLunariumLogger)

For scenarios where the Logger has already been built via custom logic at application startup.

```csharp
// Build the Logger manually (can read from database config, environment variables, etc.)
var logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddConsoleSink()
    .Build();

builder.Logging.ClearProviders();
builder.Logging.AddLunariumLogger(logger);

// ⚠️ AddLunariumLogger does NOT take ownership of the Logger's lifetime;
//    the caller is responsible for Dispose:
// app.Lifetime.ApplicationStopped.Register(() => logger.Dispose());
```

---

## Section 4: Using ILogger\<T\> in DI

Once registered, inject `ILogger<T>` via constructor injection. The class name of `T` maps to Lunarium's `Context` field.

```csharp
public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }

    public void ProcessOrder(int orderId)
    {
        _logger.LogInformation("Starting to process order {OrderId}", orderId);
        // Context field: OrderService

        try { /* ... */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order {OrderId}", orderId);
            throw;
        }
    }
}
```

---

## Section 5: ToMicrosoftLogger — Convert to MEL ILogger

For third-party libraries that only accept `Microsoft.Extensions.Logging.ILogger`.

```csharp
var lunariumLogger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddConsoleSink()
    .Build();

// Convert to MEL ILogger; the category string appears as Context
Microsoft.Extensions.Logging.ILogger melLogger =
    lunariumLogger.ToMicrosoftLogger("ThirdPartyComponent");

// Pass to a third-party library that only accepts MEL ILogger
// thirdPartyService.SetLogger(melLogger);
```

---

## Section 6: Scope

Scope information produced by MEL's `ILogger.BeginScope()` is captured, stored in the `LogEntry.Scope` field, and output as a `"scope"` field in JSON Sinks.

```csharp
public void HandleRequest(string requestId)
{
    using (_logger.BeginScope("RequestId={RequestId}", requestId))
    {
        _logger.LogInformation("Started handling request");
        // JSON output scope field: RequestId=abc-123
    }
}
```

---

## Section 7: Lifecycle Considerations

| Registration method | Logger lifetime |
|---------------------|-----------------|
| `UseLunariumLog(configureSinks, ...)` | Created internally by the framework; automatically Disposed when the Host stops |
| `AddLunariumLogger(logger)` | Created by the caller; the caller is responsible for Dispose |

```csharp
// Manual Dispose for the AddLunariumLogger scenario:
app.Lifetime.ApplicationStopped.Register(() => logger.Dispose());
```

> A given `LoggerName` can only be `Build()` once within the same process.
> Registering a second Logger with the same name will throw an `InvalidOperationException` on the second `Build()` call.
