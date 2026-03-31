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
//  This file is a quick-start example for Lunarium.Logging.
//  For reference only — not compiled into the library.
//  Goal: get your first log line running in 5 minutes.
//  For full Sink configuration, see SinkConfiguration.EN.cs.
// ============================================================

using Lunarium.Logging.Target;
using Lunarium.Logging.Configuration;
using Lunarium.Logging.Extensions;
using Microsoft.Extensions.Hosting;
using Lunarium.Logging.Config.Models;

namespace Lunarium.Logging;

public static class QuickStartExample
{
    // ================================================================
    //  Step 1: Create a Logger
    //
    //  • GlobalConfigurator.Configure() is optional; if omitted,
    //    Build() automatically applies default values.
    //  • LoggerBuilder can call Build() multiple times, each returning
    //    an independent Logger instance.
    //  • The Logger should be held as a global singleton — created once
    //    at startup and shared via DI or a static property.
    // ================================================================
    public static readonly ILogger Logger = new LoggerBuilder()
        .SetLoggerName("MyApp")          // Logger name shown in log output (optional, defaults to empty string)
        .AddConsoleSink()                // Simplest console Sink with all defaults
        .Build();

    // ================================================================
    //  Step 2: Write Logs
    //
    //  Five levels from lowest to highest: Debug < Info < Warning < Error < Critical
    //  Default FilterConfig.LogMinLevel = Info, so Debug is filtered out.
    // ================================================================
    public static void WriteBasicLogs()
    {
        Logger.Debug("Debug message — filtered out by default Info-level Sink");
        Logger.Info("Service started successfully on port {Port}", 8080);
        Logger.Warning("Memory usage exceeded {Percent}%, please monitor", 85);
        Logger.Error("Database connection failed, retry attempt {Attempt}", 3);
        Logger.Critical("Disk space critically low, system will halt");
    }

    // ================================================================
    //  Step 3: Log Exceptions
    //
    //  All levels support an Exception? parameter overload.
    //  Exception details are appended at the end of the log output.
    // ================================================================
    public static void WriteExceptionLogs()
    {
        try
        {
            // ...
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error while processing order {OrderId}", 42);
            // Example output:
            // [ERROR] Error while processing order 42
            // System.InvalidOperationException: ...
            //   at ...
        }
    }

    // ================================================================
    //  Step 4: Message Template Syntax Quick Reference
    //
    //  Property values are filled in order via params object?[] propertyValues.
    //  Property name rules: first character must be a letter or _,
    //  subsequent characters may include letters, digits, underscores, or dots
    //  (a dot cannot be followed by a digit).
    // ================================================================
    public static void MessageTemplateSyntax()
    {
        // ── Basic property substitution ───────────────────────────────────────
        Logger.Info("User {UserId} logged in", 1001);
        // Output: User 1001 logged in

        // ── Destructuring ({@}): serialize object as JSON ─────────────────────
        var order = new { Id = 42, Amount = 99.9m };
        Logger.Info("New order {@Order}", order);
        // Output: New order {"Id":42,"Amount":99.9}

        // ── Stringify ({$}): force call to ToString() ─────────────────────────
        Logger.Info("Status: {$Status}", SomeEnum.Active);
        // Output: Status: Active  (rather than the underlying integer value)

        // ── Alignment (positive = right-align, negative = left-align) ─────────
        Logger.Info("[{Level,8}] message", "INFO");
        // Output: [    INFO] message

        Logger.Info("[{Level,-8}] message", "INFO");
        // Output: [INFO    ] message

        // ── Format string (standard .NET format specifiers) ───────────────────
        Logger.Info("Amount: {Amount:F2}", 1234.5);
        // Output: Amount: 1234.50

        // ── Alignment + format (order is fixed: alignment first, then format) ──
        Logger.Info("[{Amount,10:F2}]", 9.9);
        // Output: [      9.90]

        // ── Brace escaping ────────────────────────────────────────────────────
        Logger.Info("Literal braces: {{will not be parsed}}");
        // Output: Literal braces: {will not be parsed}

        // ── Null values ───────────────────────────────────────────────────────
        string? name = null;
        Logger.Info("Name: {Name}", name);
        // Output: Name: (null)
    }

    // ================================================================
    //  Step 5: ForContext — Attach a Context Prefix to Logs
    //
    //  • ForContext() returns a lightweight LoggerWrapper, not a new Logger.
    //  • Multiple ForContext levels are joined with a dot:
    //      A → ForContext("B") → Context = "A.B"
    //  • Context bytes are pre-computed at construction time;
    //    subsequent Log calls incur zero additional allocations.
    // ================================================================
    public static void ForContextExample()
    {
        // Specify context by string
        var orderLogger = Logger.ForContext("Order.Service");
        orderLogger.Info("Processing order {OrderId}", 42);
        // Context in output: Order.Service

        // Specify context by type (uses the class name as the context string)
        var payLogger = Logger.ForContext<PaymentService>();
        payLogger.Info("Payment deducted successfully");
        // Context in output: PaymentService

        // Multi-level nesting (flattened — unwrapped to root Logger in one step)
        var rootCtx  = Logger.ForContext("App");
        var childCtx = rootCtx.ForContext("Module");
        childCtx.Info("Message");
        // Context in output: App.Module
    }

    // ================================================================
    //  Appendix: LoggerName vs. Context
    //
    //  LoggerName  ─ Identifies the Logger instance itself
    //                (set at build time via .SetLoggerName(), immutable)
    //  Context     ─ Identifies the current call site
    //                (set dynamically via ForContext(), supports nesting)
    //
    //  Typical usage:
    //    LoggerName = "Runtime"          (Logger name for the entire service)
    //    Context    = "Payment.Processor" (current processing module)
    // ================================================================

    private enum SomeEnum { Active, Inactive }
    private class PaymentService { }
}
