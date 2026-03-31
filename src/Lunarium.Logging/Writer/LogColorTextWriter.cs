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

using System.Text;
using System.Globalization;
using Lunarium.Logging.Parser;

namespace Lunarium.Logging.Writer;


// 格式: [颜色指令]文本[重置指令]
// $"{AnsiPrefix}{colorCode}m{text}{AnsiReset}";
// ANSI标准允许用分号组合多个代码
// $"{AnsiPrefix}{foregroundCode};{backgroundCode}m{text}{AnsiReset}";
internal sealed class LogColorTextWriter : LogWriter
{
    // =============== 编译时常量区 ===============
    // ANSI 指令的前缀
    private static ReadOnlySpan<byte> AnsiPrefix => "\x1b["u8; // \x1b 是 ESC 字符的十六进制表示
    // 重置所有颜色和样式的指令
    private static ReadOnlySpan<byte> AnsiReset => "\x1b[0m"u8;
    private static ReadOnlySpan<byte> Prefix => "\x1b[90m[\x1b[0m"u8;
    private static ReadOnlySpan<byte> Suffix => "\x1b[90m]\x1b[0m "u8;
    // ===== 颜色定义 =====
    private const ConsoleColor TimestampColor = ConsoleColor.Green;
    private const ConsoleColor LevelDebugColor = ConsoleColor.DarkGray;
    private const ConsoleColor LevelInfoColor = ConsoleColor.Green;
    private const ConsoleColor LevelWarningColor = ConsoleColor.Yellow;
    private const ConsoleColor LevelErrorColor = ConsoleColor.Red;
    private const ConsoleColor LevelCriticalBgColor = ConsoleColor.DarkRed;
    private const ConsoleColor ContextColor = ConsoleColor.Cyan;
    private const ConsoleColor ExceptionColor = ConsoleColor.Red;

    // 值类型颜色
    private const ConsoleColor StringColor = ConsoleColor.Magenta;
    private const ConsoleColor NumberColor = ConsoleColor.Yellow;
    private const ConsoleColor BooleanColor = ConsoleColor.Blue;
    private const ConsoleColor NullColor = ConsoleColor.DarkBlue;
    private const ConsoleColor OtherColor = ConsoleColor.Gray; // 其他未知类型
    // =============== 编译时常量区 ===============

    // ============= 池化管理 API =============
    protected override void ReturnToPool() => WriterPool.Return(this);

    #region 公共 API
    // =============== 公共 API ===============
    protected override LogColorTextWriter WriteTimestamp(DateTimeOffset timestamp)
    {
        _bufferWriter.Append(Prefix);
        SetColor(TimestampColor);
        switch (TimestampFormatConfig.TextMode)
        {
            case TextTimestampMode.Unix:
                _bufferWriter.AppendFormattable(timestamp.ToUnixTimeSeconds());
                break;
            case TextTimestampMode.UnixMs:
                _bufferWriter.AppendFormattable(timestamp.ToUnixTimeMilliseconds());
                break;
            case TextTimestampMode.ISO8601:
                _bufferWriter.AppendFormattable(timestamp, "O");
                break;
            case TextTimestampMode.Custom:
                _bufferWriter.AppendFormattable(timestamp, TimestampFormatConfig.TextCustomFormat.AsSpan());
                break;
        }
        _bufferWriter.Append(Suffix);
        return this;
    }

    protected override LogColorTextWriter WriteLoggerName(ReadOnlyMemory<byte> loggerName)
    {
        if (!loggerName.IsEmpty)
        {
            _bufferWriter.Append(Prefix);
            SetColor(ContextColor);
            _bufferWriter.Append(loggerName.Span);
            _bufferWriter.Append(Suffix);
        }
        return this;
    }

    protected override LogColorTextWriter WriteLevel(LogLevel level)
    {
        var levelStr = level switch
        {
            LogLevel.Debug => "DBG"u8,
            LogLevel.Info => "INF"u8,
            LogLevel.Warning => "WRN"u8,
            LogLevel.Error => "ERR"u8,
            LogLevel.Critical => "CRT"u8,
            _ => "UNK"u8
        };
        Action levelColor = level switch
        {
            LogLevel.Debug => () => SetColor(LevelDebugColor),
            LogLevel.Info => () => SetColor(LevelInfoColor),
            LogLevel.Warning => () => SetColor(LevelWarningColor),
            LogLevel.Error => () => SetColor(LevelErrorColor),
            LogLevel.Critical => () => SetColor(ConsoleColor.White, LevelCriticalBgColor),
            _ => () => SetColor(ConsoleColor.White, ConsoleColor.Yellow)
        };
        _bufferWriter.Append(Prefix);
        levelColor.Invoke();
        _bufferWriter.Append(levelStr);
        _bufferWriter.Append(AnsiReset);
        _bufferWriter.Append(Suffix);
        return this;
    }

    protected override LogColorTextWriter WriteContext(ReadOnlyMemory<byte> context)
    {
        if (!context.IsEmpty)
        {
            _bufferWriter.Append(Prefix);
            SetColor(ContextColor);
            _bufferWriter.Append(context.Span);
            _bufferWriter.Append(Suffix);
        }
        return this;
    }

    protected override LogColorTextWriter WriteRenderedMessage(IReadOnlyList<MessageTemplateTokens> tokens, object?[] propertys)
    {
        int i = 0;
        foreach (var token in tokens)
        {
            switch (token)
            {
                case TextToken textToken:
                    _bufferWriter.Append(textToken.TextBytes.Span);
                    break;
                case PropertyToken propertyToken:
                    RenderPropertyToken(propertyToken, propertys, i);
                    i++;
                    break;
            }
        }
        return this;
    }

    protected override LogColorTextWriter WriteException(Exception? exception)
    {
        if (exception != null)
        {
            _bufferWriter.AppendLine();
            SetColor(ExceptionColor);
            _bufferWriter.Append(exception.ToString()); // Explicit ToString to ensure Append(string) is called
            _bufferWriter.Append(AnsiReset);
        }
        return this;
    }

    // ========================================
    #endregion

    #region 辅助方法
    // ================ 辅助方法 ================

    protected override void RenderPropertyToken(PropertyToken propertyToken, object?[] propertys, int i)
    {
        try
        {
            // 获取要渲染的值
            object? value = GetPropertyValue(propertys, i, out bool found);

            if (!found)
            {
                // 如果找不到对应的参数，输出原始文本
                _bufferWriter.Append(propertyToken.RawText.TextBytes.Span);
                return;
            }

            if (value is null)
            {
                SetValueColor(value);
                _bufferWriter.Append("null"u8);
                _bufferWriter.Append(AnsiReset);
                return;
            }

            // 当具有解构标识或设置了默认解构(且是集合类型)时, 尝试解构对象且跳过对齐和格式化(对json格式无意义)
            if (propertyToken.Destructuring == Destructuring.Destructure || (DestructuringConfig.AutoDestructureCollections && IsCommonCollectionType(value)))
            {
                // 当遇到 {@...} 时，使用 JsonSerializer 序列化 JSON 字符串
                if (value is IDestructurable destructurable)
                {
                    DestructureHelper destructureHelper = new DestructureHelper(_bufferWriter, false, _serializerWriter, true);
                    destructurable.Destructure(destructureHelper);

                    if (destructureHelper.TryFlush())
                    {
                        _bufferWriter.Append(destructureHelper.WrittenSpan);
                    }

                    destructureHelper.Dispose(false, true);
                }
                else if (value is IDestructured destructured)
                {
                    _bufferWriter.Append(destructured.Destructured().Span);
                }
                else
                {
                    TrySerializeToJson(value);
                }
                
                return; // 处理完就返回, 跳过构建格式字符串
            }

            // 高性能快路径：处理所有数值/时间类型 (IUtf8SpanFormattable)
            if (value is IUtf8SpanFormattable formattable)
            {
                SetValueColor(value);
                if (!propertyToken.Alignment.HasValue)
                {
                    // [极致优化] 无对齐：直接写入 BufferWriter，0 分配
                    _bufferWriter.AppendFormattable(formattable, propertyToken.Format);
                }
                else
                {
                    // [极致优化] 有对齐：手动补空格，避免 string.Format 解析开销
                    WriteAligned(propertyToken.Alignment.Value, formattable, propertyToken.Format);
                }
                _bufferWriter.Append(AnsiReset);
                return;
            }

            // 构建格式字符串，支持对齐和格式化
            SetValueColor(value);
            // string 专用重载：跳过 string.Format，直接 UTF-8 编码
            if (value is string strValue)
                _bufferWriter.AppendFormat(propertyToken.FormatString, strValue);
            else
                _bufferWriter.AppendFormat(propertyToken.FormatString, value);
            _bufferWriter.Append(AnsiReset);
        }
        catch (Exception ex)
        {
            _bufferWriter.Append(propertyToken.RawText.TextBytes.Span);
            InternalLogger.Error(ex, $"LogWriter WriteValue Failed: {propertyToken.PropertyName}");
        }
    }

    // 写入 ANSI 前景色指令
    private void SetColor(ConsoleColor color)
    {
        // 格式: [颜色指令]文本[重置指令]
        _bufferWriter.Append(AnsiPrefix);
        _bufferWriter.Append(ForegroundCode(color));
        _bufferWriter.Append('m');
    }

    private void SetColor(ConsoleColor foreground, ConsoleColor background)
    {
        // ANSI标准允许用分号组合多个代码
        _bufferWriter.Append(AnsiPrefix);
        _bufferWriter.Append(ForegroundCode(foreground));
        _bufferWriter.Append(';');
        _bufferWriter.Append(BackgroundCode(background));
        _bufferWriter.Append('m');
    }

    // 将 ConsoleColor 枚举值映射到对应的 ANSI 前景色代码
    private static ReadOnlySpan<byte> ForegroundCode(ConsoleColor color)
    {
        return color switch
        {
            // 这是标准的前景色代码
            ConsoleColor.Black => "30"u8,
            ConsoleColor.DarkRed => "31"u8,
            ConsoleColor.DarkGreen => "32"u8,
            ConsoleColor.DarkYellow => "33"u8,
            ConsoleColor.DarkBlue => "34"u8,
            ConsoleColor.DarkMagenta => "35"u8,
            ConsoleColor.DarkCyan => "36"u8,
            ConsoleColor.Gray => "37"u8,
            ConsoleColor.DarkGray => "90"u8,
            ConsoleColor.Red => "91"u8,
            ConsoleColor.Green => "92"u8,
            ConsoleColor.Yellow => "93"u8,
            ConsoleColor.Blue => "94"u8,
            ConsoleColor.Magenta => "95"u8,
            ConsoleColor.Cyan => "96"u8,
            ConsoleColor.White => "97"u8,
            _ => "37"u8 // 默认灰色
        };
    }
    // 背景色映射方法 (背景色代码通常是前景色+10)
    private static ReadOnlySpan<byte> BackgroundCode(ConsoleColor color)
    {
        return color switch
        {
            ConsoleColor.Black => "40"u8,
            ConsoleColor.DarkRed => "41"u8,
            ConsoleColor.DarkGreen => "42"u8,
            ConsoleColor.DarkYellow => "43"u8,
            ConsoleColor.DarkBlue => "44"u8,
            ConsoleColor.DarkMagenta => "45"u8,
            ConsoleColor.DarkCyan => "46"u8,
            ConsoleColor.Gray => "47"u8,
            ConsoleColor.DarkGray => "100"u8,
            ConsoleColor.Red => "101"u8,
            ConsoleColor.Green => "102"u8,
            ConsoleColor.Yellow => "103"u8,
            ConsoleColor.Blue => "104"u8,
            ConsoleColor.Magenta => "105"u8,
            ConsoleColor.Cyan => "106"u8,
            ConsoleColor.White => "107"u8,
            _ => "40"u8 // 默认黑色背景
        };
    }


    private void SetValueColor(object? value)
    {
        if (value == null) SetColor(NullColor);
        else switch (value)
            {
                // ===== 数值类型 (Numbers) =====
                case int:
                case long:
                case short:
                case byte:
                case uint:
                case ulong:
                case ushort:
                case sbyte:
                case decimal:
                case double:
                case float:
                    SetColor(NumberColor);
                    break;

                // ===== 布尔类型 (Boolean) =====
                case bool:
                    SetColor(BooleanColor);
                    break;

                // ===== 其他常见类型 - 字符串 =====
                case string:
                case char:
                case DateTime:
                case DateTimeOffset:
                case Guid:
                case TimeSpan:
                case Uri:
                    SetColor(StringColor);
                    break;

                // ===== 默认回退 (Default Fallback) =====
                default:
                    SetColor(OtherColor);
                    break;
            }
    }
    #endregion
}
