# SinkConfiguration — Sink 配置完整参考

> 完整代码版本：[RawCSharp/SinkConfiguration.ZH.cs](RawCSharp/SinkConfiguration.ZH.cs)

---

## 第一节：GlobalConfigurator — 全局配置

必须在第一次 `Build()` 之前调用，整个进程只能调用一次。不调用时 `Build()` 自动应用默认值。

```csharp
GlobalConfigurator.Configure()
    // 时区（默认：本地时间）
    .UseCustomTimezone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"))
    // .UseUtcTimeZone()
    // .UseLocalTimeZone()

    // JSON Sink 时间戳格式（默认：ISO 8601）
    .UseJsonISO8601Timestamp()
    // .UseJsonUnixTimestamp()      // Unix 秒（long）
    // .UseJsonUnixMsTimestamp()    // Unix 毫秒（long）
    // .UseJsonCustomTimestamp("yyyy-MM-ddTHH:mm:ss")

    // 文本 Sink 时间戳格式（默认："yyyy-MM-dd HH:mm:ss.fff"）
    .UseTextCustomTimestamp("yyyy-MM-dd HH:mm:ss.fff zzz")
    // .UseTextISO8601Timestamp()
    // .UseTextUnixTimestamp()
    // .UseTextUnixMsTimestamp()

    // 集合自动解构（默认：关闭）
    // 开启后 List<T>、Dictionary、数组等无需 {@} 前缀即可序列化为 JSON
    .EnableAutoDestructuring()

    // JSON 中文转义（默认：保留原始中文）
    // .DisableUnsafeRelaxedJsonEscaping()    // 将中文/Emoji 转义为 \uXXXX
    // .EnableUnsafeRelaxedJsonEscaping()     // 显式保留原始字符（与默认相同）

    // JSON 缩进（默认：紧凑单行）
    // .UseIndentedJson()
    // .UseCompactJson()

    .Apply();
```

**默认值汇总：**

| 配置项 | 默认值 |
|--------|--------|
| 时区 | 本地时间 |
| JSON 时间戳 | ISO 8601（`"O"`） |
| 文本时间戳 | `"yyyy-MM-dd HH:mm:ss.fff"` |
| 集合自动解构 | 关闭 |
| JSON 中文转义 | 保留原始中文 |
| JSON 缩进 | 紧凑单行 |

---

## 第二节：FilterConfig — 级别过滤与上下文过滤

每个 Sink 独立一份 FilterConfig，互不影响。

**过滤执行顺序：**
1. 级别检查：`LogMinLevel ≤ entry.LogLevel ≤ LogMaxLevel`
2. Include 检查：Context 前缀命中任一 Include 项（空列表 = 全部通过）
3. Exclude 检查：Context 前缀命中任一 Exclude 项则丢弃

```csharp
// 只收 Info 及以上（最常见）
new FilterConfig { LogMinLevel = LogLevel.Info }

// 精确匹配单个级别
new FilterConfig { LogMinLevel = LogLevel.Warning, LogMaxLevel = LogLevel.Warning }

// Context Include 白名单
new FilterConfig
{
    ContextFilterIncludes = ["Payment", "Order"],
    LogMinLevel = LogLevel.Debug,
}

// Context Exclude 黑名单
new FilterConfig
{
    ContextFilterExcludes = ["Proxy", "Heartbeat"],
    LogMinLevel = LogLevel.Info,
}

// Include + Exclude 同时使用（先 Include，后 Exclude）
new FilterConfig
{
    ContextFilterIncludes = ["Runtime"],
    ContextFilterExcludes = ["Runtime.Proxy"],
    LogMinLevel = LogLevel.Info,
}

// 大小写不敏感匹配
new FilterConfig
{
    ContextFilterIncludes = ["Payment"],
    IgnoreFilterCase = true,
}
```

---

## 第三节：TextOutputIncludeConfig — 文本输出字段可见性

适用于 ConsoleSink、FileSink（文本模式）、StringChannelSink、Utf8ByteChannelSink。

默认输出格式：
```
[2026-03-30 14:00:00.000] [INFO ] [MyApp] [Order.Service] 消息内容
```

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `IncludeTimestamp` | bool | true | 是否显示时间戳 |
| `IncludeLevel` | bool | true | 是否显示日志级别 |
| `IncludeLoggerName` | bool | true | 是否显示 Logger 名称 |
| `IncludeContext` | bool | true | 是否显示 Context |

```csharp
// 极简模式：只显示级别和消息
new TextOutputIncludeConfig
{
    IncludeTimestamp  = false,
    IncludeLevel      = true,
    IncludeLoggerName = false,
    IncludeContext    = false,
}
```

---

## 第四节：所有 Sink 类型

### ConsoleSink

非重定向时自动彩色输出；`Error`/`Critical` → stderr，其余 → stdout。

```csharp
.AddConsoleSink(
    isColor: true,    // 彩色（重定向或 NO_COLOR 时自动降级）
    toJson: false,    // false=文本；true=JSON
    textOutputIncludeConfig: new TextOutputIncludeConfig { ... },
    FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })
```

### FileSink（无轮转）

```csharp
.AddFileSink(logFilePath: "Logs/app.log")
```

### SizedRotatingFileSink（按大小轮转）

超过 `maxFileSizeMB` 时轮转，新文件名含完整时间戳：`app-2026-03-30-14-00-00.log`

```csharp
.AddSizedRotatingFileSink(
    logFilePath: "Logs/app.log",
    maxFileSizeMB: 10,
    maxFile: 5,        // 最多保留 5 个历史文件；0 = 不限
    FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })
```

### TimedRotatingFileSink（按天轮转）

每天 00:00 轮转，文件名含日期：`app-2026-03-30.log`

```csharp
.AddTimedRotatingFileSink(
    logFilePath: "Logs/app.log",
    maxFile: 30,       // 最多保留 30 天
    FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })
```

### RotatingFileSink（叠加轮转：大小 + 按天）

任一条件满足即触发，文件名含完整时间戳。

```csharp
.AddRotatingFileSink(
    logFilePath: "Logs/app.log",
    maxFileSizeMB: 50,
    rotateOnNewDay: true,
    maxFile: 14)
```

### StringChannelSink

格式化为 `string` 推入 Channel，供外部消费（UI/WebSocket）。每次 Emit 有一次 string 堆分配。

```csharp
.AddStringChannelSink(
    out ChannelReader<string> reader,
    capacity: 1000,
    isColor: false,
    toJson: false,
    FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })
```

### Utf8ByteChannelSink

格式化为 `byte[]` 推入 Channel，跳过 UTF-8→string 解码。适合下游直接写网络/文件。

```csharp
.AddUtf8ByteChannelSink(
    out ChannelReader<byte[]> reader,
    capacity: 1000,
    toJson: true)
```

### LogEntryChannelSink

直接将 `LogEntry` 对象推入 Channel，零格式化开销。适合需要完整结构化数据的场景。

```csharp
.AddLogEntryChannelSink(
    out ChannelReader<LogEntry> reader,
    capacity: 500,
    FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Warning })
```

**三种 ChannelSink 性能对比：**

| Sink | 格式化 | 下游使用场景 |
|------|--------|-------------|
| `StringChannelSink` | UTF-8 → string（一次分配） | 直接展示字符串 |
| `Utf8ByteChannelSink` | UTF-8 字节 → byte[]（一次拷贝） | 直接写网络/文件 |
| `LogEntryChannelSink` | 无格式化 | 自定义处理逻辑 |

---

## 第五节：ISinkConfig 配置对象化

适合配置来自外部（JSON 文件、数据库、配置中心）的场景，通过 `AddSinkByConfig()` 统一注册。

```csharp
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
        Enabled        = true,
        FilterConfig   = new FilterConfig { LogMinLevel = LogLevel.Info },
    },
    new FileSinkConfig
    {
        LogFilePath  = "Logs/Error.log",
        MaxFileSizeMB = 5,
        RotateOnNewDay = true,
        MaxFile      = 30,
        FilterConfig = new FilterConfig
        {
            LogMinLevel = LogLevel.Error,
            LogMaxLevel = LogLevel.Critical,
        },
    },
    // Http 包同样实现 ISinkConfig：
    // new Lunarium.Logging.Http.Config.HttpSinkConfig { Endpoint = ..., HttpClient = ... }
];

var builder = new LoggerBuilder().SetLoggerName("MyApp");
foreach (var cfg in sinkConfigs)
    builder.AddSinkByConfig(cfg);
var logger = builder.Build();
```

---

## 第六节：多 Sink 实战场景

典型的生产配置：主日志 + 模块专属 + 全局错误/警告汇聚 + 控制台 + ChannelSink。

> 完整代码见 [RawCSharp/SinkConfiguration.ZH.cs](RawCSharp/SinkConfiguration.ZH.cs) 的 `ProductionLoggerConfigurator` 类。

```csharp
var logger = new LoggerBuilder()
    .SetLoggerName("Runtime")

    // 主日志：排除高频模块，按大小轮转
    .AddSizedRotatingFileSink("Logs/Runtime/Runtime.log", 10, 5,
        FilterConfig: new FilterConfig
        {
            ContextFilterExcludes = ["Runtime.ProxyService", "Runtime.LoginService"],
            LogMinLevel = LogLevel.Info,
        })

    // 模块专属：只收对应 Context 前缀
    .AddSizedRotatingFileSink("Logs/ProxyService/ProxyService.log", 10, 10,
        FilterConfig: new FilterConfig
        {
            ContextFilterIncludes = ["Runtime.ProxyService"],
            LogMinLevel = LogLevel.Info,
        })

    // 全局错误汇聚：无 Context 过滤，所有模块的 Error+ 均写入
    .AddSizedRotatingFileSink("Logs/EW/Error.log", 10, 5,
        FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Error })

    // 精确收 Warning 一个级别
    .AddSizedRotatingFileSink("Logs/EW/Warning.log", 10, 5,
        FilterConfig: new FilterConfig
        {
            LogMinLevel = LogLevel.Warning,
            LogMaxLevel = LogLevel.Warning,
        })

    // 控制台：开发期实时观察
    .AddConsoleSink(FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

    // ChannelSink：推给 UI 或 WebSocket
    .AddStringChannelSink(out var reader, capacity: 1000, toJson: true,
        FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Info })

    .Build();
```
