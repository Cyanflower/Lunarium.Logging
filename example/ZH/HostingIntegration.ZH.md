# HostingIntegration — Generic Host + DI + MEL 桥接

> 完整代码版本：[RawCSharp/HostingIntegration.ZH.cs](RawCSharp/HostingIntegration.ZH.cs)
> 需引用 NuGet 包：`Lunarium.Logging.Hosting`

---

## 第一节：IHostBuilder 集成（Generic Host / Worker Service）

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

## 第二节：ILoggingBuilder 集成（WebApplication / Minimal API）

适用于 ASP.NET Core 6+ 的 `WebApplication.CreateBuilder()` 写法。

```csharp
var builder = WebApplication.CreateBuilder(args);

// 清除默认提供器（推荐，避免控制台重复输出）
builder.Logging.ClearProviders();

// 注册 Lunarium 作为 MEL 提供器
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

## 第三节：注册已有 Logger 实例（AddLunariumLogger）

适用于 Logger 在应用启动时已通过自定义逻辑构建好的场景。

```csharp
// 自行构建 Logger（可读取数据库配置、环境变量等）
var logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddConsoleSink()
    .Build();

builder.Logging.ClearProviders();
builder.Logging.AddLunariumLogger(logger);

// ⚠️ AddLunariumLogger 不接管 Logger 的生命周期，由调用方负责 Dispose：
// app.Lifetime.ApplicationStopped.Register(() => logger.Dispose());
```

---

## 第四节：在 DI 中使用 ILogger\<T\>

注册后，通过构造函数注入 `ILogger<T>` 即可。`T` 的类名会映射为 Lunarium 的 `Context` 字段。

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
        _logger.LogInformation("开始处理订单 {OrderId}", orderId);
        // Context 字段：OrderService

        try { /* ... */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理订单 {OrderId} 失败", orderId);
            throw;
        }
    }
}
```

---

## 第五节：ToMicrosoftLogger — 转换为 MEL ILogger

适用于只接受 `Microsoft.Extensions.Logging.ILogger` 的第三方库。

```csharp
var lunariumLogger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddConsoleSink()
    .Build();

// 转换为 MEL ILogger，category 显示为 Context
Microsoft.Extensions.Logging.ILogger melLogger =
    lunariumLogger.ToMicrosoftLogger("ThirdPartyComponent");

// 传给只认 MEL ILogger 的第三方库
// thirdPartyService.SetLogger(melLogger);
```

---

## 第六节：Scope（作用域）

MEL 的 `ILogger.BeginScope()` 产生的作用域信息会被捕获，存入 `LogEntry.Scope` 字段，在 JSON Sink 中作为 `"scope"` 字段输出。

```csharp
public void HandleRequest(string requestId)
{
    using (_logger.BeginScope("RequestId={RequestId}", requestId))
    {
        _logger.LogInformation("开始处理请求");
        // JSON 输出中 scope 字段：RequestId=abc-123
    }
}
```

---

## 第七节：生命周期注意事项

| 注册方式 | Logger 生命周期 |
|----------|-----------------|
| `UseLunariumLog(configureSinks, ...)` | 框架内部创建，Host 停止时自动 Dispose |
| `AddLunariumLogger(logger)` | 调用方创建，调用方负责 Dispose |

```csharp
// AddLunariumLogger 场景的手动 Dispose：
app.Lifetime.ApplicationStopped.Register(() => logger.Dispose());
```

> 同一进程内同一 `LoggerName` 只能 `Build()` 一次。
> 多处注册同名 Logger 会在第二次 `Build()` 时抛出 `InvalidOperationException`。
