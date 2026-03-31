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

// 实现 IHttpLogSerializer 时凿守关键约束：
// 1. Serialize() 结果应当可多次调用（重试时不能佟用直接还口 HttpContent）
// 2. Serialize() 必须是同步的，不得包含 await，否则 [ThreadStatic] 内部资源在续体中会轮转到不同线程
/// <summary>
/// Defines the contract for an HTTP log serializer that converts a batch of
/// <see cref="LogEntry"/> objects into an <see cref="HttpContent"/> payload.
/// </summary>
/// <remarks>
/// Implementations must be stateless or thread-safe via <c>[ThreadStatic]</c> fields.
/// <para>
/// ⚠️ <see cref="Serialize"/> must stay synchronous (no <c>await</c>),
/// because built-in serializers use <c>[ThreadStatic]</c> resources that would be
/// corrupted if the continuation runs on a different thread.
/// </para>
/// </remarks>
public interface IHttpLogSerializer
{
    /// <summary>The HTTP <c>Content-Type</c> value produced by this serializer.</summary>
    string ContentType { get; }

    /// <summary>
    /// Serializes a non-empty batch of log entries into an <see cref="HttpContent"/> payload.
    /// The returned content must be independently readable (all bytes already materialized).
    /// </summary>
    /// <param name="entries">Non-empty, read-only list of log entries to serialize.</param>
    /// <returns>An <see cref="HttpContent"/> ready to be sent in an HTTP POST request.</returns>
    HttpContent Serialize(IReadOnlyList<LogEntry> entries);
}
