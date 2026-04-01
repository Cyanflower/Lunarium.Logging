# Lunarium.Logging

[![Build](https://github.com/Cyanflower/Lunarium.Logging/actions/workflows/ci.yml/badge.svg)](https://github.com/Cyanflower/Lunarium.Logging/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Lunarium.Logging.svg)](https://www.nuget.org/packages/Lunarium.Logging)
[![Coverage](https://img.shields.io/badge/coverage-92%25-brightgreen)](https://github.com/Cyanflower/Lunarium.Logging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

面向 .NET 的轻量级、高性能结构化日志库——零外部依赖、热路径过滤与解析零分配、完整调用链 ~200 ns 低开销、原生 AOT 兼容。

> 为那些希望使用结构化消息模板日志、同时不想引入大量依赖包或与复杂 Sink 配置周旋的开发者而设计。

---

## 为什么选择 Lunarium.Logging？

许多结构化日志库将基础功能拆分到多个包中——核心包、控制台 Sink、文件 Sink、格式化器需要分别安装。Lunarium.Logging 将日常使用所需的一切打包在单一主包中：控制台、文件（含内置轮转）和 Channel Sink 全部内置，零外部依赖，无程序集蔓延。

厌倦了复杂的日志器配置？
**这个库诞生于一个具体的实际需求**：让多 Sink + 按条件分发的配置变得**直观**可用。将高频模块独立切割、将全局错误单独汇聚、让控制台根据环境使用不同过滤级别——Lunarium.Logging 正是为了应对这些真实的生产痛点而生。从此你不再需要折腾复杂的子日志记录器（Sub-loggers）、晦涩的过滤表达式语法，更不需要拼凑各种额外的依赖包。在这里，每个 Sink 均可在同一个 builder 链中直接声明自己的级别范围和上下文规则，无需独立的配置步骤，也不需要学习额外的表达式语法。具体示例见[过滤与多 Sink 路由](#过滤与多-sink-路由)。

在性能方面，过滤器与模板解析器热路径零分配（分别约 9 ns 和 11–19 ns）。通过 `Hosting` 包可作为 `Microsoft.Extensions.Logging` 的完整替代 Provider 接入，现有 ASP.NET Core 或 Generic Host 应用无需修改注入方式即可迁移。过滤级别支持通过 `appsettings.json` 热更新在运行时生效，无需重启。

可选的扩展包（`Hosting`、`Configuration`、`Http`）是真正的可选项——核心库可完全独立运行。

---

## 特性亮点

- **简洁直观的 API** — Fluent Builder，合理的默认值，无多余仪式感。
- **按 Sink 独立过滤与上下文路由** — 每个 Sink 直接在 builder 链中声明自己的级别范围和上下文 Include/Exclude 规则。模块路由到专属文件、错误单独汇聚、控制台使用不同级别——无需子日志记录器，无需学习过滤表达式语法。
- **零依赖** — 核心库功能完整，控制台、文件（含内置轮转）和 Channel Sink 均已内置，日常使用无需额外安装任何包。
- **热路径低开销/过滤解析零分配** — 过滤器缓存命中约 9 ns，模板缓存命中约 11–19 ns，两者均零分配。完整日志调用约 200 ns，分配 128–240 B，来自 LogEntry 构造与 params boxing，而非日志基础设施本身。详见[性能基准](#性能基准)。
- **结构化消息模板** — 支持 `{Property}`、`{@Object}`、`{Value,10:F2}` 语法，含对齐、格式化和解构前缀。
- **原生 AOT 兼容** — 全面支持 AOT 和裁剪分析。注册 Source Generated 的 `JsonSerializerContext` 以支持 `{@Object}` 解构，或实现 `IDestructurable`/`IDestructured` 以实现完全无反射的结构化输出。

### 控制台输出示例
![彩色控制台输出](assets/ConsoleSample.png)

---

## 包列表

| 包 | 说明 | NuGet |
|----|------|-------|
| `Lunarium.Logging` | 核心库——结构化日志、Sink、过滤器 | [![NuGet](https://img.shields.io/nuget/v/Lunarium.Logging.svg)](https://www.nuget.org/packages/Lunarium.Logging) |
| `Lunarium.Logging.Hosting` | `IHostBuilder` / `ILoggingBuilder` 集成，MEL 桥接（依赖 `Lunarium.Logging.Configuration`） | [![NuGet](https://img.shields.io/nuget/v/Lunarium.Logging.Hosting.svg)](https://www.nuget.org/packages/Lunarium.Logging.Hosting) |
| `Lunarium.Logging.Configuration` | `appsettings.json` 集成，支持热更新 | [![NuGet](https://img.shields.io/nuget/v/Lunarium.Logging.Configuration.svg)](https://www.nuget.org/packages/Lunarium.Logging.Configuration) |
| `Lunarium.Logging.Http` | HTTP 批量推送 Sink——支持 Seq（CLEF）、Loki 及自定义端点 | [![NuGet](https://img.shields.io/nuget/v/Lunarium.Logging.Http.svg)](https://www.nuget.org/packages/Lunarium.Logging.Http) |

---

## 快速开始

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

文件轮转已内置，无需额外安装任何包：

```csharp
// 按天轮转，保留最近 30 个文件
new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddTimedRotatingFileSink("logs/app.log", maxFile: 30)
    .Build();

// 文件超过 50 MB 时轮转
new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddSizedRotatingFileSink("logs/app.log", maxFileSizeMB: 50)
    .Build();

// 两种策略叠加：大小限制 + 按天轮转
new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddRotatingFileSink("logs/app.log", maxFileSizeMB: 50, rotateOnNewDay: true, maxFile: 30)
    .Build();
```

### 结构化解构

```csharp
// {@} — 渲染为结构化 JSON
logger.Info("Order created: {@Order}", order);
// [2026-03-31 12:00:00.000] [INF] [MyApp] Order created: {"Id":42,"Total":99.99}

// {$} — 强制 ToString()
logger.Info("Status: {$Status}", myEnum);
// [2026-03-31 12:00:00.000] [INF] [MyApp] Status: Active

// 对齐与格式化
logger.Info("Price: {Amount,10:F2} USD", 42.5);
// [2026-03-31 12:00:00.000] [INF] [MyApp] Price:      42.50 USD
```

### 上下文与作用域

```csharp
// 通过字符串附加来源上下文
ILogger orderLog = logger.ForContext("Order.Processor");
orderLog.Info("Processing order {Id}", orderId);
// [2026-03-31 12:00:00.000] [INF] [MyApp] [Order.Processor] Processing order 1001

// 或通过类型——使用类名作为上下文字符串
ILogger payLog = logger.ForContext<PaymentService>();

// 链式上下文："Order.Processor" → ForContext("Validator") = "Order.Processor.Validator"
ILogger validatorLog = orderLog.ForContext("Validator");
validatorLog.Info("Validating order {Id}", orderId);
// [2026-03-31 12:00:00.000] [INF] [MyApp] [Order.Processor.Validator] Validating order 1001
```

可如上所示独立使用，也可作为 ASP.NET Core / Generic Host 默认 `Microsoft.Extensions.Logging` 提供者的完整替代品——详见[与 Generic Host 集成](#与-generic-host-集成)。

---

## 过滤与多 Sink 路由

每个 Sink 携带独立的 `FilterConfig`——级别范围加可选的上下文 Include/Exclude 规则。所有配置直接写在 builder 链中，无需独立的配置步骤。

```csharp
ILogger logger = new LoggerBuilder()
    .SetLoggerName("MyApp")

    // 主日志——排除高频的代理模块，其余全部接收
    .AddSizedRotatingFileSink(
        logFilePath: "logs/app.log", 
        maxFileSizeMB: 10, 
        maxFile: 5,
        FilterConfig: new FilterConfig
        {
            LogMinLevel = LogLevel.Info,
            ContextFilterExcludes = ["MyApp.ProxyService"],
        })

    // 代理模块专属文件，只收该前缀
    .AddSizedRotatingFileSink(
        logFilePath: "logs/proxy.log", 
        maxFileSizeMB: 10, 
        maxFile: 10,
        FilterConfig: new FilterConfig
        {
            LogMinLevel = LogLevel.Info,
            ContextFilterIncludes = ["MyApp.ProxyService"],
        })

    // 全局错误汇聚——所有模块，仅 Error 及以上
    .AddSizedRotatingFileSink(
        logFilePath: "logs/error.log", 
        maxFileSizeMB: 10, 
        maxFile: 5,
        FilterConfig: new FilterConfig 
        { 
            LogMinLevel = LogLevel.Error 
        })

    // 控制台——开发期 Debug 及以上
    .AddConsoleSink(
        FilterConfig: new FilterConfig 
        { 
            LogMinLevel = LogLevel.Debug 
        })
    .Build();
```

`ContextFilterIncludes` 和 `ContextFilterExcludes` 按前缀匹配，因此 `"MyApp.ProxyService"` 会覆盖 `MyApp.ProxyService`、`MyApp.ProxyService.Handler` 及从中派生的任何更深层上下文。

包含审计日志、仅收 Warning 的专属 Sink、推给 UI 广播的 Channel Sink 等完整生产示例，参见 [Sink 配置详解](example/ZH/SinkConfiguration.ZH.md)。

---

## 与 Generic Host 集成

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

照常从 DI 使用 `ILogger<T>`——Lunarium.Logging 作为 MEL 提供者运行。

---

## appsettings.json 配置

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

支持**热更新**——过滤级别变更在运行时即时生效，无需重启。

---

## HTTP Sink（Seq / Loki）

```sh
dotnet add package Lunarium.Logging.Http
```

```csharp
// Seq（CLEF 格式）
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

使用原生 HttpClient 提供轻量级 HTTP Sink，支持将日志批量推送到 Seq（CLEF 格式）或 Grafana Loki。

---

## 性能基准

基准测试运行环境：i7-8750H，.NET 10.0，Release 模式。

**热路径（过滤器 + 解析器缓存命中）**

| 场景 | 耗时 | 分配 |
|------|------|------|
| 过滤器缓存命中 | ~9 ns | 0 |
| 模板缓存命中 | ~11–19 ns | 0 |
| 完整日志调用（无属性） | ~186 ns | 113 B |
| 完整日志调用（5 个属性） | ~212 ns | 227 B |

**Writer 渲染**

| 格式 | 耗时 | 分配 |
|------|------|------|
| 文本 / 彩色 | ~320–600 ns | 32 B |
| JSON | ~470–750 ns | 64 B |

渲染时产生的 32–64 B 分配是一笔极小的固定开销（与对象池管理或内部结构体包装有关），完全不受属性数量或消息长度的影响。过滤器和解析器缓存在热路径上实现了严格的零分配。

详细分析及跨平台对比，请参阅基准测试报告：
- [性能分析](BenchmarkReports/Latest/EN/PerformanceAnalysis.md)
- [平台差异](BenchmarkReports/Latest/EN/PlatformDifferences.md)

---

## 示例

完整的带注释示例位于 [`example/`](example/) 目录，每个示例均提供中英文 Markdown 指南和原始 C# 文件。

| 示例 | 说明 |
|------|------|
| [快速开始](example/ZH/QuickStart.ZH.md) | 日志级别、异常、消息模板语法、`ForContext` |
| [Sink 配置](example/ZH/SinkConfiguration.ZH.md) | 所有 Sink 类型、`FilterConfig`、`ISinkConfig`、`GlobalConfigurator` |
| [Hosting 集成](example/ZH/HostingIntegration.ZH.md) | Generic Host、DI、MEL 桥接、`UseLunariumLog` |
| [配置文件集成](example/ZH/ConfigurationIntegration.ZH.md) | `appsettings.json` 绑定、热更新 |
| [HTTP Sink](example/ZH/HttpSink.ZH.md) | Seq、Loki、自定义序列化器、`AddHttpSink` |
| [进阶用法](example/ZH/AdvancedUsage.ZH.md) | 自定义 `ILogTarget`、`IDestructurable`/`IDestructured`、AOT、`LoggerManager` |

原始 C# 源文件（无 Markdown）位于 [`example/RawCSharp/`](example/RawCSharp/)。

---

## 原生 AOT

Lunarium.Logging 标记了 `IsAotCompatible=true`，在构建时即通过 AOT/裁剪分析。

`{@Object}` 解构底层依赖 `JsonSerializer`。在 AOT 环境下，注册 Source Generated 的 `JsonSerializerContext` 以实现完全无反射的序列化。未注册的类型将静默回退为 `ToString()`——不会抛出运行时异常。

**第一步——为你的类型声明 `JsonSerializerContext`：**

```csharp
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(UserRecord))]
[JsonSerializable(typeof(List<Order>))]
internal partial class MyAppJsonContext : JsonSerializerContext { }
```

**第二步——在调用 `Build()` 前注册：**

```csharp
GlobalConfigurator.Configure()
    .UseJsonTypeInfoResolver(MyAppJsonContext.Default)
    .Apply();

// 多个 Context 可合并：
// .UseJsonTypeInfoResolver(
//     JsonTypeInfoResolver.Combine(MyAppJsonContext.Default, AnotherContext.Default))
```

也可以实现 `IDestructurable` 或 `IDestructured`，从而完全不依赖序列化器即可获得结构化输出。

---

## 环境要求

- .NET 8、9 或 10
- 无外部 NuGet 依赖（核心库）

---

## 测试覆盖率

三个测试项目共 776 个测试。

| 项目 | 测试数 |
|------|-------:|
| `Lunarium.Logging.Tests` | 653 |
| `Lunarium.Logging.Http.Tests` | 102 |
| `Lunarium.Logging.IntegrationTests` | 21 |

| 指标 | 覆盖率 |
|------|-------:|
| 行覆盖率 | 92.1% |
| 分支覆盖率 | 88.8% |
| 方法覆盖率 | 98.7% |

各包覆盖率：

| 包 | 行覆盖率 |
|----|--------:|
| `Lunarium.Logging` | 91.3% |
| `Lunarium.Logging.Hosting` | 100% |
| `Lunarium.Logging.Configuration` | 98.1% |
| `Lunarium.Logging.Http` | 92.8% |

---

## 致谢

本库所支持的**结构化消息模板语法（Message Templates）**及其底层的 `Token` 抽象模型（如 `MessageTemplate`、`TextToken`、`PropertyToken`），在灵感和概念设计上深受 [Serilog](https://github.com/serilog/serilog) 优秀架构的启发。完整说明见 [`ATTRIBUTIONS.md`](ATTRIBUTIONS.md)。

---

## 许可证

Apache 2.0——详见 [LICENSE](LICENSE)。
