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
using System.Text.Encodings.Web;
using System.Text.Json;
using Lunarium.Logging.Internal;
using Lunarium.Logging.Parser;

namespace Lunarium.Logging.Http.Serializer;

// CLEF (Compact Log Event Format) 是 Seq 日志管理平台的原生格式
// 局设计：每条进入志 body 使用独立 Utf8JsonWriter，直接 Reset 到同一 buffer 继续写入，无中间字符串
/// <summary>
/// Serializes a batch of log entries into CLEF (Compact Log Event Format) for Seq.
/// Each entry is a single-line JSON object separated by <c>\n</c>.
/// </summary>
/// <remarks>
/// <para>Field convention: <c>@t</c> (timestamp), <c>@l</c> (level; omitted for Info),
/// <c>@mt</c> (message template), <c>@x</c> (exception; omitted if null),
/// plus top-level properties per CLEF spec.</para>
/// <para>Content-Type: <c>application/vnd.serilog.clef</c></para>
/// </remarks>
public sealed class ClefSerializer : IHttpLogSerializer
{
    /// <summary>Shared singleton — stateless, safe to share across threads.</summary>
    public static readonly ClefSerializer Default = new();

    /// <inheritdoc/>
    public string ContentType => "application/vnd.serilog.clef";

    // ===== ThreadStatic 批级资源 =====
    // ⚠️ Serialize() 必须保持同步（无 await），否则 ThreadStatic 状态在 await 点后可能跑在不同线程上。

    private static readonly JsonWriterOptions _writerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        SkipValidation = false
    };

    [ThreadStatic] private static BufferWriter? _batchBuffer;
    [ThreadStatic] private static Utf8JsonWriter? _batchWriter;

    private static (BufferWriter buf, Utf8JsonWriter w) GetBatchResources()
    {
        _batchBuffer ??= new BufferWriter(4096);
        _batchWriter ??= new Utf8JsonWriter(Stream.Null, _writerOptions);
        _batchBuffer.Reset();
        // 首次绑定；循环内每条通过 writer.Reset(buffer) 继续写同一 buffer
        _batchWriter.Reset(_batchBuffer);
        return (_batchBuffer, _batchWriter);
    }

    // ===== IHttpLogSerializer =====

    /// <inheritdoc/>
    public HttpContent Serialize(IReadOnlyList<LogEntry> entries)
    {
        var (buffer, writer) = GetBatchResources();

        // CLEF：每条日志一行 JSON，行间用 \n 分隔
        // writer 在循环内通过 Reset(buffer) 复用，不再 per-entry new
        bool first = true;
        foreach (var entry in entries)
        {
            if (!first)
                buffer.Append("\n"u8);
            first = false;

            writer.Reset(buffer);       // 重置 writer 内部状态，继续写入同一 buffer
            WriteEntry(writer, entry);
            writer.Flush();
        }

        return new ByteArrayContent(buffer.WrittenSpan.ToArray())
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ContentType) { CharSet = "utf-8" } }
        };
    }

    // ===== 私有辅助 =====

    private static void WriteEntry(Utf8JsonWriter writer, LogEntry entry)
    {
        writer.WriteStartObject();

        // @t - 时间戳（ISO 8601）
        writer.WriteString("@t"u8, entry.Timestamp);

        // @l - 日志级别（Info 省略，符合 CLEF 规范）
        if (entry.LogLevel != LogLevel.Info)
            writer.WriteString("@l"u8, MapLevel(entry.LogLevel));

        // @mt - 原始消息模板（CLEF 不写渲染后消息，下游自行渲染）
        writer.WriteString("@mt"u8, entry.Message);

        // @x - 异常
        if (entry.Exception is not null)
            writer.WriteString("@x"u8, entry.Exception.ToString());

        // 附加结构化字段
        if (!string.IsNullOrEmpty(entry.LoggerName))
            writer.WriteString("LoggerName"u8, entry.LoggerName);

        if (!string.IsNullOrEmpty(entry.Context))
            writer.WriteString("Context"u8, entry.Context);

        if (!string.IsNullOrEmpty(entry.Scope))
            writer.WriteString("Scope"u8, entry.Scope);

        // Properties 平铺到顶级（CLEF 约定）
        int propIndex = 0;
        foreach (var token in entry.MessageTemplate.MessageTemplateTokens)
        {
            if (token is not PropertyToken pt) continue;
            if (propIndex < entry.Properties.Length)
                JsonArraySerializer.WritePropertyValue(writer, pt, entry.Properties[propIndex]);
            propIndex++;
        }

        writer.WriteEndObject();
    }

    private static string MapLevel(LogLevel level) => level switch
    {
        LogLevel.Debug    => "Debug",
        LogLevel.Warning  => "Warning",
        LogLevel.Error    => "Error",
        LogLevel.Critical => "Fatal",
        _                 => level.ToString()
    };
}
