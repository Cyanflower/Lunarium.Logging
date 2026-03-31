# HTTP Sink

> Full runnable example: [HttpSink.EN.cs](https://github.com/Cyanflower/Lunarium.Logging/blob/main/example/RawCSharp/EN/HttpSink.EN.cs)  
> Required package: `Lunarium.Logging.Http`

```xml
<PackageReference Include="Lunarium.Logging.Http" Version="*" />
```

Zero additional NuGet dependencies — uses only BCL types.

## Generic HTTP Endpoint

Uses `JsonArraySerializer` by default (`application/json`).

```csharp
private static readonly HttpClient SharedClient = new();

var logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddHttpSink(
        httpClient:      SharedClient,
        endpoint:        "http://localhost:9200/logs",
        batchSize:       100,
        flushInterval:   TimeSpan.FromSeconds(5),
        channelCapacity: 1000,
        requestTimeout:  TimeSpan.FromSeconds(30),
        filterConfig:    new FilterConfig { LogMinLevel = LogLevel.Info })
    .Build();
```

## Seq (CLEF/NDJSON)

Automatically uses `ClefSerializer` (`application/vnd.serilog.clef`).  
The `@mt` field stores the raw template; Seq handles structured querying server-side.

```csharp
var logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddSeqSink(
        httpClient:    new HttpClient(),
        seqEndpoint:   "http://localhost:5341/api/events/raw",
        apiKey:        "your-seq-api-key")   // null = no auth header
    .Build();
```

## Grafana Loki

Automatically uses `LokiSerializer` (Loki Push API v1).

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
        })
    .Build();
```

> ⚠️ **Labels must be static.** Do not use dynamic values (user IDs, request IDs, etc.) as labels — each unique label combination creates a new stream in Loki and will cause cardinality explosion. Dynamic data belongs in the log message body.

## HttpSinkConfig — Object-Based Registration

```csharp
var config = new HttpSinkConfig
{
    HttpClient       = new HttpClient(),
    Endpoint         = new Uri("http://localhost:9200/logs"),
    BatchSize        = 100,
    FlushInterval    = TimeSpan.FromSeconds(5),
    DisposeTimeout   = TimeSpan.FromSeconds(5),
    ChannelCapacity  = 1000,
    RequestTimeout   = TimeSpan.FromSeconds(30),
    FilterConfig     = new FilterConfig { LogMinLevel = LogLevel.Info },
};
builder.AddSinkByConfig(config);
```

## Custom Serializer

```csharp
var serializer = new DelegateHttpLogSerializer(
    contentType: "application/x-ndjson",
    serialize: entries =>
    {
        // Build and return an HttpContent from entries
        var lines = entries.Select(e => JsonSerializer.Serialize(e.Message));
        return new StringContent(string.Join('\n', lines), Encoding.UTF8, "application/x-ndjson");
    });

builder.AddHttpSink(
    httpClient:  new HttpClient(),
    endpoint:    "http://my-endpoint/logs",
    serializer:  serializer);
```

## Batching & Reliability

- **Dual trigger**: flush fires when `batchSize` is reached **or** `flushInterval` elapses — whichever comes first.
- **Retry**: one automatic retry on failure. Both attempts failing drops the batch silently.
- **Back-pressure**: when the internal channel is full, new entries are dropped (warning logged once until space frees).
- **Graceful shutdown**: `Dispose()` waits up to `disposeTimeout` for in-flight batches to complete.
