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
//  This file is an integration example for Lunarium.Logging.Http.
//  For reference only — not compiled into the library.
//  Covers AddHttpSink / AddSeqSink / AddLokiSink, HttpSinkConfig
//  object-based configuration, custom serializers, and HttpClient
//  management recommendations.
//  Required NuGet package: Lunarium.Logging.Http
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
//  Section 1: AddHttpSink — Generic HTTP Push (JSON array format)
//
//  Uses JsonArraySerializer by default, producing application/json:
//  [{"timestamp":"...","level":"Information","logger":"...","message":"..."}]
// ================================================================
public static class BasicHttpSinkExample
{
    // ⚠️ HttpClient is created and managed by the caller; HttpTarget will NOT Dispose it.
    //    Recommendation: use a static singleton or IHttpClientFactory (ASP.NET Core).
    private static readonly HttpClient SharedClient = new();

    public static ILogger Build()
    {
        return new LoggerBuilder()
            .SetLoggerName("MyApp")
            .AddHttpSink(
                httpClient:      SharedClient,
                endpoint:        "http://localhost:9200/logs",  // Receiver URL
                batchSize:       100,            // Send when 100 entries have accumulated
                flushInterval:   TimeSpan.FromSeconds(5),   // Force send after at most 5 seconds
                disposeTimeout:  TimeSpan.FromSeconds(5),   // Timeout to drain queue on Dispose
                channelCapacity: 1000,           // Internal Channel capacity; drops newest entries when full
                requestTimeout:  TimeSpan.FromSeconds(30),  // Per-request timeout
                filterConfig:    new FilterConfig { LogMinLevel = LogLevel.Info })
            .Build();
    }
}

// ================================================================
//  Section 2: AddSeqSink — Push to Seq (CLEF/NDJSON format)
//
//  Automatically uses ClefSerializer (Content-Type: application/vnd.serilog.clef).
//  The @mt field stores the raw template (not rendered); Seq renders and queries it server-side.
//  When apiKey is not null, the X-Seq-ApiKey header is automatically added.
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
                apiKey:       "your-seq-api-key",   // null = no auth header added
                batchSize:    100,
                flushInterval: TimeSpan.FromSeconds(5))
            .Build();
    }
}

// ================================================================
//  Section 3: AddLokiSink — Push to Grafana Loki (Push API v1)
//
//  Automatically uses LokiSerializer, producing Loki Push API format:
//  {"streams":[{"stream":{"app":"myservice"},"values":[["<ns>","<line>"]]}]}
//
//  ⚠️ Labels must be static values (service name, environment name, etc.).
//     Do NOT include dynamic content — this would cause Loki stream
//     cardinality explosion (each unique label combination creates a new stream).
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
                    // ⚠️ Do NOT put dynamic values here (user ID, request ID, etc.).
                    //    Dynamic data should go into the log message itself.
                },
                batchSize:    100,
                flushInterval: TimeSpan.FromSeconds(5))
            .Build();
    }
}

// ================================================================
//  Section 4: HttpSinkConfig — Object-Based Configuration
//
//  Suitable for reading from an external configuration source
//  (database, config center) and passing in, or for unified
//  registration via AddSinkByConfig() (consistent with other ISinkConfig implementations).
// ================================================================
public static class HttpSinkConfigExample
{
    public static ILogger Build()
    {
        var config = new HttpSinkConfig
        {
            // ── Required ──────────────────────────────────────────────────────
            Endpoint   = new Uri("http://localhost:5341/api/events/raw"),
            HttpClient = new HttpClient(),   // caller manages the lifetime

            // ── Serializer (optional, defaults to JsonArraySerializer.Default) ─
            // Serializer = ClefSerializer.Default,   // Seq
            // Serializer = new LokiSerializer(labels),  // Loki

            // ── Batching parameters ────────────────────────────────────────────
            BatchSize       = 100,                        // default: 100
            FlushInterval   = TimeSpan.FromSeconds(5),    // default: 5s
            DisposeTimeout  = TimeSpan.FromSeconds(5),    // default: 5s
            ChannelCapacity = 1000,                       // default: 1000
            RequestTimeout  = TimeSpan.FromSeconds(30),   // default: 30s

            // ── Custom headers ─────────────────────────────────────────────────
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer your-token",
            },

            // ── ISinkConfig common fields ──────────────────────────────────────
            Enabled      = true,
            FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Info },
        };

        return new LoggerBuilder()
            .SetLoggerName("MyApp")
            .AddSinkByConfig(config)   // equivalent to .AddHttpSink(config)
            .Build();
    }
}

// ================================================================
//  Section 5: Custom Serializer (DelegateHttpLogSerializer)
//
//  When the built-in JsonArray / CLEF / Loki formats don't meet your needs,
//  use DelegateHttpLogSerializer to wrap any serialization logic.
//
//  ⚠️ The serialization delegate must be synchronous (no await).
//     [ThreadStatic] resources are used internally and are unsafe across await points.
// ================================================================
public static class CustomSerializerExample
{
    public static ILogger Build()
    {
        // Custom serialization: convert a log batch to newline-delimited plain text
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
//  Section 6: HttpClient Management Recommendations
//
//  Principle: HttpTarget does NOT Dispose the provided HttpClient;
//  the caller owns its lifetime.
// ================================================================
public static class HttpClientManagementAdvice
{
    // ── Option 1: Static singleton (non-ASP.NET Core scenarios) ───────────────
    // HttpClient is thread-safe and can be shared across threads safely.
    // Create once for the application lifetime; released when the process exits.
    private static readonly HttpClient _client = new()
    {
        Timeout = Timeout.InfiniteTimeSpan,  // Timeout is controlled by the requestTimeout parameter
    };

    // ── Option 2: IHttpClientFactory (ASP.NET Core scenarios) ─────────────────
    // Inject IHttpClientFactory via DI to avoid socket exhaustion.
    //
    // services.AddHttpClient("lunarium-sink");
    //
    // Register the Logger in the DI container:
    // services.AddSingleton<ILogger>(sp =>
    // {
    //     var factory = sp.GetRequiredService<IHttpClientFactory>();
    //     var client  = factory.CreateClient("lunarium-sink");
    //     return new LoggerBuilder()
    //         .AddHttpSink(client, "http://localhost:9200/logs")
    //         .Build();
    // });

    // ── Notes ──────────────────────────────────────────────────────────────────
    // • Do NOT create a new HttpClient per request — this causes socket exhaustion.
    // • After HttpTarget.Dispose(), continuing to use the same HttpClient for other
    //   requests is safe (but the Target's Channel is closed, so no more log requests
    //   will be sent through it).
}

// ================================================================
//  Section 7: Tuning Recommendations
//
//  BatchSize and FlushInterval are complementary dual-trigger mechanisms:
//    • BatchSize: throughput guarantee — sends promptly under high log volume
//                 to prevent memory pressure
//    • FlushInterval: latency guarantee — ensures timely delivery even under
//                     low log volume
//
//  ChannelCapacity and overflow behavior:
//    • When full, uses DropWrite (discards the newest entry) — caller is never blocked
//    • On the first overflow, InternalLogger prints a warning; resets after a successful
//      flush; warns again on the next overflow
//    • Set capacity based on estimated peak log rate and network latency;
//      1000–5000 is a reasonable starting range
//
//  RequestTimeout recommendations:
//    • Set to 2–3× FlushInterval to ensure a single request doesn't block the
//      entire flush loop
//    • The default of 30s is appropriate for most scenarios;
//      for intranet pushes, 5–10s may be more suitable
// ================================================================
