# HttpSink — HTTP Batch Push

> Full code version: [RawCSharp/HttpSink.EN.cs](RawCSharp/HttpSink.EN.cs)
> Required NuGet package: `Lunarium.Logging.Http`

---

## Section 1: AddHttpSink — Generic HTTP Push

Uses `JsonArraySerializer` by default, producing an `application/json` JSON array.

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

## Section 2: AddSeqSink — Push to Seq

Automatically uses `ClefSerializer` (CLEF/NDJSON format, `application/vnd.serilog.clef`).

- The `@mt` field stores the raw template (not rendered); Seq handles structured querying server-side
- When `apiKey` is not null, the `X-Seq-ApiKey` header is automatically added

```csharp
var logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddSeqSink(
        httpClient:    new HttpClient(),
        seqEndpoint:   "http://localhost:5341/api/events/raw",
        apiKey:        "your-seq-api-key",   // null = no auth header added
        batchSize:     100,
        flushInterval: TimeSpan.FromSeconds(5))
    .Build();
```

---

## Section 3: AddLokiSink — Push to Grafana Loki

Automatically uses `LokiSerializer` (Loki Push API v1 format).

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

> ⚠️ `labels` must be **static values** (service name, environment name, etc.) — do not include dynamic content (user IDs, request IDs, etc.). Dynamic data should go into the log message itself. Each unique label combination creates an independent stream in Loki; dynamic values cause stream cardinality explosion.

---

## Section 4: HttpSinkConfig — Object-Based Configuration

```csharp
var config = new HttpSinkConfig
{
    // ── Required ──────────────────────────────────────────────────────────────
    Endpoint   = new Uri("http://localhost:5341/api/events/raw"),
    HttpClient = new HttpClient(),

    // ── Serializer (optional, defaults to JsonArraySerializer.Default) ────────
    // Serializer = ClefSerializer.Default,
    // Serializer = new LokiSerializer(labels),

    // ── Batch send parameters ─────────────────────────────────────────────────
    BatchSize       = 100,
    FlushInterval   = TimeSpan.FromSeconds(5),
    DisposeTimeout  = TimeSpan.FromSeconds(5),
    ChannelCapacity = 1000,
    RequestTimeout  = TimeSpan.FromSeconds(30),

    // ── Custom headers ────────────────────────────────────────────────────────
    Headers = new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer your-token",
    },

    // ── ISinkConfig common fields ─────────────────────────────────────────────
    Enabled      = true,
    FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Info },
};

var logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddSinkByConfig(config)   // equivalent to .AddHttpSink(config)
    .Build();
```

**HttpSinkConfig parameter reference:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Endpoint` | `Uri` | **required** | Push endpoint; must be http/https |
| `HttpClient` | `HttpClient` | **required** | Lifetime managed by the caller |
| `Serializer` | `IHttpLogSerializer?` | `JsonArraySerializer.Default` | Serialization format |
| `Headers` | `IReadOnlyDictionary?` | `null` | Additional request headers |
| `BatchSize` | `int` | `100` | Number of entries that trigger a batch send |
| `FlushInterval` | `TimeSpan` | `5s` | Maximum time before a forced flush |
| `DisposeTimeout` | `TimeSpan` | `5s` | Timeout to drain the queue on Dispose |
| `ChannelCapacity` | `int` | `1000` | Internal Channel capacity |
| `RequestTimeout` | `TimeSpan` | `30s` | Per-request HTTP timeout |

---

## Section 5: Custom Serializer

When the built-in formats don't meet your needs, use `DelegateHttpLogSerializer` to wrap any serialization logic.

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

> ⚠️ The serialization delegate must be **synchronous** (no `await`). Internal `[ThreadStatic]` resources are unsafe across `await` points.

---

## Section 6: HttpClient Management Recommendations

`HttpTarget` **will NOT** Dispose the provided `HttpClient`; its lifetime is managed by the caller.

```csharp
// Recommended: static singleton (non-ASP.NET Core scenarios)
private static readonly HttpClient _client = new()
{
    Timeout = Timeout.InfiniteTimeSpan,  // Timeout is controlled by the RequestTimeout parameter
};

// Recommended: IHttpClientFactory (ASP.NET Core scenarios)
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

## Section 7: Tuning Recommendations

### BatchSize and FlushInterval (dual-trigger mechanism)

The two parameters are complementary; whichever condition is met first triggers a flush:

| Parameter | Guarantee | Recommended value |
|-----------|-----------|-------------------|
| `BatchSize` | Throughput: sends promptly under high log volume to prevent memory pressure | 50–200 |
| `FlushInterval` | Latency: ensures timely delivery even under low log volume | 2s–10s |

### ChannelCapacity and Overflow Behavior

- When full, uses **DropWrite** (discards the newest entry) — caller is never blocked
- On the first overflow, `InternalLogger` prints a one-time warning; resets after a successful flush
- Set capacity based on estimated peak log rate and network latency; 1000–5000 is a reasonable starting range

### RequestTimeout Recommendations

Set to 2–3× `FlushInterval` to ensure a single request doesn't block the entire flush loop. For intranet pushes, 5–10s may be more appropriate.
