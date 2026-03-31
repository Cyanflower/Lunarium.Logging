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

using System.Buffers;

namespace Lunarium.Logging;

// IDestructurable 和 IDestructured 是 {@Object} 解构的高性能扩展点，优先级高于 JsonSerializer 回退路径
// 优先调用顺序：IDestructurable → IDestructured → JsonSerializer.Serialize()

/// <summary>
/// Marks an object as supporting high-performance structured destructuring via <c>{@Property}</c> syntax.
/// Implement <see cref="Destructure"/> to write JSON directly into the log writer's shared buffer
/// using the provided <see cref="DestructureHelper"/>, avoiding intermediate string and object
/// allocations.
/// </summary>
public interface IDestructurable
{
    /// <summary>
    /// Writes this object's structured representation into the log output buffer.
    /// The implementation must write a complete, valid JSON value (object, array, or scalar).
    /// </summary>
    /// <param name="helper">Helper that wraps the writer's <c>Utf8JsonWriter</c> and target buffer.</param>
    void Destructure(DestructureHelper helper);

}

// IDestructured 最适合返回静态常量、缓存字节块或预先序列化的不可变对象
/// <summary>
/// Marks an object as providing a pre-serialized UTF-8 JSON byte representation.
/// The returned bytes are embedded directly into the log output via <c>WriteRawValue</c>,
/// achieving zero allocation and zero encoding overhead.
/// </summary>
public interface IDestructured
{
    /// <summary>
    /// Returns the pre-serialized UTF-8 JSON bytes for this object.
    /// The caller treats the bytes as a raw JSON value and writes them verbatim.
    /// </summary>
    /// <returns>A <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;byte&gt;</see> containing valid UTF-8 JSON.</returns>
    ReadOnlyMemory<byte> Destructured();

}
