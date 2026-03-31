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
//  本文件是 Lunarium.Logging.Configuration 的集成示例，仅供参考，不参与编译。
//  涵盖 appsettings.json 结构说明、BuildLunariumLogger/Loggers、
//  热更新机制，以及与 Hosting 包的组合用法。
//  需引用 NuGet 包：Lunarium.Logging.Configuration
//  若需与 Host/DI 集成，还需引用：Lunarium.Logging.Hosting
// ============================================================

using Microsoft.Extensions.Configuration;


using Microsoft.Extensions.Logging;

using Lunarium.Logging.Target;
using Lunarium.Logging.Configuration;
using Lunarium.Logging.Extensions;
using Microsoft.Extensions.Hosting;

namespace Lunarium.Logging;

// ================================================================
//  第一节：appsettings.json 完整结构参考
//
//  配置节点默认为 "LunariumLogging"（可通过 sectionKey 参数修改）。
//  顶级字段：
//    GlobalConfig     ─ 全局配置（时区、时间戳格式等）
//    LoggerConfigs    ─ Logger 列表，每个元素对应一个 Logger 实例
//
//  ⚠️  GlobalConfig 不支持热更新（一次性设计）。
//  ✅  LoggerConfig 的 FilterConfig 支持热更新（watchForChanges: true）。
// ================================================================

/*  appsettings.json 示例：

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
//  第二节：GlobalConfig 字段对照表
//
//  所有字段均可空，null 表示使用主库默认值。
// ================================================================

/*  GlobalConfig 字段说明：

  | JSON 字段                        | 类型    | 可选值 / 说明                                    | 默认值        |
  |----------------------------------|---------|--------------------------------------------------|---------------|
  | TimeZone                         | string  | "Local" / "Utc" / "Asia/Shanghai" 等 IANA 时区 ID | 本地时间      |
  | TextTimestampMode                | string  | "Unix" / "UnixMs" / "ISO8601" / "Custom"         | Custom        |
  | TextCustomTimestampFormat        | string  | 仅在 TextTimestampMode=Custom 时生效               | "yyyy-MM-dd HH:mm:ss.fff" |
  | JsonTimestampMode                | string  | "Unix" / "UnixMs" / "ISO8601" / "Custom"         | ISO8601       |
  | JsonCustomTimestampFormat        | string  | 仅在 JsonTimestampMode=Custom 时生效               | —             |
  | EnableUnsafeRelaxedJsonEscaping  | bool    | true=中文/Emoji 不转义（推荐）；false=全转义       | true          |
  | WriteIndentedJson                | bool    | true=缩进；false=紧凑单行                         | false         |
  | EnableAutoDestructuring          | bool    | true=集合类型自动解构为 JSON                       | false         |
*/

// ================================================================
//  第三节：BuildLunariumLogger — 按名称构建单个 Logger
// ================================================================
public static class BuildSingleLoggerExample
{
    public static ILogger? Build(IConfiguration configuration)
    {
        // 从 "LunariumLogging" 节点读取配置，按 LoggerName="Runtime" 构建
        var logger = configuration.BuildLunariumLogger(
            loggerName:     "Runtime",
            sectionKey:     "LunariumLogging",    // 默认值，可省略
            watchForChanges: false);               // 不启用热更新

        // 返回 null 表示配置中找不到指定 LoggerName
        if (logger is null)
        {
            Console.WriteLine("找不到名为 Runtime 的 Logger 配置");
        }

        return logger;
    }
}

// ================================================================
//  第四节：BuildLunariumLoggers — 构建全部 Logger
// ================================================================
public static class BuildAllLoggersExample
{
    public static IReadOnlyList<ILogger> Build(IConfiguration configuration)
    {
        // 构建 LoggerConfigs 列表中定义的所有 Logger
        var loggers = configuration.BuildLunariumLoggers(
            sectionKey:     "LunariumLogging",
            watchForChanges: true);    // 启用热更新

        foreach (var logger in loggers)
        {
            // 可以通过 LoggerManager.GetLoggerList() 查看已注册的 Logger 名称
        }

        return loggers;
    }
}

// ================================================================
//  第五节：热更新（watchForChanges: true）
//
//  原理：ChangeToken.OnChange() 监听 IConfiguration 变更事件，
//  触发时调用 LoggerManager.UpdateAllLoggerConfig() 热替换 Sink 配置。
//
//  支持热更新的字段（运行时生效）：
//    • FilterConfig（LogMinLevel / LogMaxLevel / Include / Exclude）
//    • Sink 的 Enabled 开关
//
//  不支持热更新（需重启生效）：
//    • GlobalConfig（时区、时间戳格式等一次性全局配置）
//    • LogFilePath（文件路径变更需重建 FileTarget）
//    • LoggerName
// ================================================================
public static class HotReloadExample
{
    public static void Explain()
    {
        // 典型场景：开发时将 LogMinLevel 从 Info 调低为 Debug，
        // 修改 appsettings.Development.json 保存后立即生效，无需重启服务。

        // 实现细节：
        //   configuration.BuildLunariumLogger("Runtime", watchForChanges: true)
        //   内部等价于：
        //     var logger = Build(config);
        //     ChangeToken.OnChange(
        //         () => config.GetReloadToken(),
        //         () => LoggerManager.UpdateAllLoggerConfig(config.ReadLoggerConfigs()));
    }
}

// ================================================================
//  第六节：与 Hosting 包结合使用
//
//  UseLunariumLog(ILoggingBuilder, IConfiguration, loggerName, ...)
//  将 Configuration 包和 Hosting 包的功能合并：
//  从 appsettings.json 读取配置 → 构建 Logger → 注册为 MEL Provider。
// ================================================================
public static class HostingWithConfigurationExample
{
    public static void Configure()
    {
        // var builder = WebApplication.CreateBuilder(args);

        // 从 appsettings.json 读取 "Runtime" Logger 配置并注册为 MEL Provider
        // builder.Logging.ClearProviders();
        // builder.Logging.UseLunariumLog(
        //     configuration: builder.Configuration,
        //     loggerName: "Runtime",
        //     sectionKey: "LunariumLogging",    // 默认值
        //     watchForChanges: true);           // 启用热更新

        // ── 或者使用 IHostBuilder 写法 ──
        // Host.CreateDefaultBuilder()
        //     .UseLunariumLog(
        //         configuration: context.Configuration,
        //         loggerName: "Runtime",
        //         watchForChanges: true)
        //     .Build()
        //     .Run();
    }
}
