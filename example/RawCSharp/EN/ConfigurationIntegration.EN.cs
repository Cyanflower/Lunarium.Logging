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
//  This file is an integration example for Lunarium.Logging.Configuration.
//  For reference only — not compiled into the library.
//  Covers the appsettings.json structure, BuildLunariumLogger/Loggers,
//  hot-reload mechanism, and combined usage with the Hosting package.
//  Required NuGet package: Lunarium.Logging.Configuration
//  For Host/DI integration, also reference: Lunarium.Logging.Hosting
// ============================================================

using Microsoft.Extensions.Configuration;


using Microsoft.Extensions.Logging;

using Lunarium.Logging.Models;
using Lunarium.Logging.Target;
using Lunarium.Logging.Configuration;
using Lunarium.Logging.Extensions;
using Microsoft.Extensions.Hosting;
using Lunarium.Logging.Config.Models;

namespace Lunarium.Logging;

// ================================================================
//  Section 1: Complete appsettings.json Structure Reference
//
//  The configuration section key defaults to "LunariumLogging"
//  (can be changed via the sectionKey parameter).
//  Top-level fields:
//    GlobalConfig     — Global settings (time zone, timestamp format, etc.)
//    LoggerConfigs    — List of Loggers; each element corresponds to one Logger instance
//
//  ⚠️  GlobalConfig does NOT support hot-reload (one-time design).
//  ✅  LoggerConfig FilterConfig supports hot-reload (watchForChanges: true).
// ================================================================

/*  appsettings.json example:

{
  "LunariumLogging": {
    "GlobalConfig": {
      "TimeZone": "Asia/Shanghai",
      "TextTimestampMode": "Custom",
      "TextCustomTimestampFormat": "yyyy-MM-dd HH:mm:ss.fff zzz",
      "JsonTimestampMode": "ISO8601",
      "EnableUnsafeRelaxedJsonEscaping": true,
      "WriteIndentedJson": false,
      "EnableAutoDestructuring": true
    },
    "LoggerConfigs": [
      {
        "LoggerName": "Runtime",
        "ConsoleSinks": {
          "console": {
            "Enabled": true,
            "ToJson": false,
            "IsColor": true,
            "FilterConfig": {
              "LogMinLevel": "Info",
              "LogMaxLevel": "Critical"
            }
          }
        },
        "FileSinks": {
          "main": {
            "Enabled": true,
            "LogFilePath": "Logs/Runtime.log",
            "MaxFileSizeMB": 10,
            "MaxFile": 5,
            "RotateOnNewDay": false,
            "ToJson": false,
            "FilterConfig": {
              "LogMinLevel": "Info",
              "ContextFilterExcludes": ["Runtime.Proxy"]
            }
          },
          "error": {
            "Enabled": true,
            "LogFilePath": "Logs/Error.log",
            "MaxFileSizeMB": 5,
            "MaxFile": 30,
            "RotateOnNewDay": true,
            "FilterConfig": {
              "LogMinLevel": "Error"
            }
          }
        }
      },
      {
        "LoggerName": "Analytics",
        "FileSinks": {
          "analytics": {
            "LogFilePath": "Logs/Analytics.log",
            "MaxFileSizeMB": 50,
            "MaxFile": 7,
            "RotateOnNewDay": true,
            "ToJson": true,
            "FilterConfig": {
              "LogMinLevel": "Info",
              "ContextFilterIncludes": ["Analytics"]
            }
          }
        }
      }
    ]
  }
}
*/

// ================================================================
//  Section 2: GlobalConfig Field Reference
//
//  All fields are nullable; null means use the library's default value.
// ================================================================

/*  GlobalConfig field descriptions:

  | JSON Field                       | Type    | Allowed values / notes                                     | Default         |
  |----------------------------------|---------|------------------------------------------------------------|-----------------|
  | TimeZone                         | string  | "Local" / "Utc" / "Asia/Shanghai" or any IANA time zone ID | Local time      |
  | TextTimestampMode                | string  | "Unix" / "UnixMs" / "ISO8601" / "Custom"                   | Custom          |
  | TextCustomTimestampFormat        | string  | Only effective when TextTimestampMode=Custom               | "yyyy-MM-dd HH:mm:ss.fff" |
  | JsonTimestampMode                | string  | "Unix" / "UnixMs" / "ISO8601" / "Custom"                   | ISO8601         |
  | JsonCustomTimestampFormat        | string  | Only effective when JsonTimestampMode=Custom               | —               |
  | EnableUnsafeRelaxedJsonEscaping  | bool    | true = CJK/Emoji not escaped (recommended); false = escape all | true        |
  | WriteIndentedJson                | bool    | true = pretty-print; false = compact single line           | false           |
  | EnableAutoDestructuring          | bool    | true = collections auto-destructured as JSON               | false           |
*/

// ================================================================
//  Section 3: BuildLunariumLogger — Build a Single Logger by Name
// ================================================================
public static class BuildSingleLoggerExample
{
    public static ILogger? Build(IConfiguration configuration)
    {
        // Read from the "LunariumLogging" section and build the Logger named "Runtime"
        var logger = configuration.BuildLunariumLogger(
            loggerName:     "Runtime",
            sectionKey:     "LunariumLogging",    // default value; can be omitted
            watchForChanges: false);               // hot-reload disabled

        // Returns null if the specified LoggerName is not found in the configuration
        if (logger is null)
        {
            Console.WriteLine("No Logger configuration found with name 'Runtime'");
        }

        return logger;
    }
}

// ================================================================
//  Section 4: BuildLunariumLoggers — Build All Loggers
// ================================================================
public static class BuildAllLoggersExample
{
    public static IReadOnlyList<ILogger> Build(IConfiguration configuration)
    {
        // Build all Loggers defined in the LoggerConfigs list
        var loggers = configuration.BuildLunariumLoggers(
            sectionKey:     "LunariumLogging",
            watchForChanges: true);    // enable hot-reload

        foreach (var logger in loggers)
        {
            // You can inspect registered Logger names via LoggerManager.GetLoggerList()
        }

        return loggers;
    }
}

// ================================================================
//  Section 5: Hot-Reload (watchForChanges: true)
//
//  Mechanism: ChangeToken.OnChange() listens for IConfiguration change events;
//  when triggered, it calls LoggerManager.UpdateAllLoggerConfig() to
//  hot-swap the Sink configuration.
//
//  Fields that support hot-reload (take effect at runtime):
//    • FilterConfig (LogMinLevel / LogMaxLevel / Include / Exclude)
//    • Sink Enabled switch
//
//  Fields that do NOT support hot-reload (require restart):
//    • GlobalConfig (time zone, timestamp format, and other one-time global settings)
//    • LogFilePath (file path changes require rebuilding the FileTarget)
//    • LoggerName
// ================================================================
public static class HotReloadExample
{
    public static void Explain()
    {
        // Typical use case: during development, lower LogMinLevel from Info to Debug.
        // Edit appsettings.Development.json and save — changes take effect immediately
        // without restarting the service.

        // Implementation details:
        //   configuration.BuildLunariumLogger("Runtime", watchForChanges: true)
        //   is internally equivalent to:
        //     var logger = Build(config);
        //     ChangeToken.OnChange(
        //         () => config.GetReloadToken(),
        //         () => LoggerManager.UpdateAllLoggerConfig(config.ReadLoggerConfigs()));
    }
}

// ================================================================
//  Section 6: Combined Usage with the Hosting Package
//
//  UseLunariumLog(ILoggingBuilder, IConfiguration, loggerName, ...)
//  merges Configuration and Hosting package functionality:
//  read configuration from appsettings.json → build Logger → register as MEL Provider.
// ================================================================
public static class HostingWithConfigurationExample
{
    public static void Configure()
    {
        // var builder = WebApplication.CreateBuilder(args);

        // Read "Runtime" Logger configuration from appsettings.json and register as MEL Provider
        // builder.Logging.ClearProviders();
        // builder.Logging.UseLunariumLog(
        //     configuration: builder.Configuration,
        //     loggerName: "Runtime",
        //     sectionKey: "LunariumLogging",    // default value
        //     watchForChanges: true);           // enable hot-reload

        // ── Or use the IHostBuilder style ──
        // Host.CreateDefaultBuilder()
        //     .UseLunariumLog(
        //         configuration: context.Configuration,
        //         loggerName: "Runtime",
        //         watchForChanges: true)
        //     .Build()
        //     .Run();
    }
}
