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
//  本文件是 Lunarium.Logging 的 Sink 配置完整参考示例，仅供参考，不参与编译。
//  涵盖 GlobalConfigurator、所有 Sink 类型、FilterConfig、TextOutputIncludeConfig
//  以及 ISinkConfig 配置对象化用法，最后附一份多 Sink 实战配置场景。
// ============================================================

using System.Threading.Channels;
using Lunarium.Logging.Target;

namespace Lunarium.Logging;

// ================================================================
//  第一节：GlobalConfigurator — 全局配置
//
//  • 必须在第一次 Build() 之前调用，且只能调用一次（进程级锁保护）。
//  • 若不调用，Build() 自动应用默认值（见下方注释）。
//  • Configure() 返回流式 ConfigurationBuilder，最后调用 Apply() 生效。
// ================================================================
public static class GlobalConfiguratorExample
{
    public static void Configure()
    {
        GlobalConfigurator.Configure()

            // ── 时区 ──────────────────────────────────────────────────────────
            // 默认：本地时间（UseLocalTimeZone）
            .UseCustomTimezone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"))
            // 其他选项：
            // .UseUtcTimeZone()          // UTC 时间
            // .UseLocalTimeZone()        // 系统本地时间（默认）

            // ── JSON Sink 时间戳格式 ───────────────────────────────────────────
            // 默认：ISO 8601（"O"）
            .UseJsonISO8601Timestamp()
            // 其他选项：
            // .UseJsonUnixTimestamp()           // Unix 秒（long）
            // .UseJsonUnixMsTimestamp()          // Unix 毫秒（long）
            // .UseJsonCustomTimestamp("yyyy-MM-ddTHH:mm:ss")  // 自定义格式字符串

            // ── 文本 Sink 时间戳格式 ───────────────────────────────────────────
            // 默认："yyyy-MM-dd HH:mm:ss.fff"
            .UseTextCustomTimestamp("yyyy-MM-dd HH:mm:ss.fff zzz")
            // 其他选项：
            // .UseTextISO8601Timestamp()         // ISO 8601
            // .UseTextUnixTimestamp()            // Unix 秒
            // .UseTextUnixMsTimestamp()          // Unix 毫秒

            // ── 集合自动解构 ───────────────────────────────────────────────────
            // 默认：关闭
            // 开启后，List<T>、Dictionary、数组等无需手动写 {@} 前缀即可序列化为 JSON。
            // 不开启时集合只输出 ToString() 结果（通常是类型名）。
            .EnableAutoDestructuring()

            // ── JSON 中文字符转义 ──────────────────────────────────────────────
            // 默认：保留原始中文（UnsafeRelaxedJsonEscaping）
            // .DisableUnsafeRelaxedJsonEscaping()    // 将中文/Emoji 转义为 \uXXXX
            // .EnableUnsafeRelaxedJsonEscaping()     // 显式保留原始字符（与默认行为相同，用于表达意图）

            // ── JSON 缩进 ──────────────────────────────────────────────────────
            // 默认：紧凑（单行）
            // .UseIndentedJson()   // 换行缩进，便于人工阅读；体积略大
            // .UseCompactJson()    // 紧凑（显式设置，与默认行为相同）

            // ── AOT：注册 JsonSerializerContext（见 AdvancedUsage.ZH.cs） ──────
            // .UseJsonTypeInfoResolver(MyAppJsonContext.Default)

            .Apply();
    }
}

// ================================================================
//  第二节：FilterConfig — Sink 级别过滤与上下文过滤
//
//  每个 Sink 独立拥有一份 FilterConfig，互不影响。
//  过滤执行顺序：
//    1. 级别检查：LogMinLevel ≤ entry.Level ≤ LogMaxLevel
//    2. Include 检查：Context 必须以列表中某个前缀开头（空列表 = 全部通过）
//    3. Exclude 检查：Context 匹配任一排除前缀则丢弃
// ================================================================
public static class FilterConfigReference
{
    public static FilterConfig[] Examples =
    [
        // 示例一：只接收 Info 及以上级别（最常见用法）
        new FilterConfig
        {
            LogMinLevel = LogLevel.Info,        // 默认值
            LogMaxLevel = LogLevel.Critical,    // 默认值
        },

        // 示例二：精确匹配单个级别（MinLevel == MaxLevel）
        new FilterConfig
        {
            LogMinLevel = LogLevel.Warning,
            LogMaxLevel = LogLevel.Warning,    // 只收 Warning，不收 Error/Critical
        },

        // 示例三：Context Include 白名单
        //   只接收 Context 以 "Payment" 或 "Order" 开头的日志
        new FilterConfig
        {
            ContextFilterIncludes = ["Payment", "Order"],
            LogMinLevel = LogLevel.Debug,
        },

        // 示例四：Context Exclude 黑名单
        //   接收所有日志，但排除高频模块 "Proxy" 和 "Heartbeat"
        new FilterConfig
        {
            ContextFilterExcludes = ["Proxy", "Heartbeat"],
            LogMinLevel = LogLevel.Info,
        },

        // 示例五：Include + Exclude 同时使用
        //   先 Include（只要 Runtime 前缀），再 Exclude（排除 Runtime.Proxy）
        new FilterConfig
        {
            ContextFilterIncludes = ["Runtime"],
            ContextFilterExcludes = ["Runtime.Proxy"],
            LogMinLevel = LogLevel.Info,
        },

        // 示例六：大小写不敏感匹配
        //   "payment"、"Payment"、"PAYMENT" 均命中
        new FilterConfig
        {
            ContextFilterIncludes = ["Payment"],
            IgnoreFilterCase = true,
        },
    ];
}

// ================================================================
//  第三节：TextOutputIncludeConfig — 文本/颜色输出字段可见性
//
//  适用于 ConsoleTarget、FileTarget（文本模式）、StringChannelTarget、
//  ByteChannelTarget。控制日志行中哪些前置字段参与输出。
//
//  默认输出格式：
//    [2026-03-30 14:00:00.000] [INFO ] [MyApp] [Order.Service] 消息内容
//     ^^^^时间戳^^^^           ^^级别^ ^^^Logger名^^^  ^^Context^^
// ================================================================
public static class TextOutputIncludeConfigReference
{
    public static TextOutputIncludeConfig[] Examples =
    [
        // 全部显示（默认行为）
        new TextOutputIncludeConfig
        {
            IncludeTimestamp  = true,
            IncludeLevel      = true,
            IncludeLoggerName = true,
            IncludeContext    = true,
        },

        // 极简模式：只显示级别和消息
        new TextOutputIncludeConfig
        {
            IncludeTimestamp  = false,
            IncludeLevel      = true,
            IncludeLoggerName = false,
            IncludeContext    = false,
        },

        // 隐藏 LoggerName，仅保留 Context（多 Logger 实例的场景中可减少冗余）
        new TextOutputIncludeConfig
        {
            IncludeTimestamp  = true,
            IncludeLevel      = true,
            IncludeLoggerName = false,
            IncludeContext    = true,
        },
    ];
}

// ================================================================
//  第四节：所有 Sink 类型速览
//
//  每种 Sink 对应一个 AddXxxSink 扩展方法，可链式添加到 LoggerBuilder。
//  所有方法均支持可选的 FilterConfig 和 sinkName 参数。
// ================================================================
public static class AllSinkTypesExample
{
    public static ILogger Build()
    {
        ChannelReader<string> stringReader;
        ChannelReader<byte[]> bytesReader;
        ChannelReader<LogEntry> entryReader;

        return new LoggerBuilder()
            .SetLoggerName("MyApp")

            // ── ConsoleSink ────────────────────────────────────────────────────
            // 非重定向时自动彩色输出；Error/Critical → stderr，其余 → stdout。
            // isColor=true（默认）：ANSI 彩色。重定向或 NO_COLOR 环境变量时自动降级。
            // toJson=false（默认）：文本格式。
            .AddConsoleSink(
                isColor: true,
                toJson: false,
                textOutputIncludeConfig: new TextOutputIncludeConfig
                {
                    IncludeTimestamp  = true,
                    IncludeLevel      = true,
                    IncludeLoggerName = true,
                    IncludeContext    = true,
                },
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Debug })

            // ── FileSink（无轮转） ─────────────────────────────────────────────
            // 追加写入，不自动轮转，适合小型应用或短期调试。
            .AddFileSink(
                logFilePath: "Logs/app.log")

            // ── SizedRotatingFileSink（按大小轮转） ────────────────────────────
            // 单文件超过 maxFileSizeMB 时轮转，新文件名含完整时间戳：
            //   app-2026-03-30-14-00-00.log
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/app-sized.log",
                maxFileSizeMB: 10,
                maxFile: 5,         // 最多保留 5 个历史文件；0 = 不限
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

            // ── TimedRotatingFileSink（按天轮转） ──────────────────────────────
            // 每天 00:00 轮转，文件名含日期：app-2026-03-30.log
            .AddTimedRotatingFileSink(
                logFilePath: "Logs/app-daily.log",
                maxFile: 30,        // 最多保留 30 天
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

            // ── RotatingFileSink（叠加轮转：大小 + 按天） ─────────────────────
            // 任一条件满足即触发轮转；文件名含完整时间戳（同按大小轮转）。
            // rotateOnNewDay=true 可省略，AddRotatingFileSink 专为叠加场景设计。
            .AddRotatingFileSink(
                logFilePath: "Logs/app-combined.log",
                maxFileSizeMB: 50,
                rotateOnNewDay: true,
                maxFile: 14)

            // ── StringChannelSink ─────────────────────────────────────────────
            // 将日志格式化为 string 写入 Channel，供外部消费（UI/WebSocket 等）。
            // 每次 Emit 产生一次 string 堆分配；若下游直接写网络/文件，推荐 Utf8ByteChannel。
            .AddStringChannelSink(
                out stringReader,
                capacity: 1000,     // Channel 有界容量，满时丢弃新写入（DropWrite）
                isColor: false,
                toJson: false,
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

            // ── Utf8ByteChannelSink ───────────────────────────────────────────
            // 将日志格式化为 byte[] 写入 Channel，跳过 UTF-8→string 解码。
            // 适合消费者直接写网络/文件（HttpClient.SendAsync、FileStream.Write 等）。
            .AddUtf8ByteChannelSink(
                out bytesReader,
                capacity: 1000,
                toJson: true,       // JSON 格式；false = 纯文本
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

            // ── LogEntryChannelSink ───────────────────────────────────────────
            // 直接将 LogEntry 对象推入 Channel，零格式化开销。
            // 适合消费者需要完整结构化数据（自定义处理、数据库写入等）。
            .AddLogEntryChannelSink(
                out entryReader,
                capacity: 500,
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Warning })

            .Build();

        // Channel 消费示例（后台任务）：
        // _ = Task.Run(async () =>
        // {
        //     await foreach (var line in stringReader.ReadAllAsync())
        //         Console.WriteLine(line);
        // });
        //
        // _ = Task.Run(async () =>
        // {
        //     await foreach (var entry in entryReader.ReadAllAsync())
        //     {
        //         // 直接访问结构化字段，自行决定如何处理
        //         Console.WriteLine($"[{entry.LogLevel}] {entry.LoggerName} {entry.Message}");
        //     }
        // });
    }
}

// ================================================================
//  第五节：ISinkConfig 配置对象化
//
//  上面的 AddXxxSink() 是"直接传参"方式，适合在代码中固定写死的配置。
//  若 Sink 配置来自外部（JSON 文件、数据库、配置中心），可用 ISinkConfig 实现类：
//    ConsoleSinkConfig / FileSinkConfig（内置）/ HttpSinkConfig（Http 包）
//
//  通过 AddSinkByConfig(ISinkConfig) 统一注册，构建器不感知内部实现。
// ================================================================
public static class ISinkConfigExample
{
    public static ILogger Build()
    {
        // 可以从外部加载为 POCO 对象，再传入
        ISinkConfig[] sinkConfigs =
        [
            new ConsoleSinkConfig
            {
                ToJson   = false,
                IsColor  = true,
                Enabled  = true,
                FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Debug },
            },

            new FileSinkConfig
            {
                LogFilePath    = "Logs/Runtime.log",
                MaxFileSizeMB  = 10,
                RotateOnNewDay = false,
                MaxFile        = 5,
                ToJson         = false,
                Enabled        = true,
                FilterConfig   = new FilterConfig { LogMinLevel = LogLevel.Info },
            },

            new FileSinkConfig
            {
                LogFilePath    = "Logs/Error.log",
                MaxFileSizeMB  = 5,
                RotateOnNewDay = true,
                MaxFile        = 30,
                Enabled        = true,
                FilterConfig   = new FilterConfig
                {
                    LogMinLevel = LogLevel.Error,
                    LogMaxLevel = LogLevel.Critical,
                },
            },

            // Http 包的 HttpSinkConfig 同样实现 ISinkConfig，可在此处统一注册：
            // new Lunarium.Logging.Http.Config.HttpSinkConfig
            // {
            //     Endpoint   = new Uri("http://localhost:5341/api/events/raw"),
            //     HttpClient = sharedHttpClient,
            //     BatchSize  = 100,
            //     Enabled    = true,
            // },
        ];

        var builder = new LoggerBuilder().SetLoggerName("MyApp");
        foreach (var cfg in sinkConfigs)
            builder.AddSinkByConfig(cfg);
        return builder.Build();
    }
}

// ================================================================
//  第六节：多 Sink 实战场景
//
//  一个典型的生产配置：
//    • 主日志文件（Runtime.log）   接收除高频模块外的所有日志
//    • 模块专属日志                 高频模块独立归档，避免主日志刷屏
//    • Error.log / Warning.log      全局错误/警告汇聚，一站式排查
//    • 审计日志（按天轮转）         按日期归档的审计记录
//    • 控制台                       开发期实时观察，级别可环境变量控制
//    • ChannelSink                   推给 UI 或 WebSocket 广播
// ================================================================
public static class ProductionLoggerConfigurator
{
    /// <summary>
    /// 构建全局单例日志记录器。
    ///
    /// <para><b>环境变量：</b></para>
    /// <list type="table">
    /// <item><term>FILE_LOG_LEVEL</term><description>文件 Sink 最低级别，默认 Info。</description></item>
    /// <item><term>CONSOLE_LOG_LEVEL</term><description>控制台 Sink 最低级别，默认 Info。</description></item>
    /// </list>
    /// </summary>
    public static ILogger Build(out ChannelReader<string> outReader, string? context = null)
    {
        // 从环境变量读取日志级别（可替换为 appsettings.json、命令行参数等）
        var fileLogLevel    = ParseEnvLevel("FILE_LOG_LEVEL",    LogLevel.Info);
        var consoleLogLevel = ParseEnvLevel("CONSOLE_LOG_LEVEL", LogLevel.Info);

        // 全局配置（只调用一次）
        GlobalConfigurator.Configure()
            .UseCustomTimezone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"))
            .UseJsonISO8601Timestamp()
            .UseTextCustomTimestamp("yyyy-MM-dd HH:mm:ss.fff zzz")
            .EnableAutoDestructuring()
            .Apply();

        var rootLogger = new LoggerBuilder()
            .SetLoggerName("Runtime")

            // 主日志：排除高频模块，按大小轮转，最多保留 5 个
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/Runtime/Runtime.log",
                maxFileSizeMB: 10,
                maxFile: 5,
                FilterConfig: new FilterConfig
                {
                    ContextFilterExcludes = ["Runtime.ProxyService", "Runtime.LoginService"],
                    LogMinLevel = fileLogLevel,
                })

            // ProxyService 专属：只收该前缀，给更多保留空间
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/ProxyService/ProxyService.log",
                maxFileSizeMB: 10,
                maxFile: 10,
                FilterConfig: new FilterConfig
                {
                    ContextFilterIncludes = ["Runtime.ProxyService"],
                    LogMinLevel = fileLogLevel,
                })

            // LoginService 专属：独立归档便于安全审计
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/Login/Login.log",
                maxFileSizeMB: 10,
                maxFile: 10,
                FilterConfig: new FilterConfig
                {
                    ContextFilterIncludes = ["Runtime.LoginService"],
                    LogMinLevel = fileLogLevel,
                })

            // 全局错误汇聚：无上下文过滤，所有模块的 Error+ 均写入
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/EW/Error.log",
                maxFileSizeMB: 10,
                maxFile: 5,
                FilterConfig: new FilterConfig
                {
                    LogMinLevel = LogLevel.Error,
                    // LogMaxLevel 默认 Critical，即收 Error 和 Critical
                })

            // 精确收 Warning 级别（MinLevel == MaxLevel）
            .AddSizedRotatingFileSink(
                logFilePath: "Logs/EW/Warning.log",
                maxFileSizeMB: 10,
                maxFile: 5,
                FilterConfig: new FilterConfig
                {
                    LogMinLevel = LogLevel.Warning,
                    LogMaxLevel = LogLevel.Warning,
                })

            // 审计日志：按天轮转，保留 30 天
            .AddTimedRotatingFileSink(
                logFilePath: "Logs/Audit/Audit.log",
                maxFile: 30,
                FilterConfig: new FilterConfig
                {
                    ContextFilterIncludes = ["Runtime.Audit"],
                    LogMinLevel = LogLevel.Info,
                })

            // 叠加轮转：超 50 MB 或新的一天均触发
            .AddRotatingFileSink(
                logFilePath: "Logs/System/System.log",
                maxFileSizeMB: 50,
                rotateOnNewDay: true,
                maxFile: 14,
                FilterConfig: new FilterConfig
                {
                    ContextFilterIncludes = ["Runtime.System"],
                    LogMinLevel = fileLogLevel,
                })

            // 控制台：开发期实时观察，重定向时自动降级为纯文本
            .AddConsoleSink(
                FilterConfig: new FilterConfig { LogMinLevel = consoleLogLevel })

            // ChannelSink：推给 UI/WebSocket；JSON 优先级高于 isColor
            .AddStringChannelSink(
                out var reader,
                capacity: 1000,
                isColor: true,
                toJson: true,
                FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

            .Build();

        outReader = reader;
        return context is not null ? rootLogger.ForContext(context) : rootLogger;
    }

    private static LogLevel ParseEnvLevel(string envKey, LogLevel defaultLevel)
    {
        var raw = Environment.GetEnvironmentVariable(envKey);
        return !string.IsNullOrEmpty(raw) && Enum.TryParse<LogLevel>(raw, true, out var parsed)
            ? parsed
            : defaultLevel;
    }
}
