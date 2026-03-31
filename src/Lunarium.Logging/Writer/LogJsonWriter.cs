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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Lunarium.Logging.Parser;

namespace Lunarium.Logging.Writer;

internal sealed class LogJsonWriter : LogWriter
{
    private const int DefaultMaxCapacity = 32 * 1024; // Utf8JsonWriter 默认32KB阈值

    private static readonly JsonEncodedText TimestampKey = JsonEncodedText.Encode("Timestamp");
    private static readonly JsonEncodedText LevelKey     = JsonEncodedText.Encode("Level");
    private static readonly JsonEncodedText LogLevelKey  = JsonEncodedText.Encode("LogLevel");
    private static readonly JsonEncodedText MsgKey       = JsonEncodedText.Encode("OriginalMessage");
    private static readonly JsonEncodedText RenderedKey  = JsonEncodedText.Encode("RenderedMessage");
    private static readonly JsonEncodedText PropertysKey = JsonEncodedText.Encode("Propertys");
    private static readonly JsonEncodedText ContextKey   = JsonEncodedText.Encode("Context");
    private static readonly JsonEncodedText ExceptionKey = JsonEncodedText.Encode("Exception");

    // 专用于处理临时缓冲区, 用于单次方法执行, 严格遵循用处用前重置和用后原地重置
    private readonly BufferWriter _scratchWriter = new();

    // 主写入器
    private readonly Utf8JsonWriter _jsonWriter = new(Stream.Null, new JsonWriterOptions
        {
            Indented = JsonSerializationConfig.Options.WriteIndented,
            Encoder = JsonSerializationConfig.Options.Encoder
        });

    protected override void ReturnToPool() => WriterPool.Return(this);

    #region 公共 API

    protected override LogWriter BeginEntry()
    {
        _jsonWriter.Reset(_bufferWriter);
        _jsonWriter.WriteStartObject();
        return this;
    }

    protected override LogWriter EndEntry()
    {
        _jsonWriter.WriteEndObject();
        _jsonWriter.Flush();
        return this;
    }

    protected override void BeforeRenderMessage(LogEntry logEntry)
    {
        _jsonWriter.WriteString(MsgKey, logEntry.MessageTemplate.OriginalMessageBytes.Span);
    }

    protected override void AfterRenderMessage(LogEntry logEntry)
    {
        WritePropertyValue(_jsonWriter, logEntry.MessageTemplate.MessageTemplateTokens, logEntry.Properties);
    }

    // 尝试重置写入器以供重用, 如果对象因状态异常 (如缓冲区过大) 而不适合重用, 则返回 false。
    // maxCapacity：允许的最大缓冲区容量, 超过此容量的对象将被视为不健康, 该参数对 JsonWriter 无效
    //              (因为 Utf8JsonWriter 贪婪申请, 4KB会造成永远无法归池)
    internal override bool TryReset(int maxCapacity)
    {
        if (_bufferWriter.Capacity > maxCapacity || _scratchWriter.Capacity > maxCapacity)
        {
            // 对象被污染, 不应归还
            return false;
        }

        // 对象健康, 清理并准备重用
        _bufferWriter.Reset();
        _scratchWriter.Reset();
        _jsonWriter.Reset(_bufferWriter);

        return true;
    }
    #endregion

    #region Write Methods

    protected override LogWriter WriteTimestamp(DateTimeOffset timestamp)
    {
        switch (TimestampFormatConfig.JsonMode)
        {
            case JsonTimestampMode.Unix:
                _jsonWriter.WriteNumber(TimestampKey, timestamp.ToUnixTimeSeconds());
                break;
            case JsonTimestampMode.UnixMs:
                _jsonWriter.WriteNumber(TimestampKey, timestamp.ToUnixTimeMilliseconds());
                break;
            case JsonTimestampMode.ISO8601:
                Span<byte> isoBuffer = stackalloc byte[64];
                if (timestamp.TryFormat(isoBuffer, out int writtenIso, "O", CultureInfo.InvariantCulture))
                {
                    _jsonWriter.WriteString(TimestampKey, isoBuffer[..writtenIso]);
                }
                break;
            case JsonTimestampMode.Custom:
                Span<byte> customBuffer = stackalloc byte[64];
                if (timestamp.TryFormat(customBuffer, out int writtenCustom, TimestampFormatConfig.JsonCustomFormat, CultureInfo.InvariantCulture))
                {
                    _jsonWriter.WriteString(TimestampKey, customBuffer[..writtenCustom]);
                }
                break;
        }
        return this;
    }

    protected override LogWriter WriteLoggerName(ReadOnlyMemory<byte> loggerName)
    {
        _jsonWriter.WriteString("LoggerName", loggerName.Span);
        return this;
    }

    protected override LogWriter WriteLevel(LogLevel level)
    {
        _jsonWriter.WritePropertyName(LevelKey);
        _jsonWriter.WriteStringValue(level switch {
            LogLevel.Debug => "Debug"u8,
            LogLevel.Info => "Information"u8,
            LogLevel.Warning => "Warning"u8,
            LogLevel.Error => "Error"u8,
            LogLevel.Critical => "Critical"u8,
            _ => "Unknown"u8
        });
        _jsonWriter.WriteNumber(LogLevelKey, (int)level);
        return this;
    }

    protected override LogWriter WriteContext(ReadOnlyMemory<byte> context)
    {
        if (!context.IsEmpty)
        {
            _jsonWriter.WriteString(ContextKey, context.Span);
        }
        return this;
    }

    protected override LogWriter WriteRenderedMessage(IReadOnlyList<MessageTemplateTokens> tokens, object?[] propertys)
    {
        _scratchWriter.Reset();
        int i = 0;

        foreach (var token in tokens)
        {
            switch (token)
            {
                case TextToken textToken:
                    _scratchWriter.Append(textToken.TextBytes.Span);
                    break;
                case PropertyToken propertyToken:
                    RenderPropertyToken(propertyToken, propertys, i);
                    i++;
                    break;
            }
        }
        _jsonWriter.WriteString(RenderedKey, _scratchWriter.WrittenSpan);
        _scratchWriter.Reset();
        return this;
    }


    protected override void RenderPropertyToken(PropertyToken propertyToken, object?[] propertys, int i)
    {
        try
        {
            // ================
            // 1. 获取值
            // ================
            // 获取要渲染的值
            object? value = GetPropertyValue(propertys, i, out bool found);

            if (!found)
            {
                // 如果找不到对应的参数，输出原始文本
                _scratchWriter.Append(propertyToken.RawText.TextBytes.Span);
                return;
            }
            // 或值是 null, 直接输出 null (utf8字面量)
            if (value is null)
            {
                _scratchWriter.Append("null"u8);
                return;
            }

            // 当具有解构标识或设置了默认解构(且是集合类型)时, 尝试解构对象且跳过对齐和格式化(对json格式无意义)
            if (propertyToken.Destructuring == Destructuring.Destructure || (DestructuringConfig.AutoDestructureCollections && IsCommonCollectionType(value)))
            {
                // JsonWriter 的 RenderedMessage 不显示解构后的对象
                // 解构对象只存储于 Properties 字段中
                _scratchWriter.Append(value.ToString());
                return; // 处理完就返回, 跳过构建格式字符串, Json不能使用对齐和格式化
            }

            // 高性能快路径：处理所有数值/时间类型 (IUtf8SpanFormattable)
            if (value is IUtf8SpanFormattable formattable)
            {
                if (!propertyToken.Alignment.HasValue)
                {
                    // [极致优化] 无对齐：直接写入 BufferWriter，0 分配
                    _scratchWriter.AppendFormattable(formattable, propertyToken.Format);
                    return;
                }
                else
                {
                    // [极致优化] 有对齐：手动补空格，避免 string.Format 解析开销
                    WriteAligned(propertyToken.Alignment.Value, formattable, propertyToken.Format);
                    return;
                }
            }

            // 构建格式字符串，支持对齐和格式化
            // string 专用重载：跳过 string.Format，直接 UTF-8 编码
            if (value is string strValue)
                _scratchWriter.AppendFormat(propertyToken.FormatString, strValue);
            else
                _scratchWriter.AppendFormat(propertyToken.FormatString, value);
        }
        catch (Exception ex)
        {
            _scratchWriter.Append(propertyToken.RawText.TextBytes.Span);
            InternalLogger.Error(ex, $"LogWriter WriteValue Failed: {propertyToken.PropertyName}");
        }
    }

    protected override void WriteAligned(int alignment, IUtf8SpanFormattable content, string? format)
    {
        Span<byte> temp = stackalloc byte[128];

        // 尝试格式化
        if (content.TryFormat(temp, out int written, format, CultureInfo.InvariantCulture))
        {
            // 计算需要的空格数
            int padding = Math.Abs(alignment) - written;
            // 如果内容已经超过或等于对齐宽度，直接写内容
            if (padding <= 0)
            {
                _scratchWriter.Append(temp[..written]);
                return;
            }
            // 3. 根据正负值处理左/右对齐
            if (alignment > 0) // 右对齐: [  123]
            {
                AppendPadding(padding);
                _scratchWriter.Append(temp[..written]);
            }
            else // 左对齐: [123  ]
            {
                _scratchWriter.Append(temp[..written]);
                AppendPadding(padding);
            }
        }
        else
        {
            // 极小概率失败（如格式符极其复杂），回退到通用路径或直接输出
            _scratchWriter.AppendFormattable(content, format);
        }
    }

    protected override void AppendPadding(int count)
    {
        // 如果需要的空格在池子范围内，直接 MemoryCopy
        if (count <= SpacePool.Length)
        {
            _scratchWriter.Append(SpacePool[..count]);
        }
        else
        {
            // 超过池子范围再回退到 Fill
            _scratchWriter.GetSpan(count)[..count].Fill((byte)' ');
            _scratchWriter.Advance(count);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "JSON serialization uses JsonSerializationConfig.Options which can be configured with JsonTypeInfoResolver for AOT compatibility.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Same as IL2026.")]
    private void WritePropertyValue(Utf8JsonWriter json, IReadOnlyList<MessageTemplateTokens> tokens, object?[] propertys)
    {
        json.WriteStartObject(PropertysKey);

        if (propertys.Length != 0)
        {
            int i = 0;
            foreach (var token in tokens)
            {
                if (token is PropertyToken propertyToken)
                {
                    json.WritePropertyName(propertyToken.PropertyName);

                    if (i >= propertys.Length || propertys[i] is null)
                    {
                        json.WriteNullValue();
                    }
                    else if (
                        propertyToken.Destructuring == Destructuring.Destructure ||
                        (DestructuringConfig.AutoDestructureCollections && IsCommonCollectionType(propertys[i])))
                    {
                        if (propertys[i] is IDestructurable destructurable)
                        {
                            var destructureHelper = new DestructureHelper(_scratchWriter, true, _serializerWriter, true);
                            
                            destructurable.Destructure(destructureHelper);

                            if (destructureHelper.TryFlush())
                            {
                                _jsonWriter.WriteRawValue(_scratchWriter.WrittenSpan);
                            }

                            destructureHelper.Dispose(true, true);
                        }
                        else if (propertys[i] is IDestructured destructured)
                        {
                            _jsonWriter.WriteRawValue(destructured.Destructured().Span);
                        }
                        else
                        {
                            JsonSerializer.Serialize(json, propertys[i], JsonSerializationConfig.Options);
                        }
                    }
                    else
                    {
                        WriteJsonValue(json, propertys[i]);
                    }

                    i++;
                }
            }
        }

        json.WriteEndObject();
    }

    private static void WriteJsonValue(Utf8JsonWriter json, object? value)
    {
        switch (value)
        {
            case null:
                json.WriteNullValue();
                break;
            case int i:
                json.WriteNumberValue(i);
                break;
            case long l:
                json.WriteNumberValue(l);
                break;
            case short s:
                json.WriteNumberValue(s);
                break;
            case byte b:
                json.WriteNumberValue(b);
                break;
            case uint ui:
                json.WriteNumberValue(ui);
                break;
            case ulong ul:
                json.WriteNumberValue(ul);
                break;
            case ushort us:
                json.WriteNumberValue(us);
                break;
            case sbyte sb:
                json.WriteNumberValue(sb);
                break;
            case decimal dec:
                json.WriteNumberValue(dec);
                break;
            case double d:
                if (double.IsNaN(d))
                    json.WriteStringValue("NaN"u8);
                else if (double.IsPositiveInfinity(d))
                    json.WriteStringValue("Infinity"u8);
                else if (double.IsNegativeInfinity(d))
                    json.WriteStringValue("-Infinity"u8);
                else
                    json.WriteNumberValue(d);
                break;
            case float f:
                if (float.IsNaN(f))
                    json.WriteStringValue("NaN"u8);
                else if (float.IsPositiveInfinity(f))
                    json.WriteStringValue("Infinity"u8);
                else if (float.IsNegativeInfinity(f))
                    json.WriteStringValue("-Infinity"u8);
                else
                    json.WriteNumberValue(f);
                break;
            case bool b:
                json.WriteBooleanValue(b);
                break;
            case string s:
                json.WriteStringValue(s);
                break;
            case char c:
                Span<char> charSpan = stackalloc char[1] { c };
                json.WriteStringValue(charSpan);
                break;
            case IUtf8SpanFormattable formattable:
                Span<byte> utf8Buffer = stackalloc byte[128]; 
                ReadOnlySpan<char> fmt = (value is DateTime or DateTimeOffset) ? "O" : default;
                
                if (formattable.TryFormat(utf8Buffer, out int written, fmt, CultureInfo.InvariantCulture))
                {
                    json.WriteStringValue(utf8Buffer[..written]);
                }
                else
                {
                    json.WriteStringValue(value.ToString());
                }
                break;
            default:
                json.WriteStringValue(value.ToString());
                break;
        }
    }

    protected override LogWriter WriteException(Exception? exception)
    {
        if (exception != null)
        {
            _jsonWriter.WriteString(ExceptionKey, exception.ToString());
        }
        return this;
    }

    #endregion
}
