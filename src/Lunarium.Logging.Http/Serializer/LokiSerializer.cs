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

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Lunarium.Logging.Internal;

namespace Lunarium.Logging.Http.Serializer;

// Loki 要求 stream labels 必须为静态值，动态 labels 会导致 cardinality 爆炸，剥天 Loki 性能
// 内部使用两层 ThreadStatic 资源：外层写 Loki payload，内层写单条 log line JSON
/// <summary>
/// Serializes a batch of log entries into the Loki Push API v1 format.
/// </summary>
/// <remarks>
/// <para>Payload structure: <c>{"streams":[{"stream":{labels},"values":[["&lt;ns&gt;","&lt;json&gt;"],...]}]}</c></para>
/// <para>Timestamps are Unix nanosecond strings. Log lines are JSON objects
/// (same schema as <see cref="JsonArraySerializer"/>), compatible with Grafana's
/// <c>| json</c> LogQL pipeline.</para>
/// <para>⚠️ Stream labels must be static. Dynamic label values will cause Loki cardinality explosion.</para>
/// </remarks>
public sealed class LokiSerializer : IHttpLogSerializer
{
    private readonly IReadOnlyDictionary<string, string> _labels;

    /// <summary>Initializes the serializer with the given static Loki stream labels.</summary>
    /// <param name="labels">
    /// Loki stream labels, e.g. <c>{"app":"order-service","env":"prod"}</c>.
    /// Values must be static — dynamic content causes cardinality explosion.
    /// </param>
    public LokiSerializer(IReadOnlyDictionary<string, string> labels)
    {
        _labels = labels;
    }

    // public LokiSerializer(Dictionary<string, string> labels)
    //     : this((IReadOnlyDictionary<string, string>)labels)
    // { }

    /// <inheritdoc/>
    public string ContentType => "application/json";

    // ===== ThreadStatic 批级资源（外层 Loki JSON 结构）=====
    // ⚠️ Serialize() 必须保持同步（无 await），否则 ThreadStatic 状态在 await 点后可能跑在不同线程上。

    private static readonly JsonWriterOptions _writerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        SkipValidation = false
    };

    [ThreadStatic] private static BufferWriter? _batchBuffer;
    [ThreadStatic] private static Utf8JsonWriter? _batchWriter;

    // ===== ThreadStatic 行级资源（每条 log line JSON 渲染）=====

    [ThreadStatic] private static BufferWriter? _lineBuffer;
    [ThreadStatic] private static Utf8JsonWriter? _lineWriter;

    // char[] 用于 UTF-8 bytes → ReadOnlySpan<char> 解码，避免 string 堆分配
    [ThreadStatic] private static char[]? _lineDecodeChars;

    private static (BufferWriter buf, Utf8JsonWriter w) GetBatchResources()
    {
        _batchBuffer ??= new BufferWriter(4096);
        _batchWriter ??= new Utf8JsonWriter(Stream.Null, _writerOptions);
        _batchBuffer.Reset();
        _batchWriter.Reset(_batchBuffer);
        return (_batchBuffer, _batchWriter);
    }

    private static (BufferWriter buf, Utf8JsonWriter w) GetLineResources()
    {
        _lineBuffer ??= new BufferWriter(512);
        _lineWriter ??= new Utf8JsonWriter(Stream.Null, _writerOptions);
        _lineBuffer.Reset();
        _lineWriter.Reset(_lineBuffer);
        return (_lineBuffer, _lineWriter);
    }

    private static char[] GetLineDecodeChars(int minSize)
    {
        if (_lineDecodeChars is null || _lineDecodeChars.Length < minSize)
            _lineDecodeChars = new char[Math.Max(minSize * 2, 512)];
        return _lineDecodeChars;
    }

    // ===== IHttpLogSerializer =====

    /// <inheritdoc/>
    public HttpContent Serialize(IReadOnlyList<LogEntry> entries)
    {
        var (buffer, writer) = GetBatchResources();

        writer.WriteStartObject();
        writer.WriteStartArray("streams"u8);

        writer.WriteStartObject();

        // stream labels
        writer.WriteStartObject("stream"u8);
        foreach (var (k, v) in _labels)
            writer.WriteString(k, v);
        writer.WriteEndObject();

        // values: 每条日志是 ["<unix_ns>", "<json_line>"]
        writer.WriteStartArray("values"u8);
        foreach (var entry in entries)
        {
            writer.WriteStartArray();
            WriteUnixNanoseconds(writer, entry.Timestamp);
            WriteJsonLine(writer, entry);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();

        writer.WriteEndObject(); // stream object
        writer.WriteEndArray();  // streams
        writer.WriteEndObject(); // root
        writer.Flush();

        return new ByteArrayContent(buffer.WrittenSpan.ToArray())
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ContentType) { CharSet = "utf-8" } }
        };
    }

    // ===== 私有辅助 =====

    // 将时间戳转换为 Unix 纳秒并直接写入 writer，零 string 分配
    private static void WriteUnixNanoseconds(Utf8JsonWriter writer, DateTimeOffset timestamp)
    {
        long ms = timestamp.ToUnixTimeMilliseconds();
        long nsFromTicks = (timestamp.Ticks % TimeSpan.TicksPerMillisecond) * 100L;
        long ns = ms * 1_000_000L + nsFromTicks;

        // stackalloc：long 最大 20 位，20 字节足够
        Span<byte> nsBytes = stackalloc byte[20];
        ns.TryFormat(nsBytes, out int written, default, CultureInfo.InvariantCulture);
        writer.WriteStringValue(nsBytes[..written]);
    }

    // 将单条 LogEntry 渲染为 JSON 字节，解码为 char span 后写入 writer，
    // 替代原来 Encoding.UTF8.GetString() 的 string 堆分配
    private static void WriteJsonLine(Utf8JsonWriter batchWriter, LogEntry entry)
    {
        // 1. 渲染 log line JSON 到行级 scratch buffer
        var (lineBuffer, lineWriter) = GetLineResources();
        JsonArraySerializer.WriteEntry(lineWriter, entry);
        lineWriter.Flush();

        // 2. UTF-8 bytes → char[]（自增长，零 string 分配）
        ReadOnlySpan<byte> lineBytes = lineBuffer.WrittenSpan;
        int charCount = Encoding.UTF8.GetCharCount(lineBytes);
        char[] chars = GetLineDecodeChars(charCount);
        Encoding.UTF8.GetChars(lineBytes, chars);

        // 3. 直接以 ReadOnlySpan<char> 写入，WriteStringValue 负责 JSON 转义
        batchWriter.WriteStringValue(chars.AsSpan(0, charCount));
    }
}
