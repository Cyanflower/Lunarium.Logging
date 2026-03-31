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

using Lunarium.Logging.Http.Target;

namespace Lunarium.Logging.Http;

/// <summary>
/// Provides extension methods for <see cref="LoggerBuilder"/> to add HTTP log targets.
/// </summary>
public static class HttpLoggerBuilderExtensions
{
    /// <summary>
    /// Adds an HTTP sink to the logger builder.
    /// Log entries will be batched, serialized, and sent to the specified HTTP endpoint in the background.
    /// </summary>
    /// <param name="builder">The logger builder to configure.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to send requests; its lifecycle must be managed externally.</param>
    /// <param name="endpoint">The log ingestion endpoint URL. Must use the <c>http</c> or <c>https</c> scheme.</param>
    /// <param name="serializer">The log serializer. Defaults to <see cref="JsonArraySerializer.Default"/> if null.</param>
    /// <param name="headers">Custom HTTP headers (e.g., authorization headers) to attach to each request.</param>
    /// <param name="batchSize">The maximum number of entries to batch before triggering a send. Defaults to 100.</param>
    /// <param name="flushInterval">The time to wait before forcing a batch flush. Defaults to 5 seconds.</param>
    /// <param name="disposeTimeout">The maximum time to wait for the background queue to drain when disposed. Defaults to 5 seconds.</param>
    /// <param name="channelCapacity">The capacity of the internal channel. When exceeded, new entries are dropped. Defaults to 1000.</param>
    /// <param name="requestTimeout">The timeout for a single HTTP request. Defaults to 30 seconds.</param>
    /// <param name="sinkName">The sink name. Used for dynamic runtime configuration; can be null.</param>
    /// <param name="filterConfig">Optional filter configuration.</param>
    /// <returns>The configured logger builder for chaining.</returns>
    public static LoggerBuilder AddHttpSink(
        this LoggerBuilder builder,
        HttpClient httpClient,
        string endpoint,
        IHttpLogSerializer? serializer = null,
        IReadOnlyDictionary<string, string>? headers = null,
        int batchSize = 100,
        TimeSpan? flushInterval = null,
        TimeSpan? disposeTimeout = null,
        int channelCapacity = 1000,
        TimeSpan? requestTimeout = null,
        string? sinkName = null,
        FilterConfig? filterConfig = null)
    {
        return builder.AddSink(
            target: new HttpTarget(
                httpClient: httpClient,
                endpoint: new Uri(endpoint),
                serializer: serializer,
                headers: headers,
                batchSize: batchSize,
                flushInterval: flushInterval,
                disposeTimeout: disposeTimeout,
                channelCapacity: channelCapacity,
                requestTimeout: requestTimeout),
            cfg: filterConfig,
            name: sinkName);
    }

    /// <summary>
    /// Adds an HTTP sink to the logger builder using a configuration object.
    /// </summary>
    /// <param name="builder">The logger builder to configure.</param>
    /// <param name="config">The HTTP sink configuration object.</param>
    /// <returns>The configured logger builder for chaining.</returns>
    public static LoggerBuilder AddHttpSink(
        this LoggerBuilder builder,
        HttpSinkConfig config)
    {
        return builder.AddSink(config);
    }

    /// <summary>
    /// Convenience method to add an HTTP sink targeting Seq (using CLEF format).
    /// Automatically uses <see cref="ClefSerializer"/> and attaches the <paramref name="apiKey"/> as an <c>X-Seq-ApiKey</c> header.
    /// </summary>
    /// <param name="builder">The logger builder to configure.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to send requests; its lifecycle must be managed externally.</param>
    /// <param name="seqEndpoint">The Seq ingestion endpoint URL (e.g., <c>http://localhost:5341/api/events/raw</c>).</param>
    /// <param name="apiKey">The Seq API key. If null, no authentication header is attached.</param>
    /// <param name="batchSize">The maximum number of entries to batch before triggering a send. Defaults to 100.</param>
    /// <param name="flushInterval">The time to wait before forcing a batch flush. Defaults to 5 seconds.</param>
    /// <param name="disposeTimeout">The maximum time to wait for the background queue to drain when disposed. Defaults to 5 seconds.</param>
    /// <param name="channelCapacity">The capacity of the internal channel. When exceeded, new entries are dropped. Defaults to 1000.</param>
    /// <param name="requestTimeout">The timeout for a single HTTP request. Defaults to 30 seconds.</param>
    /// <param name="sinkName">The sink name. Used for dynamic runtime configuration; can be null.</param>
    /// <param name="filterConfig">Optional filter configuration.</param>
    /// <returns>The configured logger builder for chaining.</returns>
    public static LoggerBuilder AddSeqSink(
        this LoggerBuilder builder,
        HttpClient httpClient,
        string seqEndpoint,
        string? apiKey = null,
        int batchSize = 100,
        TimeSpan? flushInterval = null,
        TimeSpan? disposeTimeout = null,
        int channelCapacity = 1000,
        TimeSpan? requestTimeout = null,
        string? sinkName = null,
        FilterConfig? filterConfig = null)
    {
        IReadOnlyDictionary<string, string>? headers = null;
        if (!string.IsNullOrEmpty(apiKey))
            headers = new Dictionary<string, string> { ["X-Seq-ApiKey"] = apiKey };

        return builder.AddSink(
            target: new HttpTarget(
                httpClient: httpClient,
                endpoint: new Uri(seqEndpoint),
                serializer: ClefSerializer.Default,
                headers: headers,
                batchSize: batchSize,
                flushInterval: flushInterval,
                disposeTimeout: disposeTimeout,
                channelCapacity: channelCapacity,
                requestTimeout: requestTimeout),
            cfg: filterConfig,
            name: sinkName);
    }

    /// <summary>
    /// Convenience method to add an HTTP sink targeting Grafana Loki.
    /// Automatically uses <see cref="LokiSerializer"/> and passes the given <paramref name="labels"/> as Loki stream labels.
    /// <para>⚠️ <paramref name="labels"/> must be static. Dynamic content will cause a Loki cardinality explosion.</para>
    /// </summary>
    /// <param name="builder">The logger builder to configure.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to send requests; its lifecycle must be managed externally.</param>
    /// <param name="lokiEndpoint">The Loki Push API endpoint URL (e.g., <c>http://localhost:3100/loki/api/v1/push</c>).</param>
    /// <param name="labels">Loki stream labels, e.g. <c>{"app":"order-service","env":"prod"}</c>. Uses empty labels if null.</param>
    /// <param name="batchSize">The maximum number of entries to batch before triggering a send. Defaults to 100.</param>
    /// <param name="flushInterval">The time to wait before forcing a batch flush. Defaults to 5 seconds.</param>
    /// <param name="disposeTimeout">The maximum time to wait for the background queue to drain when disposed. Defaults to 5 seconds.</param>
    /// <param name="channelCapacity">The capacity of the internal channel. When exceeded, new entries are dropped. Defaults to 1000.</param>
    /// <param name="requestTimeout">The timeout for a single HTTP request. Defaults to 30 seconds.</param>
    /// <param name="sinkName">The sink name. Used for dynamic runtime configuration; can be null.</param>
    /// <param name="filterConfig">Optional filter configuration.</param>
    /// <returns>The configured logger builder for chaining.</returns>
    public static LoggerBuilder AddLokiSink(
        this LoggerBuilder builder,
        HttpClient httpClient,
        string lokiEndpoint,
        IReadOnlyDictionary<string, string>? labels = null,
        int batchSize = 100,
        TimeSpan? flushInterval = null,
        TimeSpan? disposeTimeout = null,
        int channelCapacity = 1000,
        TimeSpan? requestTimeout = null,
        string? sinkName = null,
        FilterConfig? filterConfig = null)
    {
        var serializer = new LokiSerializer(
            labels ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>());

        return builder.AddSink(
            target: new HttpTarget(
                httpClient: httpClient,
                endpoint: new Uri(lokiEndpoint),
                serializer: serializer,
                batchSize: batchSize,
                flushInterval: flushInterval,
                disposeTimeout: disposeTimeout,
                channelCapacity: channelCapacity,
                requestTimeout: requestTimeout),
            cfg: filterConfig,
            name: sinkName);
    }
}
