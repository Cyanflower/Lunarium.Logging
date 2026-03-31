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

internal sealed class LogTextWriter : LogWriter
{
    // ============= 池化管理 API =============
    protected override void ReturnToPool() => WriterPool.Return(this);

    #region 公共 API
    // =============== 公共 API ===============
    protected override LogTextWriter WriteTimestamp(DateTimeOffset timestamp)
    {
        _bufferWriter.Append("["u8);
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
        _bufferWriter.Append("] "u8);
        return this;
    }

    protected override LogTextWriter WriteLoggerName(ReadOnlyMemory<byte> loggerName)
    {
        if (!loggerName.IsEmpty)
        {
            _bufferWriter.Append("["u8);
            _bufferWriter.Append(loggerName.Span);
            _bufferWriter.Append("] "u8);
        }
        return this;
    }
    protected override LogTextWriter WriteLevel(LogLevel level)
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
        _bufferWriter.Append("["u8);
        _bufferWriter.Append(levelStr);
        _bufferWriter.Append("] "u8);
        return this;
    }

    protected override LogTextWriter WriteContext(ReadOnlyMemory<byte> context)
    {
        if (!context.IsEmpty)
        {
            _bufferWriter.Append("["u8);
            _bufferWriter.Append(context.Span);
            _bufferWriter.Append("] "u8);
        }
        return this;
    }

    protected override LogTextWriter WriteRenderedMessage(IReadOnlyList<MessageTemplateTokens> tokens, object?[] propertys)
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

    protected override LogTextWriter WriteException(Exception? exception)
    {
        if (exception != null)
        {
            _bufferWriter.AppendLine();
            _bufferWriter.Append(exception);
        }
        return this;
    }

    // ========================================
    #endregion
}
