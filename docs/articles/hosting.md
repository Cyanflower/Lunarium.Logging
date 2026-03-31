# Hosting & DI

> Full runnable example: [HostingIntegration.EN.cs](https://github.com/Cyanflower/Lunarium.Logging/blob/main/example/RawCSharp/EN/HostingIntegration.EN.cs)  
> Required package: `Lunarium.Logging.Hosting`

```xml
<PackageReference Include="Lunarium.Logging.Hosting" Version="*" />
```

## IHostBuilder (Worker Service / Console Host)

```csharp
var host = Host.CreateDefaultBuilder()
    .UseLunariumLog(
        configureSinks: builder => builder
            .SetLoggerName("MyApp")
            .AddConsoleSink()
            .AddSizedRotatingFileSink("Logs/app.log", maxFileSizeMB: 10, maxFile: 5),
        configureGlobal: global => global
            .UseCustomTimezone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai")))
    .ConfigureServices((ctx, services) =>
    {
        services.AddHostedService<MyWorker>();
    })
    .Build();

host.Run();
```

## ILoggingBuilder (ASP.NET Core / Minimal API)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders(); // remove default providers
builder.Logging.UseLunariumLog(
    configureSinks: b => b
        .SetLoggerName("WebApp")
        .AddConsoleSink()
        .AddSizedRotatingFileSink("Logs/web.log", maxFileSizeMB: 20, maxFile: 5));

var app = builder.Build();
app.Run();
```

## Using ILogger\<T\> from DI

```csharp
public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }

    public void Process(int orderId)
    {
        _logger.LogInformation("Processing order {OrderId}", orderId);
    }
}
```

The category name (`OrderService`) is passed as the `Context` field in log output.

## Scopes

```csharp
using (_logger.BeginScope("RequestId:{RequestId}", requestId))
{
    _logger.LogInformation("Handling request");
    // Scope info appears in the Scope field of each entry within the using block
}
```

## Register an Existing Logger

For cases where the logger is built before the host:

```csharp
var myLogger = new LoggerBuilder()
    .SetLoggerName("Runtime")
    .AddConsoleSink()
    .Build();

builder.Logging.AddLunariumLogger(myLogger, categoryName: "Runtime");
```

## Convert to Microsoft ILogger

```csharp
// Wrap a Lunarium ILogger as Microsoft.Extensions.Logging.ILogger
Microsoft.Extensions.Logging.ILogger msLogger =
    lunariumLogger.ToMicrosoftLogger("MyCategory");
```
