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

using System.Threading.Channels;
using Lunarium.Logging.Writer;

namespace Lunarium.Logging.Target;

/// <summary>
/// Abstract base for channel-backed targets. Subclasses implement <see cref="Transform"/> to
/// convert a <see cref="LogEntry"/> into the desired output type <typeparamref name="T"/>;
/// channel write safety is handled by this base class.
/// </summary>
public abstract class ChannelTarget<T> : ILogTarget, IDisposable
{
    private readonly ChannelWriter<T> _writer;

    /// <summary>Initializes the target with the given channel writer.</summary>
    /// <param name="writer">The channel writer to which transformed entries are written.</param>
    protected ChannelTarget(ChannelWriter<T> writer)
    {
        _writer = writer;
    }

    /// <summary>Converts a <see cref="LogEntry"/> to <typeparamref name="T"/>. Implemented by subclasses.</summary>
    /// <param name="entry">The log entry to transform.</param>
    /// <returns>The transformed value to write to the channel.</returns>
    protected abstract T Transform(LogEntry entry);

    /// <summary>Transforms the entry and writes it to the channel. Drops the entry if the channel is full.</summary>
    /// <param name="entry">The log entry to emit.</param>
    public void Emit(LogEntry entry)
    {
        var item = Transform(entry);
        _writer.TryWrite(item);
    }

    /// <summary>Completes the channel writer, signalling no more items will be written.</summary>
    public void Dispose() => _writer.TryComplete();
}

/// <summary>
/// Formats each <see cref="LogEntry"/> as a <see cref="string"/> and writes it to the channel.
/// </summary>
/// <remarks>
/// Each call to <see cref="ILogTarget.Emit"/> incurs one <see cref="string"/> heap allocation
/// (UTF-8 → UTF-16 decode). If the consumer writes directly to a network stream or file,
/// prefer <see cref="ByteChannelTarget"/> to avoid that allocation.
/// </remarks>
public sealed class StringChannelTarget : ChannelTarget<string>, IJsonTextTarget, ITextTarget
{
    /// <summary>
    /// Output format override. <see langword="true"/> forces JSON output, <see langword="false"/> forces plain text.
    /// <see langword="null"/> (default) leaves the target's own default in effect.
    /// </summary>
    public bool ToJson { get; init; } = false;

    /// <summary>Whether ANSI color codes are included in the output. Defaults to <see langword="false"/>.</summary>
    public bool IsColor { get; init; } = false;
    private TextOutputIncludeConfig _textOutputIncludeConfig;

    /// <summary>Initializes a new <see cref="StringChannelTarget"/>.</summary>
    /// <param name="writer">The channel writer that receives formatted strings.</param>
    /// <param name="toJson">When <see langword="true"/>, entries are rendered as JSON.</param>
    /// <param name="isColor">When <see langword="true"/>, ANSI color codes are included in text output.</param>
    /// <param name="textOutputIncludeConfig">Controls which fields appear in text output. Uses defaults when <see langword="null"/>.</param>
    public StringChannelTarget(ChannelWriter<string> writer, bool toJson, bool isColor, TextOutputIncludeConfig? textOutputIncludeConfig = null)
        : base(writer)
    {
        ToJson = toJson;
        IsColor = isColor;
        _textOutputIncludeConfig = textOutputIncludeConfig ?? new TextOutputIncludeConfig();
    }

    /// <inheritdoc/>
    protected override string Transform(LogEntry entry)
    {
        // 根据配置选择格式化器
        // ToJson 优先级最高，其次是是否使用颜色(如果输出为Json则不使用颜色)
        // ToJson?
        //    是 -> LogJsonWriter
        //    否 -> _isColor?
        //           是 -> LogColorTextWriter
        //           否 -> LogTextWriter
        LogWriter logWriter = ToJson
            ? WriterPool.Get<LogJsonWriter>()
            : IsColor
                ? WriterPool.Get<LogColorTextWriter>()
                : WriterPool.Get<LogTextWriter>();
        try
        {
            if (logWriter is ITextTarget textTarget)
            {
                logWriter.Render(entry, _textOutputIncludeConfig);
            }
            else
            {
                logWriter.Render(entry);
            }
            return logWriter.ToString();
        }
        finally
        {
            logWriter.Return();
        }
    }

    /// <inheritdoc/>
    public void UpdateTextOutputIncludeConfig(TextOutputIncludeConfig config)
    {
        // 原子性地更新配置
        Interlocked.Exchange(ref _textOutputIncludeConfig, config);
    }

    /// <inheritdoc/>
    public TextOutputIncludeConfig GetTextOutputIncludeConfig() => _textOutputIncludeConfig;
}

/// <summary>
/// Formats each <see cref="LogEntry"/> as a UTF-8 <see cref="byte"/> array and writes it to the channel.
/// Skips the UTF-8 → UTF-16 decode step compared to <see cref="StringChannelTarget"/>,
/// making it suitable for consumers that write directly to a network stream or file.
/// </summary>
/// <remarks>
/// Each call to <see cref="ILogTarget.Emit"/> still allocates one <c>byte[]</c> (<c>ToArray()</c>).
/// For zero-allocation pass-through, use <see cref="LogEntryChannelTarget"/> instead.
/// </remarks>
public sealed class ByteChannelTarget : ChannelTarget<byte[]>, IJsonTextTarget, ITextTarget
{
    /// <summary>
    /// Output format override. <see langword="true"/> forces JSON output, <see langword="false"/> forces plain text.
    /// <see langword="null"/> (default) leaves the target's own default in effect.
    /// </summary>
    public bool ToJson { get; init; } = false;
    /// <summary>Whether ANSI color codes are included in the output. Defaults to <see langword="false"/>.</summary>
    public bool IsColor { get; init; } = false;
    private TextOutputIncludeConfig _textOutputIncludeConfig;

    /// <summary>Initializes a new <see cref="ByteChannelTarget"/>.</summary>
    /// <param name="writer">The channel writer that receives the formatted byte arrays.</param>
    /// <param name="toJson">When <see langword="true"/>, entries are rendered as JSON.</param>
    /// <param name="isColor">When <see langword="true"/>, ANSI color codes are included in text output.</param>
    /// <param name="textOutputIncludeConfig">Controls which fields appear in text output. Uses defaults when <see langword="null"/>.</param>
    public ByteChannelTarget(ChannelWriter<byte[]> writer, bool toJson, bool isColor, TextOutputIncludeConfig? textOutputIncludeConfig = null)
        : base(writer)
    {
        ToJson = toJson;
        IsColor = isColor;
        _textOutputIncludeConfig = textOutputIncludeConfig ?? new TextOutputIncludeConfig();
    }

    /// <inheritdoc/>
    protected override byte[] Transform(LogEntry entry)
    {
        LogWriter logWriter = ToJson
            ? WriterPool.Get<LogJsonWriter>()
            : IsColor
                ? WriterPool.Get<LogColorTextWriter>()
                : WriterPool.Get<LogTextWriter>();
        try
        {
            logWriter.Render(entry);
            return logWriter.GetWrittenBytes();
        }
        finally
        {
            logWriter.Return();
        }
    }

    /// <inheritdoc/>
    public void UpdateTextOutputIncludeConfig(TextOutputIncludeConfig config)
    {
        // 原子性地更新配置
        Interlocked.Exchange(ref _textOutputIncludeConfig, config);
    }

    /// <inheritdoc/>
    public TextOutputIncludeConfig GetTextOutputIncludeConfig() => _textOutputIncludeConfig;
}

/// <summary>
/// Passes <see cref="LogEntry"/> instances to the channel without any formatting.
/// Use this when the consumer needs the full structured entry (e.g. for custom processing).
/// </summary>
public sealed class LogEntryChannelTarget : ChannelTarget<LogEntry>, ILogEntryTarget
{
    /// <summary>Initializes a new <see cref="LogEntryChannelTarget"/>.</summary>
    /// <param name="writer">The channel writer that receives log entries.</param>
    public LogEntryChannelTarget(ChannelWriter<LogEntry> writer) : base(writer) { }

    /// <inheritdoc/>
    protected override LogEntry Transform(LogEntry entry) => entry;
}


// 将 LogEntry 转换为自定义类型 T 写入 Channel
internal sealed class DelegateChannelTarget<T> : ChannelTarget<T>
{
    private readonly Func<LogEntry, T> _transform; // 存起来备用

    public DelegateChannelTarget(ChannelWriter<T> writer, Func<LogEntry, T> transform)
        : base(writer)
    {
        _transform = transform;
    }

    protected override T Transform(LogEntry entry) => _transform(entry); // 转发给委托
}
