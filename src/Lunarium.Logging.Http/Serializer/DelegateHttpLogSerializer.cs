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

namespace Lunarium.Logging.Http.Serializer;

// 适合 Elasticsearch、Datadog、Splunk 等需要自定义 Payload 格式的场景
// _serialize 委托由调用方负责内容序列化逻辑，本类不做任何假设
/// <summary>
/// A delegate-based <see cref="IHttpLogSerializer"/> that delegates serialization
/// to a caller-supplied <see cref="Func{T,TResult}"/>.
/// Use this to integrate with Elasticsearch, Datadog, Splunk, or any endpoint
/// that requires a custom payload format.
/// </summary>
public sealed class DelegateHttpLogSerializer : IHttpLogSerializer
{
    private readonly Func<IReadOnlyList<LogEntry>, HttpContent> _serialize;

    /// <inheritdoc/>
    public string ContentType { get; }

    /// <summary>Initializes the serializer with a custom serialization delegate.</summary>
    /// <param name="serialize">Delegate that converts a batch of log entries to <see cref="HttpContent"/>.</param>
    /// <param name="contentType">The <c>Content-Type</c> header value; defaults to <c>application/json</c>.</param>
    public DelegateHttpLogSerializer(
        Func<IReadOnlyList<LogEntry>, HttpContent> serialize,
        string contentType = "application/json")
    {
        _serialize = serialize;
        ContentType = contentType;
    }

    /// <inheritdoc/>
    public HttpContent Serialize(IReadOnlyList<LogEntry> entries) => _serialize(entries);
}
