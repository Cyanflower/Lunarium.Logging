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
//  This file is an integration example for Lunarium.Logging.Hosting.
//  For reference only — not compiled into the library.
//  Covers Generic Host, ASP.NET Core, MEL bridge, and DI injection.
//  Required NuGet package: Lunarium.Logging.Hosting
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
//  Section 1: IHostBuilder Integration (Generic Host / Console App)
//
//  Use case: traditional Host.CreateDefaultBuilder() pattern,
//  or Worker Service / Console applications.
// ================================================================
public static class HostBuilderIntegrationExample
{
    public static void Run()
    {
        var host = Host.CreateDefaultBuilder()
            // Replace the Host's default logging providers with Lunarium as the sole output
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
//  Section 2: ILoggingBuilder Integration (WebApplication / Minimal API)
//
//  Use case: ASP.NET Core 6+ WebApplication.CreateBuilder() pattern.
// ================================================================
public static class WebApplicationIntegrationExample
{
    public static void Configure()
    {
        // var builder = WebApplication.CreateBuilder(args);

        // Clear default providers (optional but recommended to avoid duplicate console output)
        // builder.Logging.ClearProviders();

        // Register Lunarium as the MEL provider
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
//  Section 3: Register an Existing Logger Instance (AddLunariumLogger)
//
//  Use case: the Logger has already been configured in code at startup
//  (e.g., reading from a database), and needs to be injected into
//  the DI container for use with ILogger<T>.
// ================================================================
public static class AddExistingLoggerExample
{
    public static void Configure()
    {
        // Build the Logger manually in application startup logic
        var logger = new LoggerBuilder()
            .SetLoggerName("MyApp")
            .AddConsoleSink()
            .AddSizedRotatingFileSink("Logs/app.log", maxFileSizeMB: 10, maxFile: 5)
            .Build();

        // var builder = WebApplication.CreateBuilder(args);
        // builder.Logging.ClearProviders();
        // builder.Logging.AddLunariumLogger(logger);
        //
        // ⚠️ AddLunariumLogger does NOT take ownership of the Logger's lifetime.
        //    The Logger is managed by whoever created it (creator is responsible for Dispose).
        //
        // var app = builder.Build();
        // app.Run();
    }
}

// ================================================================
//  Section 4: Using ILogger<T> in DI
//
//  Once registered, Lunarium works as the MEL provider.
//  Inject ILogger<T> via constructor — the category (class name)
//  maps to Lunarium's Context field.
// ================================================================
public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    // Inject ILogger<OrderService> via DI
    // Category is "OrderService", shown as Context in Lunarium log output
    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }

    public void ProcessOrder(int orderId)
    {
        _logger.LogInformation("Starting to process order {OrderId}", orderId);

        try
        {
            // ...
            _logger.LogInformation("Order {OrderId} processed successfully", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order {OrderId}", orderId);
            throw;
        }
    }
}

// ================================================================
//  Section 5: ToMicrosoftLogger — Convert a Lunarium ILogger to MEL ILogger
//
//  Use case: a third-party library or framework only accepts
//  Microsoft.Extensions.Logging.ILogger, but you still want
//  log output to flow through Lunarium's pipeline.
// ================================================================
public static class ToMicrosoftLoggerExample
{
    public static void Example()
    {
        var lunariumLogger = new LoggerBuilder()
            .SetLoggerName("MyApp")
            .AddConsoleSink()
            .Build();

        // Convert to MEL ILogger; the category parameter appears as Context
        Microsoft.Extensions.Logging.ILogger melLogger =
            lunariumLogger.ToMicrosoftLogger("ThirdPartyComponent");

        // Pass to a third-party library that only accepts MEL ILogger
        // thirdPartyService.SetLogger(melLogger);
    }
}

// ================================================================
//  Section 6: Scope
//
//  Scope information produced by MEL's ILogger.BeginScope() is captured
//  by Lunarium and stored in LogEntry.Scope, then output as a "scope"
//  field in JSON Sinks.
//  Text Sinks do not output Scope (it can be read in a custom ILogTarget).
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
        // The scope value is attached to all log entries within this scope
        using (_logger.BeginScope("RequestId={RequestId}", requestId))
        {
            _logger.LogInformation("Started handling request");
            // JSON output will include scope field: RequestId=abc-123
            DoWork();
        }
    }

    private void DoWork()
    {
        // ...
    }
}

// ================================================================
//  Section 7: Lifetime Considerations
//
//  • LunariumLoggerProvider.Dispose() does NOT destroy the Logger it holds.
//    The Logger's lifetime is managed by whoever created it
//    (creator is responsible for Dispose).
//
//  • UseLunariumLog(configureSinks, ...) creates the Logger internally
//    and registers it as an IHostedService; the framework automatically
//    Disposes it when the Host stops.
//
//  • A Logger registered via AddLunariumLogger(logger) must be manually
//    Disposed by the caller when the application shuts down:
//
//    app.Lifetime.ApplicationStopped.Register(() => logger.Dispose());
//
//  • Within the same process, a given LoggerName can only be Build() once
//    (protected by a global singleton lock). Registering two loggers with
//    the same name will throw on the second Build() call.
// ================================================================

// Placeholder: types referenced in the examples above
file class MyWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
