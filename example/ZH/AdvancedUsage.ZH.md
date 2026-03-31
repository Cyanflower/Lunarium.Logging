# AdvancedUsage — 自定义扩展 + AOT + 运行时配置

> 完整代码版本：[RawCSharp/AdvancedUsage.ZH.cs](RawCSharp/AdvancedUsage.ZH.cs)

---

## 第一节：LogEntry 字段说明

`LogEntry` 是不可变的日志事件对象，传递给所有 `ILogTarget.Emit()`。

| 字段 | 类型 | 说明 |
|------|------|------|
| `LoggerName` | `string` | Logger 实例名称 |
| `LoggerNameBytes` | `ReadOnlyMemory<byte>` | LoggerName 的 UTF-8 预编码字节 |
| `LogLevel` | `LogLevel` | 日志级别 |
| `Timestamp` | `DateTimeOffset` | 时间戳（按 GlobalConfigurator 时区） |
| `Message` | `string` | 原始消息模板字符串（未渲染） |
| `Properties` | `object?[]` | 消息模板属性值数组 |
| `Context` | `string` | 上下文字符串（ForContext 设置） |
| `ContextBytes` | `ReadOnlyMemory<byte>` | Context 的 UTF-8 预编码字节 |
| `Scope` | `string` | MEL Scope 信息 |
| `Exception` | `Exception?` | 关联异常 |
| `MessageTemplate` | `MessageTemplate` | 延迟解析的模板（Emit 时由 Sink 填充） |

> `MessageTemplate.MessageTemplateTokens` 为 `public`，供自定义 Sink 渲染消息和提取属性。

---

## 第二节：自定义 ILogTarget

提供三条扩展路径：

| 路径 | 方式 | 适用场景 |
|------|------|----------|
| A | 实现 `ILogTarget` | 同步处理（内存缓冲、直接写入） |
| B | 继承 `ChannelTarget<T>` | 异步处理（数据库写入、网络推送） |
| C | 实现 `IJsonTextTarget` / `ITextTarget` | 让 FilterConfig 统一控制输出格式 |

### 路径 A：实现 ILogTarget（内存缓冲 Sink）

```csharp
public sealed class InMemoryLogTarget : ILogTarget
{
    private readonly List<LogEntry> _entries = new();
    private readonly Lock _lock = new();

    public void Emit(LogEntry entry)
    {
        lock (_lock)
            _entries.Add(entry);
    }

    public IReadOnlyList<LogEntry> TakeAll()
    {
        lock (_lock)
        {
            var snapshot = _entries.ToList();
            _entries.Clear();
            return snapshot;
        }
    }

    public void Dispose() { }
}

// 注册方式：
var memTarget = new InMemoryLogTarget();
var logger = new LoggerBuilder()
    .SetLoggerName("Test")
    .AddSink(memTarget, filterConfig: null, name: "memory")
    .Build();
```

### 路径 B：继承 ChannelTarget\<T\>（异步数据库 Sink）

只需实现 `Transform(LogEntry) → T`。基类接受 `ChannelWriter<T>`，调用方创建 Channel 并持有 Reader。

```csharp
public sealed class DatabaseLogTarget : ChannelTarget<DatabaseLogRecord>
{
    private readonly string _connectionString;

    public DatabaseLogTarget(string connectionString, ChannelWriter<DatabaseLogRecord> writer)
        : base(writer)
    {
        _connectionString = connectionString;
    }

    protected override DatabaseLogRecord Transform(LogEntry entry) =>
        new()
        {
            Timestamp = entry.Timestamp.UtcDateTime,
            Level     = entry.LogLevel.ToString(),
            Logger    = entry.LoggerName,
            Context   = entry.Context,
            Message   = entry.Message,
            Exception = entry.Exception?.ToString(),
        };
}

// 注册方式：调用方创建 Channel，分别传入 Writer 给 Target，保留 Reader 供消费
var channel  = Channel.CreateBounded<DatabaseLogRecord>(1000);
var dbTarget = new DatabaseLogTarget("Server=...;", channel.Writer);
var logger   = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddSink(dbTarget)
    .Build();

// 后台消费：
_ = Task.Run(async () =>
{
    await foreach (var record in channel.Reader.ReadAllAsync())
    {
        // await conn.ExecuteAsync("INSERT INTO Logs ...", record);
    }
});
```

---

## 第三节：IDestructurable — 高性能自定义解构

通过 `DestructureHelper`（包装 `Utf8JsonWriter`）直接写入输出缓冲区，无中间字符串分配。与 AOT 完全兼容。

**优先级：`IDestructurable` > `IDestructured` > `JsonSerializer` 回退（AOT 下未注册类型降级为 `ToString()`）**

```csharp
public sealed class Order : IDestructurable
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public decimal Amount { get; init; }

    public void Destructure(DestructureHelper helper)
    {
        helper.WriteStartObject();
        helper.WriteNumber("id",     Id);
        helper.WriteString("name",   Name);
        helper.WriteNumber("amount", (double)Amount);
        helper.WriteEndObject();
    }
}

// 使用：
logger.Info("新订单 {@Order}", new Order { Id = 42, Name = "书", Amount = 99.9m });
// JSON 输出：{"id":42,"name":"书","amount":99.9}
```

**DestructureHelper 常用方法：**

| 方法 | 说明 |
|------|------|
| `WriteStartObject()` / `WriteEndObject()` | 开始/结束 JSON 对象 |
| `WriteStartArray()` / `WriteEndArray()` | 开始/结束 JSON 数组 |
| `WriteString(key, value)` | 写入字符串字段 |
| `WriteNumber(key, int/long/double)` | 写入数字字段 |
| `WriteBoolean(key, bool)` | 写入布尔字段 |
| `WriteNull(key)` | 写入 null 字段 |
| `WritePropertyName(key)` | 写入字段名（后接任意值方法） |
| `WriteStringValue(value)` | 写入数组中的字符串元素 |
| `WriteNumberValue(int/long/double)` | 写入数组中的数字元素 |

---

## 第四节：IDestructured — 极致零分配（预序列化字节）

适合静态或不可变数据：提前计算好 UTF-8 JSON 字节，Emit 时直接嵌入，零分配。

```csharp
public sealed class AppVersion : IDestructured
{
    // 进程内复用的静态字节
    private static readonly byte[] _bytes =
        System.Text.Encoding.UTF8.GetBytes("""{"major":1,"minor":2,"patch":0}""");

    public ReadOnlyMemory<byte> Destructured() => _bytes;
}

// 使用：
logger.Info("版本 {@Version}", new AppVersion());
// JSON 输出：{"major":1,"minor":2,"patch":0}
```

---

## 第五节：AOT（Native AOT / Trimming）配置

主库标记 `IsAotCompatible=true`。`{@Object}` 解构在 AOT 下需提前注册 Source Generated `JsonSerializerContext`，否则静默降级为 `ToString()`（无运行时异常）。

**步骤一：创建 JsonSerializerContext**

```csharp
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(List<Order>))]
internal partial class MyAppJsonContext : JsonSerializerContext { }
```

**步骤二：在 GlobalConfigurator 中注册**

```csharp
GlobalConfigurator.Configure()
    .UseJsonTypeInfoResolver(MyAppJsonContext.Default)
    // 多个 Context 合并：
    // .UseJsonTypeInfoResolver(
    //     JsonTypeInfoResolver.Combine(MyAppJsonContext.Default, AnotherContext.Default))
    .Apply();
```

> 实现了 `IDestructurable` 或 `IDestructured` 的类型不受此限制，无需在 Context 中注册。

---

## 第六节：LoggerManager — 运行时动态配置

所有 `Build()` 创建的 Logger 自动注册到 `LoggerManager`，可在不重启的情况下更新 Sink 配置。

```csharp
// 查看已注册的 Logger 名称
var names = LoggerManager.GetLoggerList();
// 返回：["Runtime", "Analytics", ...]

// 更新单个 Sink 的 FilterConfig
LoggerManager.UpdateSinkConfig(
    loggerName: "Runtime",
    sinkName:   "console",   // 构建时通过 sinkName 参数指定的名称
    sinkConfig: new ConsoleSinkConfig
    {
        Enabled      = true,
        FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Debug },
    });

// 批量更新单个 Logger 的所有 Sink
LoggerManager.UpdateLoggerConfig(loggerConfigs: new LoggerConfig { ... });

// 批量更新所有已注册 Logger（配置文件热更新时使用）
LoggerManager.UpdateAllLoggerConfig(newLoggerConfigs);
```

**注意事项：**

- `UpdateSinkConfig` / `UpdateLoggerConfig` 只更新现有 Sink 的 `FilterConfig` 和 `Enabled`，不能新增或删除 Sink（Sink 列表在 `Build()` 时固定）
- `LogFilePath` 等影响 Target 构造的参数无法热更新，需重启
- 所有方法均为线程安全（`Interlocked.Exchange` 原子操作）
