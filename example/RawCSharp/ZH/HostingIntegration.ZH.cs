// Copyright 2026 Cyanflower
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// ============================================================
//  本文件是 Lunarium.Logging.Hosting 的集成示例，仅供参考，不参与编译。
//  涵盖 Generic Host、ASP.NET Core、MEL 桥接与 DI 注入的典型用法。
//  需引用 NuGet 包：Lunarium.Logging.Hosting
// ============================================================

using Microsoft.Extensions.DependencyInjection;


using Microsoft.Extensions.Logging;

using Lunarium.Logging.Models;
using Lunarium.Logging.Target;
using Lunarium.Logging.Configuration;
using Lunarium.Logging.Extensions;
using Microsoft.Extensions.Hosting;
using Lunarium.Logging.Config.Models;

namespace Lunarium.Logging;

// ================================================================
//  第一节：IHostBuilder 集成（Generic Host / Console App）
//
//  适用场景：Host.CreateDefaultBuilder() 传统写法，
//  或 Worker Service / Console 应用。
// ================================================================
public static class HostBuilderIntegrationExample
{
    public static void Run()
    {
        var host = Host.CreateDefaultBuilder()
            // 替换 Host 的默认日志提供器，使用 Lunarium 作为唯一输出
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
    }
}

// ================================================================
//  第二节：ILoggingBuilder 集成（WebApplication / Minimal API）
//
//  适用场景：ASP.NET Core 6+ 的 WebApplication.CreateBuilder() 写法。
// ================================================================
public static class WebApplicationIntegrationExample
{
    public static void Configure()
    {
        // var builder = WebApplication.CreateBuilder(args);

        // 清除默认提供器（可选，推荐，避免控制台重复输出）
        // builder.Logging.ClearProviders();

        // 注册 Lunarium 作为 MEL 提供器
        // builder.Logging.UseLunariumLog(
        //     configureSinks: b => b
        //         .SetLoggerName("WebApp")
        //         .AddConsoleSink()
        //         .AddSizedRotatingFileSink("Logs/web.log", maxFileSizeMB: 20, maxFile: 5),
        //     configureGlobal: g => g
        //         .UseCustomTimezone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai")));

        // var app = builder.Build();
        // app.Run();
    }
}

// ================================================================
//  第三节：注册已有 Logger 实例（AddLunariumLogger）
//
//  适用场景：Logger 在应用启动时已通过代码配置好（如读取数据库配置），
//  需要将其注入 DI 容器供 ILogger<T> 使用。
// ================================================================
public static class AddExistingLoggerExample
{
    public static void Configure()
    {
        // 在应用启动逻辑中自行构建 Logger
        var logger = new LoggerBuilder()
            .SetLoggerName("MyApp")
            .AddConsoleSink()
            .AddSizedRotatingFileSink("Logs/app.log", maxFileSizeMB: 10, maxFile: 5)
            .Build();

        // var builder = WebApplication.CreateBuilder(args);
        // builder.Logging.ClearProviders();
        // builder.Logging.AddLunariumLogger(logger);
        //
        // ⚠️ AddLunariumLogger 不接管 Logger 的生命周期。
        //    Logger 由调用方管理（创建者负责 Dispose）。
        //
        // var app = builder.Build();
        // app.Run();
    }
}

// ================================================================
//  第四节：在 DI 中使用 ILogger<T>
//
//  注册后，Lunarium 作为 MEL 提供器工作，通过构造函数注入 ILogger<T> 即可。
//  Category（类名）会映射为 Lunarium 的 Context 字段。
// ================================================================
public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    // 通过 DI 注入 ILogger<OrderService>
    // Category 为 "OrderService"，在 Lunarium 日志中显示为 Context
    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }

    public void ProcessOrder(int orderId)
    {
        _logger.LogInformation("开始处理订单 {OrderId}", orderId);

        try
        {
            // ...
            _logger.LogInformation("订单 {OrderId} 处理完成", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理订单 {OrderId} 失败", orderId);
            throw;
        }
    }
}

// ================================================================
//  第五节：ToMicrosoftLogger — Lunarium ILogger 转换为 MEL ILogger
//
//  适用场景：第三方库或框架只接受 Microsoft.Extensions.Logging.ILogger，
//  但你希望底层仍走 Lunarium 的日志管道。
// ================================================================
public static class ToMicrosoftLoggerExample
{
    public static void Example()
    {
        var lunariumLogger = new LoggerBuilder()
            .SetLoggerName("MyApp")
            .AddConsoleSink()
            .Build();

        // 转换为 MEL ILogger，category 参数显示为 Context
        Microsoft.Extensions.Logging.ILogger melLogger =
            lunariumLogger.ToMicrosoftLogger("ThirdPartyComponent");

        // 传给只认 MEL ILogger 的第三方库
        // thirdPartyService.SetLogger(melLogger);
    }
}

// ================================================================
//  第六节：Scope（作用域）
//
//  MEL 的 ILogger.BeginScope() 产生的作用域信息会被 Lunarium 捕获，
//  存入 LogEntry.Scope 字段，在 JSON Sink 中作为 "scope" 字段输出。
//  文本 Sink 不输出 Scope（可在自定义 ILogTarget 中读取）。
// ================================================================
public class ScopeExample
{
    private readonly ILogger<ScopeExample> _logger;

    public ScopeExample(ILogger<ScopeExample> logger)
    {
        _logger = logger;
    }

    public void HandleRequest(string requestId)
    {
        // BeginScope 的值会在当前作用域内的所有日志中附带
        using (_logger.BeginScope("RequestId={RequestId}", requestId))
        {
            _logger.LogInformation("开始处理请求");
            // JSON 输出中 scope 字段：RequestId=abc-123
            DoWork();
        }
    }

    private void DoWork()
    {
        // ...
    }
}

// ================================================================
//  第七节：生命周期注意事项
//
//  • LunariumLoggerProvider.Dispose() 不销毁它持有的 Logger。
//    Logger 的生命周期由创建者管理（谁 new 谁负责 Dispose）。
//
//  • UseLunariumLog(configureSinks, ...) 内部创建 Logger 并注册为
//    IHostedService，Host 停止时由框架自动 Dispose。
//
//  • AddLunariumLogger(logger) 注册的 Logger 需要调用方在应用关闭时手动 Dispose：
//
//    app.Lifetime.ApplicationStopped.Register(() => logger.Dispose());
//
//  • 同一进程内同一 LoggerName 只能 Build() 一次（全局单例锁保护）。
//    多个 ILoggingBuilder 注册同名 Logger 会在第二次 Build 时抛出异常。
// ================================================================

// 占位符：示例中引用的类型
file class MyWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
