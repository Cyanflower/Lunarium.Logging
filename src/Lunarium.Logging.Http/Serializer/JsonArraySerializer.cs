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

// JsonArraySerializer 是无状态的单例，可安全共享
// [ThreadStatic] 字段每线程各一份，批间 Reset 复用，零竞争零分配
/// <summary>
/// Serializes a batch of log entries into a generic JSON array payload.
/// Each entry produces: <c>timestamp</c>, <c>level</c>, <c>logger</c>, <c>context</c> (omitted if empty),
/// <c>message</c> (rendered), <c>exception</c> (omitted if null), <c>scope</c> (omitted if empty),
/// <c>properties</c> (omitted if none).
/// </summary>
public sealed class JsonArraySerializer : IHttpLogSerializer
{
    /// <summary>Shared singleton — stateless, safe to share across threads.</summary>
    public static readonly JsonArraySerializer Default = new();

    /// <inheritdoc/>
    public string ContentType => "application/json";

    // ===== ThreadStatic 批级资源（每线程各一份，批间 Reset 复用，零竞争零分配）=====
    // ⚠️ [ThreadStatic] 字段仅在同步调用路径中安全。Serialize() 必须保持同步（无 await），
    //    否则续体可能在不同线程上执行，导致 ThreadStatic 状态被污染。

    private static readonly JsonWriterOptions _writerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        SkipValidation = false
    };

    [ThreadStatic] private static BufferWriter? _batchBuffer;
    [ThreadStatic] private static Utf8JsonWriter? _batchWriter;

    // char[] 用于消息渲染，自增长不缩减
    [ThreadStatic] private static char[]? _renderChars;

    // IDestructurable 专用 scratch 资源（与批级资源分离，避免嵌套写入冲突）
    [ThreadStatic] private static BufferWriter? _destructureBuffer;
    [ThreadStatic] private static Utf8JsonWriter? _destructureWriter;

    private static (BufferWriter buf, Utf8JsonWriter w) GetBatchResources()
    {
        _batchBuffer ??= new BufferWriter(4096);
        _batchWriter ??= new Utf8JsonWriter(Stream.Null, _writerOptions);
        _batchBuffer.Reset();
        _batchWriter.Reset(_batchBuffer);
        return (_batchBuffer, _batchWriter);
    }

    private static char[] GetRenderChars(int minSize)
    {
        if (_renderChars is null || _renderChars.Length < minSize)
            _renderChars = new char[Math.Max(minSize * 2, 512)];
        return _renderChars;
    }

    // ===== IHttpLogSerializer =====

    /// <inheritdoc/>
    public HttpContent Serialize(IReadOnlyList<LogEntry> entries)
    {
        var (buffer, writer) = GetBatchResources();

        writer.WriteStartArray();
        foreach (var entry in entries)
            WriteEntry(writer, entry);
        writer.WriteEndArray();
        writer.Flush();

        return MakeContent(buffer);
    }

    // ===== 核心渲染（internal 供 LokiSerializer 复用）=====

    internal static void WriteEntry(Utf8JsonWriter writer, LogEntry entry)
    {
        writer.WriteStartObject();

        writer.WriteString("timestamp"u8, entry.Timestamp);
        writer.WriteString("level"u8, MapLevel(entry.LogLevel));
        writer.WriteString("logger"u8, entry.LoggerName);

        if (!string.IsNullOrEmpty(entry.Context))
            writer.WriteString("context"u8, entry.Context);

        WriteRenderedMessage(writer, entry);

        if (entry.Exception is not null)
            writer.WriteString("exception"u8, entry.Exception.ToString());

        if (!string.IsNullOrEmpty(entry.Scope))
            writer.WriteString("scope"u8, entry.Scope);

        WriteProperties(writer, entry);

        writer.WriteEndObject();
    }

    // ===== 私有辅助 =====

    private static void WriteRenderedMessage(Utf8JsonWriter writer, LogEntry entry)
    {
        var tokens = entry.MessageTemplate.MessageTemplateTokens;

        // 无 token 时直接写模板字符串（无需渲染）
        if (tokens.Count == 0)
        {
            writer.WriteString("message"u8, entry.Message);
            return;
        }

        // 估算容量：模板长度 + 每个属性估 16 字符
        int estimatedLen = entry.Message.Length + entry.Properties.Length * 16;
        char[] chars = GetRenderChars(estimatedLen);
        int written = 0;
        int propIndex = 0;

        foreach (var token in tokens)
        {
            if (token is TextToken tt)
            {
                written = AppendToChars(ref chars, written, tt.Text.AsSpan());
            }
            else if (token is PropertyToken pt)
            {
                if (propIndex < entry.Properties.Length)
                {
                    var value = entry.Properties[propIndex];
                    string rendered;
                    if (value is null)
                        rendered = "null";
                    else if (pt.FormatString == "{0}")
                        rendered = value.ToString() ?? "null";
                    else
                        rendered = string.Format(CultureInfo.InvariantCulture, pt.FormatString, value);

                    written = AppendToChars(ref chars, written, rendered.AsSpan());
                }
                else
                {
                    written = AppendToChars(ref chars, written, pt.RawText.Text.AsSpan());
                }
                propIndex++;
            }
        }

        // _renderChars 可能在 AppendToChars 内被替换为更大数组，需更新本地引用
        writer.WriteString("message"u8, chars.AsSpan(0, written));
    }

    // 将 source 追加到 chars 中，若容量不足则自动翻倍扩容并更新 chars 引用
    private static int AppendToChars(ref char[] chars, int written, ReadOnlySpan<char> source)
    {
        int needed = written + source.Length;
        if (needed > chars.Length)
        {
            var newChars = new char[Math.Max(needed * 2, 512)];
            chars.AsSpan(0, written).CopyTo(newChars);
            _renderChars = newChars;
            chars = newChars;
        }
        source.CopyTo(chars.AsSpan(written));
        return written + source.Length;
    }

    private static void WriteProperties(Utf8JsonWriter writer, LogEntry entry)
    {
        int propIndex = 0;
        bool started = false;

        foreach (var token in entry.MessageTemplate.MessageTemplateTokens)
        {
            if (token is not PropertyToken pt) continue;

            if (!started)
            {
                writer.WriteStartObject("properties"u8);
                started = true;
            }

            if (propIndex < entry.Properties.Length)
                WritePropertyValue(writer, pt, entry.Properties[propIndex]);

            propIndex++;
        }

        if (started) writer.WriteEndObject();
    }

    // double/float NaN 和 ±Inf 在 JSON 中非法，转为字符串表示保留语义
    // IUtf8SpanFormattable 走栈上路径，DateTime/DateTimeOffset 强制使用 ISO8601 ("O") 格式
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "JSON serialization uses JsonSerializationConfig.Options which can be configured with JsonTypeInfoResolver for AOT compatibility.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Same as IL2026.")]
    internal static void WritePropertyValue(Utf8JsonWriter writer, PropertyToken pt, object? value)
    {
        if (value is null) { writer.WriteNull(pt.PropertyName); return; }

        // {$Value} 字符串化：无论原始类型，均写为 JSON string
        if (pt.Destructuring == Destructuring.Stringify)
        {
            writer.WriteString(pt.PropertyName, value.ToString());
            return;
        }

        if (pt.Destructuring == Destructuring.Destructure ||
           (global::Lunarium.Logging.Config.GlobalConfig.DestructuringConfig.AutoDestructureCollections && value is Array))
        {
            writer.WritePropertyName(pt.PropertyName);
            if (value is IDestructurable destructurable)
            {
                // 使用 scratch buffer/writer，避免与外层 batch writer 嵌套冲突
                _destructureBuffer ??= new BufferWriter(512);
                _destructureWriter ??= new Utf8JsonWriter(Stream.Null, _writerOptions);
                var helper = new DestructureHelper(_destructureBuffer, false, _destructureWriter, false);
                destructurable.Destructure(helper);
                if (helper.TryFlush())
                    writer.WriteRawValue(helper.WrittenSpan);
                else
                    writer.WriteNullValue();
                helper.Dispose(false, false);
            }
            else if (value is IDestructured destructured)
            {
                writer.WriteRawValue(destructured.Destructured().Span);
            }
            else
            {
                JsonSerializer.Serialize(writer, value, global::Lunarium.Logging.Config.GlobalConfig.JsonSerializationConfig.Options);
            }
            return;
        }

        switch (value)
        {
            case bool b: writer.WriteBoolean(pt.PropertyName, b); break;
            case int i: writer.WriteNumber(pt.PropertyName, i); break;
            case long l: writer.WriteNumber(pt.PropertyName, l); break;
            case double d:
                if (double.IsFinite(d)) writer.WriteNumber(pt.PropertyName, d);
                else writer.WriteString(pt.PropertyName, d.ToString(CultureInfo.InvariantCulture));
                break;
            case float f:
                if (float.IsFinite(f)) writer.WriteNumber(pt.PropertyName, f);
                else writer.WriteString(pt.PropertyName, f.ToString(CultureInfo.InvariantCulture));
                break;
            case decimal m: writer.WriteNumber(pt.PropertyName, m); break;
            case uint ui: writer.WriteNumber(pt.PropertyName, ui); break;
            case ulong ul: writer.WriteNumber(pt.PropertyName, ul); break;
            case IUtf8SpanFormattable formattable:
                Span<byte> utf8Buffer = stackalloc byte[128];
                ReadOnlySpan<char> fmt = (value is DateTime or DateTimeOffset) ? "O" : default;
                if (formattable.TryFormat(utf8Buffer, out int written, fmt, CultureInfo.InvariantCulture))
                {
                    writer.WritePropertyName(pt.PropertyName);
                    writer.WriteStringValue(utf8Buffer[..written]);
                }
                else
                {
                    writer.WriteString(pt.PropertyName, value.ToString());
                }
                break;
            default: writer.WriteString(pt.PropertyName, value.ToString()); break;
        }
    }

    internal static HttpContent MakeContent(BufferWriter buffer)
        => new ByteArrayContent(buffer.WrittenSpan.ToArray())
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" } }
        };

    private static string MapLevel(LogLevel level) => level switch
    {
        LogLevel.Debug => "Debug",
        LogLevel.Info => "Information",
        LogLevel.Warning => "Warning",
        LogLevel.Error => "Error",
        LogLevel.Critical => "Critical",
        _ => level.ToString()
    };
}
