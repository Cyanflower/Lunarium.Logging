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
//  本文件是 Lunarium.Logging.Http 的集成示例，仅供参考，不参与编译。
//  涵盖 AddHttpSink / AddSeqSink / AddLokiSink、HttpSinkConfig 配置对象化、
//  自定义序列化器，以及 HttpClient 管理建议。
//  需引用 NuGet 包：Lunarium.Logging.Http
// ============================================================

using Lunarium.Logging.Http;

using Lunarium.Logging.Http.Config;
using Lunarium.Logging.Http.Serializer;

using Lunarium.Logging.Target;
using Lunarium.Logging.Configuration;
using Lunarium.Logging.Extensions;
using Microsoft.Extensions.Hosting;

namespace Lunarium.Logging;

// ================================================================
//  第一节：AddHttpSink — 通用 HTTP 推送（JSON 数组格式）
//
//  默认使用 JsonArraySerializer，输出 application/json 格式：
//  [{"timestamp":"...","level":"Information","logger":"...","message":"..."}]
// ================================================================
public static class BasicHttpSinkExample
{
    // ⚠️ HttpClient 由调用方创建和管理，HttpTarget 不会 Dispose 它。
    //    推荐：使用静态单例或 IHttpClientFactory（ASP.NET Core 场景）。
    private static readonly HttpClient SharedClient = new();

    public static ILogger Build()
    {
        return new LoggerBuilder()
            .SetLoggerName("MyApp")
            .AddHttpSink(
                httpClient:      SharedClient,
                endpoint:        "http://localhost:9200/logs",  // 接收端 URL
                batchSize:       100,            // 累积到 100 条后批量发送
                flushInterval:   TimeSpan.FromSeconds(5),   // 最长等待 5 秒后强制发送
                disposeTimeout:  TimeSpan.FromSeconds(5),   // Dispose 时等待排空的超时
                channelCapacity: 1000,           // 内部 Channel 容量；满时丢弃最新条目
                requestTimeout:  TimeSpan.FromSeconds(30),  // 单次请求超时
                filterConfig:    new FilterConfig { LogMinLevel = LogLevel.Info })
            .Build();
    }
}

// ================================================================
//  第二节：AddSeqSink — 推送到 Seq（CLEF/NDJSON 格式）
//
//  自动使用 ClefSerializer（Content-Type: application/vnd.serilog.clef）。
//  @mt 字段保存原始模板（不渲染），Seq 服务端负责渲染和结构化查询。
//  apiKey 不为 null 时，自动附加 X-Seq-ApiKey Header。
// ================================================================
public static class SeqSinkExample
{
    private static readonly HttpClient SeqClient = new();

    public static ILogger Build()
    {
        return new LoggerBuilder()
            .SetLoggerName("MyApp")
            .AddSeqSink(
                httpClient:   SeqClient,
                seqEndpoint:  "http://localhost:5341/api/events/raw",
                apiKey:       "your-seq-api-key",   // null 时不附加认证 Header
                batchSize:    100,
                flushInterval: TimeSpan.FromSeconds(5))
            .Build();
    }
}

// ================================================================
//  第三节：AddLokiSink — 推送到 Grafana Loki（Push API v1）
//
//  自动使用 LokiSerializer，输出 Loki Push API 格式：
//  {"streams":[{"stream":{"app":"myservice"},"values":[["<ns>","<line>"]]}]}
//
//  ⚠️ labels 必须为静态值（服务名、环境名等），不应含动态内容，
//     否则会导致 Loki stream cardinality 爆炸（每个唯一 label 组合创建新 stream）。
// ================================================================
public static class LokiSinkExample
{
    private static readonly HttpClient LokiClient = new();

    public static ILogger Build()
    {
        return new LoggerBuilder()
            .SetLoggerName("MyApp")
            .AddLokiSink(
                httpClient:   LokiClient,
                lokiEndpoint: "http://localhost:3100/loki/api/v1/push",
                labels: new Dictionary<string, string>
                {
                    ["app"]         = "my-service",
                    ["environment"] = "production",
                    ["version"]     = "1.0.0",
                    // ⚠️ 不要在这里放动态值（用户ID、请求ID 等），
                    //    动态信息应写入日志消息本身
                },
                batchSize:    100,
                flushInterval: TimeSpan.FromSeconds(5))
            .Build();
    }
}

// ================================================================
//  第四节：HttpSinkConfig — 配置对象化
//
//  适合从外部配置源（数据库、配置中心）读取后传入，
//  或统一走 AddSinkByConfig() 注册（与其他 ISinkConfig 实现一致）。
// ================================================================
public static class HttpSinkConfigExample
{
    public static ILogger Build()
    {
        var config = new HttpSinkConfig
        {
            // ── 必填 ──────────────────────────────────────────────────────────
            Endpoint   = new Uri("http://localhost:5341/api/events/raw"),
            HttpClient = new HttpClient(),   // 由调用方管理生命周期

            // ── 序列化器（可选，默认 JsonArraySerializer.Default） ────────────
            // Serializer = ClefSerializer.Default,   // Seq
            // Serializer = new LokiSerializer(labels),  // Loki

            // ── 批量发送参数 ───────────────────────────────────────────────────
            BatchSize       = 100,                        // 默认 100
            FlushInterval   = TimeSpan.FromSeconds(5),    // 默认 5s
            DisposeTimeout  = TimeSpan.FromSeconds(5),    // 默认 5s
            ChannelCapacity = 1000,                       // 默认 1000
            RequestTimeout  = TimeSpan.FromSeconds(30),   // 默认 30s

            // ── 自定义 Header ──────────────────────────────────────────────────
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer your-token",
            },

            // ── ISinkConfig 通用字段 ───────────────────────────────────────────
            Enabled      = true,
            FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Info },
        };

        return new LoggerBuilder()
            .SetLoggerName("MyApp")
            .AddSinkByConfig(config)   // 等价于 .AddHttpSink(config)
            .Build();
    }
}

// ================================================================
//  第五节：自定义序列化器（DelegateHttpLogSerializer）
//
//  当内置的 JsonArray / CLEF / Loki 格式都不满足需求时，
//  可用 DelegateHttpLogSerializer 包装任意序列化逻辑。
//
//  ⚠️ 序列化委托必须是同步的（不能含 await）。
//     内部使用 [ThreadStatic] 资源，跨 await 点使用不安全。
// ================================================================
public static class CustomSerializerExample
{
    public static ILogger Build()
    {
        // 自定义序列化：将日志批次转换为换行分隔的纯文本
        var customSerializer = new DelegateHttpLogSerializer(
            serialize: entries =>
            {
                var lines = string.Join('\n', entries.Select(e => $"{e.Timestamp:O} {e.LogLevel} {e.Message}"));
                return new StringContent(lines, System.Text.Encoding.UTF8, "text/plain");
            },
            contentType: "text/plain");

        return new LoggerBuilder()
            .SetLoggerName("MyApp")
            .AddHttpSink(
                httpClient: new HttpClient(),
                endpoint:   "http://my-log-server/ingest",
                serializer: customSerializer)
            .Build();
    }
}

// ================================================================
//  第六节：HttpClient 管理建议
//
//  原则：HttpTarget 不 Dispose 传入的 HttpClient，生命周期由调用方管理。
// ================================================================
public static class HttpClientManagementAdvice
{
    // ── 推荐方案一：静态单例（非 ASP.NET Core 场景） ──────────────────────────
    // HttpClient 是线程安全的，可以安全地跨线程共享。
    // 应用生命周期内只创建一次，随进程退出释放。
    private static readonly HttpClient _client = new()
    {
        Timeout = Timeout.InfiniteTimeSpan,  // 由 requestTimeout 参数控制超时
    };

    // ── 推荐方案二：IHttpClientFactory（ASP.NET Core 场景） ──────────────────
    // 通过 DI 注入 IHttpClientFactory，避免 socket 耗尽问题。
    //
    // services.AddHttpClient("lunarium-sink");
    //
    // 在 DI 容器中注册 Logger：
    // services.AddSingleton<ILogger>(sp =>
    // {
    //     var factory = sp.GetRequiredService<IHttpClientFactory>();
    //     var client  = factory.CreateClient("lunarium-sink");
    //     return new LoggerBuilder()
    //         .AddHttpSink(client, "http://localhost:9200/logs")
    //         .Build();
    // });

    // ── 注意事项 ──────────────────────────────────────────────────────────────
    // • 不要为每个请求 new HttpClient()，会造成 socket 耗尽。
    // • 不要在 HttpTarget.Dispose 后继续使用同一 HttpClient 发其他请求（无影响，
    //   但 HttpTarget Dispose 后 Channel 已关闭，不会再发请求）。
}

// ================================================================
//  第七节：调优建议
//
//  BatchSize 与 FlushInterval 是互补的双触发机制：
//    • BatchSize：吞吐量保障 —— 日志量大时及时发送，避免内存积压
//    • FlushInterval：延迟保障 —— 日志量小时也能在合理时间内发出
//
//  ChannelCapacity 与溢出行为：
//    • 满时采用 DropWrite（丢弃新写入），不阻塞调用方
//    • 第一次溢出时 InternalLogger 会打印警告；flush 成功后重置，下次溢出再警告
//    • 建议根据峰值日志量和网络延迟估算合适容量，一般 1000-5000
//
//  RequestTimeout 建议：
//    • 设为 FlushInterval 的 2-3 倍，确保单次请求不会阻塞整个 flush 循环
//    • 默认 30s 适合大多数场景，内网推送可适当降低到 5-10s
// ================================================================
