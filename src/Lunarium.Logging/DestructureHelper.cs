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
using System.Text.Json;
using Lunarium.Logging.Internal;

namespace Lunarium.Logging;

// DestructureHelper 在 LogWriter.RenderPropertyToken {@...} 路径中被创建并传入 IDestructurable.Destructure()
// 内部共享 LogWriter 的 _bufferWriter 或 _scratchWriter（取决于调用方），避免额外分配
// 用完后必须调用 Dispose()，通知帮助类释放对缓冲区的引用，防止悬空访问
/// <summary>
/// Helper passed to <see cref="IDestructurable.Destructure"/> implementations.
/// Wraps a <c>Utf8JsonWriter</c> bound to the log writer's output buffer, allowing
/// <see cref="IDestructurable"/> objects to write structured JSON directly without
/// any intermediate string or byte-array allocations.
/// </summary>
public class DestructureHelper
{
    private BufferWriter _bufferWriter;

    private Utf8JsonWriter _serializerWriter;

    // bufferWriterIsMainWriter=false 时重置屟业缓冲区（scratch writer）；
    // jsonWriterIsMainWriter=false 时重置并重新绑定到指定 bufferWriter
    internal DestructureHelper(
        BufferWriter bufferWriter,
        bool bufferWriterIsMainWriter,
        Utf8JsonWriter jsonWriter,
        bool jsonWriterIsMainWriter)
    {
        _bufferWriter = bufferWriter;
        _serializerWriter = jsonWriter;
        if (!bufferWriterIsMainWriter)
        {
            _bufferWriter.Reset();
        }
        if (!jsonWriterIsMainWriter)
        {
            _serializerWriter.Reset(bufferWriter);
        }
    }

    /// <summary>Begins a JSON object.</summary>
    public void WriteStartObject()
    {
        _serializerWriter.WriteStartObject();
    }

    /// <summary>Ends the current JSON object.</summary>
    public void WriteEndObject()
    {
        _serializerWriter.WriteEndObject();
    }

    /// <summary>Begins a JSON array.</summary>
    public void WriteStartArray()
    {
        _serializerWriter.WriteStartArray();
    }

    /// <summary>Ends the current JSON array.</summary>
    public void WriteEndArray()
    {
        _serializerWriter.WriteEndArray();
    }

    /// <summary>Writes a property name.</summary>
    /// <param name="name">The property name.</param>
    public void WritePropertyName(string name)
    {
        _serializerWriter.WritePropertyName(name);
    }

    /// <summary>Writes a property name from a <see cref="ReadOnlySpan{T}"/> of chars.</summary>
    /// <param name="name">The property name.</param>
    public void WritePropertyName(ReadOnlySpan<char> name)
    {
        _serializerWriter.WritePropertyName(name);
    }

    /// <summary>Writes a property name from a pre-encoded <see cref="JsonEncodedText"/>.</summary>
    /// <param name="name">The pre-encoded property name.</param>
    public void WritePropertyName(JsonEncodedText name)
    {
        _serializerWriter.WritePropertyName(name);
    }

    #region Write Value Methods

    /// <summary>Writes a string as the current value.</summary>
    /// <param name="value">The string value.</param>
    public void WriteStringValue(string value)
    {
        _serializerWriter.WriteStringValue(value);
    }

    /// <summary>Writes a <see cref="ReadOnlySpan{T}"/> of chars as the current string value.</summary>
    /// <param name="value">The character span to write.</param>
    public void WriteStringValue(ReadOnlySpan<char> value)
    {
        _serializerWriter.WriteStringValue(value);
    }

    /// <summary>Writes a pre-encoded <see cref="JsonEncodedText"/> as the current string value.</summary>
    /// <param name="value">The pre-encoded value.</param>
    public void WriteStringValue(JsonEncodedText value)
    {
        _serializerWriter.WriteStringValue(value);
    }

    /// <summary>Writes a <see cref="DateTime"/> as an ISO 8601 string value.</summary>
    /// <param name="value">The date/time value.</param>
    public void WriteStringValue(DateTime value)
    {
        _serializerWriter.WriteStringValue(value);
    }

    /// <summary>Writes a <see cref="DateTimeOffset"/> as an ISO 8601 string value.</summary>
    /// <param name="value">The date/time offset value.</param>
    public void WriteStringValue(DateTimeOffset value)
    {
        _serializerWriter.WriteStringValue(value);
    }

    /// <summary>Writes a <see cref="Guid"/> as a string value.</summary>
    /// <param name="value">The GUID value.</param>
    public void WriteStringValue(Guid value)
    {
        _serializerWriter.WriteStringValue(value);
    }

    /// <summary>Writes an <see cref="int"/> as the current numeric value.</summary>
    /// <param name="value">The integer value.</param>
    public void WriteNumberValue(int value)
    {
        _serializerWriter.WriteNumberValue(value);
    }

    /// <summary>Writes a <see cref="long"/> as the current numeric value.</summary>
    /// <param name="value">The long value.</param>
    public void WriteNumberValue(long value)
    {
        _serializerWriter.WriteNumberValue(value);
    }

    /// <summary>Writes a <see cref="uint"/> as the current numeric value.</summary>
    /// <param name="value">The unsigned integer value.</param>
    public void WriteNumberValue(uint value)
    {
        _serializerWriter.WriteNumberValue(value);
    }

    /// <summary>Writes a <see cref="ulong"/> as the current numeric value.</summary>
    /// <param name="value">The unsigned long value.</param>
    public void WriteNumberValue(ulong value)
    {
        _serializerWriter.WriteNumberValue(value);
    }

    /// <summary>Writes a <see cref="float"/> as the current numeric value.</summary>
    /// <param name="value">The float value.</param>
    public void WriteNumberValue(float value)
    {
        _serializerWriter.WriteNumberValue(value);
    }

    /// <summary>Writes a <see cref="double"/> as the current numeric value.</summary>
    /// <param name="value">The double value.</param>
    public void WriteNumberValue(double value)
    {
        _serializerWriter.WriteNumberValue(value);
    }

    /// <summary>Writes a <see cref="decimal"/> as the current numeric value.</summary>
    /// <param name="value">The decimal value.</param>
    public void WriteNumberValue(decimal value)
    {
        _serializerWriter.WriteNumberValue(value);
    }

    /// <summary>Writes a <see cref="bool"/> as the current value (<c>true</c> or <c>false</c>).</summary>
    /// <param name="value">The boolean value.</param>
    public void WriteBooleanValue(bool value)
    {
        _serializerWriter.WriteBooleanValue(value);
    }

    /// <summary>Writes a JSON <see langword="null"/> as the current value.</summary>
    public void WriteNullValue()
    {
        _serializerWriter.WriteNullValue();
    }

    #endregion

    #region Write Field Methods (Property Name + Value)

    /// <summary>Writes a string property (name and value) in a single call.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The string value.</param>
    public void WriteString(string name, string value)
    {
        _serializerWriter.WriteString(name, value);
    }

    /// <summary>Writes a string property using a pre-encoded name.</summary>
    /// <param name="name">The pre-encoded property name.</param>
    /// <param name="value">The string value.</param>
    public void WriteString(JsonEncodedText name, string value)
    {
        _serializerWriter.WriteString(name, value);
    }

    /// <summary>Writes a <see cref="DateTime"/> property as an ISO 8601 string.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The date/time value.</param>
    public void WriteString(string name, DateTime value)
    {
        _serializerWriter.WriteString(name, value);
    }

    /// <summary>Writes a <see cref="DateTimeOffset"/> property as an ISO 8601 string.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The date/time offset value.</param>
    public void WriteString(string name, DateTimeOffset value)
    {
        _serializerWriter.WriteString(name, value);
    }

    /// <summary>Writes a <see cref="Guid"/> property as a string.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The GUID value.</param>
    public void WriteString(string name, Guid value)
    {
        _serializerWriter.WriteString(name, value);
    }

    /// <summary>Writes an <see cref="int"/> property.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The integer value.</param>
    public void WriteNumber(string name, int value)
    {
        _serializerWriter.WriteNumber(name, value);
    }

    /// <summary>Writes a <see cref="long"/> property.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The long value.</param>
    public void WriteNumber(string name, long value)
    {
        _serializerWriter.WriteNumber(name, value);
    }

    /// <summary>Writes a <see cref="double"/> property.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The double value.</param>
    public void WriteNumber(string name, double value)
    {
        _serializerWriter.WriteNumber(name, value);
    }

    /// <summary>Writes a <see cref="bool"/> property.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The boolean value.</param>
    public void WriteBoolean(string name, bool value)
    {
        _serializerWriter.WriteBoolean(name, value);
    }

    /// <summary>Writes a property with a JSON <see langword="null"/> value.</summary>
    /// <param name="name">The property name.</param>
    public void WriteNull(string name)
    {
        _serializerWriter.WriteNull(name);
    }

    #endregion

    /// <summary>Writes a pre-encoded UTF-8 JSON fragment verbatim into the output.</summary>
    /// <param name="utf8Json">Valid UTF-8 JSON bytes to embed without additional encoding.</param>
    public void WriteRawValue(ReadOnlySpan<byte> utf8Json)
    {
        _serializerWriter.WriteRawValue(utf8Json);
    }

    internal ReadOnlySpan<byte> WrittenSpan => _bufferWriter.WrittenSpan;

    // TryFlush 成功后由调用方负责将 WrittenSpan 的内容追加到主写入缓冲区
    // CurrentDepth != 0 表示 JSON 对象/数组未正常关闭，属于 IDestructurable 实现错误
    internal bool TryFlush()
    {
        try
        {
            if (_serializerWriter.CurrentDepth == 0)
            {
                _serializerWriter.Flush();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            InternalLogger.Error(ex);
            return false;
        }
    }

    // Dispose 后 _bufferWriter / _serializerWriter 置 null 防止调用方保留引用进行意外访问
    internal void Dispose(bool resetBufferWriter, bool resetJsonWriter)
    {
        if (resetBufferWriter)
        {
            _bufferWriter.Reset();
        }
        if (resetJsonWriter)
        {
            _serializerWriter.Reset(Stream.Null);
        }
        _bufferWriter = null!;
        _serializerWriter = null!;
    }
}