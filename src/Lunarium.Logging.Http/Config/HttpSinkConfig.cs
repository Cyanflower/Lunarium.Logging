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

// ToJson / IsColor / TextOutput 对 HTTP 输出无意义，仅用于满足 ISinkConfig 接口约定
using Lunarium.Logging.Http.Target;
using Lunarium.Logging.Target;

namespace Lunarium.Logging.Http.Config;

/// <summary>
/// Configuration for the HTTP sink. Implements <see cref="ISinkConfig"/> to support 
/// unified registration via <c>AddSinkByConfig</c>.
/// </summary>
public sealed record HttpSinkConfig : ISinkConfig
{
    // ===== ISinkConfig 通用字段 =====

    /// <inheritdoc/>
    public bool Enabled { get; init; } = true;

    /// <inheritdoc/>
    /// <remarks>
    /// ⚠️ Ineffective for HTTP sink. Setting this property does not change the serialization format.
    /// The output format is determined strictly by the <see cref="Serializer"/>.
    /// </remarks>
    public bool? ToJson { get; init; } = null;

    /// <inheritdoc/>
    /// <remarks>⚠️ Ineffective for HTTP sink. HTTP output does not support ANSI colors.</remarks>
    public bool? IsColor { get; init; } = null;

    /// <inheritdoc/>
    /// <remarks>⚠️ Ineffective for HTTP sink. Text field visibility control does not apply to HTTP serializers.</remarks>
    public TextOutputIncludeConfig? TextOutput { get; init; } = null;

    /// <inheritdoc/>
    public FilterConfig? FilterConfig { get; init; }

    // ===== HTTP 专属配置 =====

    // 必须使用 http 或 https scheme
    /// <summary>The HTTP endpoint URI where logs will be sent (e.g., <c>http://localhost:5341/api/events/raw</c>).</summary>
    public required Uri Endpoint { get; init; }

    // HttpTarget 内部不会 Dispose 此 HttpClient
    /// <summary>
    /// The <see cref="System.Net.Http.HttpClient"/> used to send requests.
    /// <para>⚠️ The lifecycle of this client must be managed externally; the HTTP target will not dispose it.</para>
    /// </summary>
    public required HttpClient HttpClient { get; init; }

    /// <summary>The log serializer. Defaults to <see cref="JsonArraySerializer.Default"/>.</summary>
    public IHttpLogSerializer? Serializer { get; init; }

    /// <summary>Optional custom HTTP headers (such as authorization headers) to attach to each request.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>The maximum number of log entries to batch before triggering a send. Defaults to 100.</summary>
    public int BatchSize { get; init; } = 100;

    /// <summary>The maximum time interval to wait before flushing the batch. Defaults to 5 seconds.</summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>The maximum time to wait for the internal queue to drain on disposal. Defaults to 5 seconds.</summary>
    public TimeSpan DisposeTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>The capacity of the internal bounded channel. When full, new entries are dropped. Defaults to 1000.</summary>
    public int ChannelCapacity { get; init; } = 1000;

    /// <summary>The timeout for a single HTTP request. Defaults to 30 seconds.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public ILogTarget CreateTarget() =>
        new HttpTarget(
            httpClient: HttpClient,
            endpoint: Endpoint,
            serializer: Serializer,
            headers: Headers,
            batchSize: BatchSize,
            flushInterval: FlushInterval,
            disposeTimeout: DisposeTimeout,
            channelCapacity: ChannelCapacity,
            requestTimeout: RequestTimeout);
}
