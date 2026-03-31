# HttpSink — HTTP 批量推送

> 完整代码版本：[RawCSharp/HttpSink.ZH.cs](RawCSharp/HttpSink.ZH.cs)
> 需引用 NuGet 包：`Lunarium.Logging.Http`

---

## 第一节：AddHttpSink — 通用 HTTP 推送

默认使用 `JsonArraySerializer`，输出 `application/json` 格式的 JSON 数组。

```csharp
private static readonly HttpClient SharedClient = new();

var logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddHttpSink(
        httpClient:      SharedClient,
        endpoint:        "http://localhost:9200/logs",
        batchSize:       100,
        flushInterval:   TimeSpan.FromSeconds(5),
        disposeTimeout:  TimeSpan.FromSeconds(5),
        channelCapacity: 1000,
        requestTimeout:  TimeSpan.FromSeconds(30),
        filterConfig:    new FilterConfig { LogMinLevel = LogLevel.Info })
    .Build();
```

---

## 第二节：AddSeqSink — 推送到 Seq

自动使用 `ClefSerializer`（CLEF/NDJSON 格式，`application/vnd.serilog.clef`）。

- `@mt` 字段保存原始模板（不渲染），Seq 负责结构化查询
- `apiKey` 不为 null 时自动附加 `X-Seq-ApiKey` Header

```csharp
var logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddSeqSink(
        httpClient:    new HttpClient(),
        seqEndpoint:   "http://localhost:5341/api/events/raw",
        apiKey:        "your-seq-api-key",   // null 时不附加认证 Header
        batchSize:     100,
        flushInterval: TimeSpan.FromSeconds(5))
    .Build();
```

---

## 第三节：AddLokiSink — 推送到 Grafana Loki

自动使用 `LokiSerializer`（Loki Push API v1 格式）。

```csharp
var logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddLokiSink(
        httpClient:   new HttpClient(),
        lokiEndpoint: "http://localhost:3100/loki/api/v1/push",
        labels: new Dictionary<string, string>
        {
            ["app"]         = "my-service",
            ["environment"] = "production",
            ["version"]     = "1.0.0",
        },
        batchSize:    100,
        flushInterval: TimeSpan.FromSeconds(5))
    .Build();
```

> ⚠️ `labels` 必须为**静态值**（服务名、环境名等），不应包含动态内容（用户 ID、请求 ID 等）。动态信息应写入日志消息本身。每个唯一 label 组合在 Loki 中创建独立 stream，动态值会导致 cardinality 爆炸。

---

## 第四节：HttpSinkConfig — 配置对象化

```csharp
var config = new HttpSinkConfig
{
    // ── 必填 ──────────────────────────────────────────────────────────────────
    Endpoint   = new Uri("http://localhost:5341/api/events/raw"),
    HttpClient = new HttpClient(),

    // ── 序列化器（可选，默认 JsonArraySerializer.Default） ──────────────────
    // Serializer = ClefSerializer.Default,
    // Serializer = new LokiSerializer(labels),

    // ── 批量发送参数 ─────────────────────────────────────────────────────────
    BatchSize       = 100,
    FlushInterval   = TimeSpan.FromSeconds(5),
    DisposeTimeout  = TimeSpan.FromSeconds(5),
    ChannelCapacity = 1000,
    RequestTimeout  = TimeSpan.FromSeconds(30),

    // ── 自定义 Header ────────────────────────────────────────────────────────
    Headers = new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer your-token",
    },

    // ── ISinkConfig 通用字段 ─────────────────────────────────────────────────
    Enabled      = true,
    FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Info },
};

var logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddSinkByConfig(config)   // 等价于 .AddHttpSink(config)
    .Build();
```

**HttpSinkConfig 参数说明：**

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Endpoint` | `Uri` | **必填** | 推送端点，必须为 http/https |
| `HttpClient` | `HttpClient` | **必填** | 由调用方管理生命周期 |
| `Serializer` | `IHttpLogSerializer?` | `JsonArraySerializer.Default` | 序列化格式 |
| `Headers` | `IReadOnlyDictionary?` | `null` | 附加请求 Header |
| `BatchSize` | `int` | `100` | 触发批量发送的条目数 |
| `FlushInterval` | `TimeSpan` | `5s` | 强制发送的时间间隔 |
| `DisposeTimeout` | `TimeSpan` | `5s` | Dispose 时排空队列的超时 |
| `ChannelCapacity` | `int` | `1000` | 内部 Channel 容量 |
| `RequestTimeout` | `TimeSpan` | `30s` | 单次 HTTP 请求超时 |

---

## 第五节：自定义序列化器

当内置格式不满足需求时，使用 `DelegateHttpLogSerializer` 包装任意逻辑。

```csharp
var customSerializer = new DelegateHttpLogSerializer(
    serialize: entries =>
    {
        var lines = string.Join('\n',
            entries.Select(e => $"{e.Timestamp:O} {e.LogLevel} {e.Message}"));
        return new StringContent(lines, Encoding.UTF8, "text/plain");
    },
    contentType: "text/plain");

var logger = new LoggerBuilder()
    .AddHttpSink(new HttpClient(), "http://my-server/ingest", serializer: customSerializer)
    .Build();
```

> ⚠️ 序列化委托必须是**同步**的（不能含 `await`）。内部使用 `[ThreadStatic]` 资源，跨 `await` 点使用不安全。

---

## 第六节：HttpClient 管理建议

`HttpTarget` **不会** Dispose 传入的 `HttpClient`，生命周期由调用方管理。

```csharp
// 推荐：静态单例（非 ASP.NET Core 场景）
private static readonly HttpClient _client = new()
{
    Timeout = Timeout.InfiniteTimeSpan,  // 由 RequestTimeout 参数控制
};

// 推荐：IHttpClientFactory（ASP.NET Core 场景）
services.AddHttpClient("lunarium-sink");
services.AddSingleton<ILogger>(sp =>
{
    var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("lunarium-sink");
    return new LoggerBuilder()
        .AddHttpSink(client, "http://localhost:9200/logs")
        .Build();
});
```

---

## 第七节：调优建议

### BatchSize 与 FlushInterval（双触发机制）

两者互补，任意一个先满足即触发 flush：

| 参数 | 保障目标 | 建议值 |
|------|----------|--------|
| `BatchSize` | 吞吐量：日志量大时及时发送，避免内存积压 | 50–200 |
| `FlushInterval` | 延迟：日志量小时也能在合理时间内发出 | 2s–10s |

### ChannelCapacity 与溢出

- 满时采用 **DropWrite**（丢弃最新写入），不阻塞调用方线程
- 首次溢出时 `InternalLogger` 打印一次警告；flush 成功后重置
- 建议根据峰值日志量和网络延迟估算，一般设 1000–5000

### RequestTimeout 建议

建议为 `FlushInterval` 的 2–3 倍，确保单次请求不阻塞整个 flush 循环。内网推送可降至 5–10s。
