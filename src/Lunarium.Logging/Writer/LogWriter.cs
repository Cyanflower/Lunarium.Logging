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

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Target;

namespace Lunarium.Logging.Writer;

internal abstract class LogWriter : IDisposable
{
    // 主写入器
    protected readonly BufferWriter _bufferWriter;

    // 专用于处理临时缓冲区和序列化, 用于单次方法执行, 严格遵循用处用前重置和用后原地重置
    protected readonly Utf8JsonWriter _serializerWriter;
    // 状态定义：0 = 使用中 (Active), 1 = 已释放/在池中 (Disposed/InPool)
    // 使用 int 以便进行 Interlocked 操作
    private int _disposedState = 0;
    private const int DefaultMaxCapacity = 32 * 1024; // 默认4KB阈值
    protected static ReadOnlySpan<byte> SpacePool => "                                "u8; // 32个空格, 别查了, 我tm亲手敲的, tmd有没有不这么丑的写法啊


    public LogWriter()
    {
        _bufferWriter = new BufferWriter(256);
        _serializerWriter = new Utf8JsonWriter(Stream.Null, new JsonWriterOptions
        {
            Indented = JsonSerializationConfig.Options.WriteIndented,
            Encoder = JsonSerializationConfig.Options.Encoder
        });
    }

    #region --- 池化管理 API ---

    // 获取内部 BufferWriter 的当前容量, 供对象池进行健康度检查
    internal virtual int GetBufferCapacity() => _bufferWriter.Capacity;

    // 尝试重置写入器以供重用, 如果对象因状态异常 (如缓冲区过大) 而不适合重用, 则返回 false。
    // maxCapacity：允许的最大缓冲区容量, 超过此容量的对象将被视为不健康
    internal virtual bool TryReset(int maxCapacity = DefaultMaxCapacity)
    {
        if (_bufferWriter.Capacity > maxCapacity)
        {
            // 对象被污染, 不应归还
            return false;
        }

        // 对象健康, 清理并准备重用
        _bufferWriter.Reset();

        // 如果派生类有其他状态, 它们应该重写此方法并调用 base.TryReset()
        return true;
    }

    // 当对象从池中取出时调用，将其标记为活跃状态
    internal void MarkAsActive()
    {
        // 使用 Volatile 确保所有 CPU 核心立即看到状态变更
        Volatile.Write(ref _disposedState, 0);
    }

    #region --- 对象池归还 ---
    // 将此 Writer 归还到对象池, 非 using 语句时手动调用
    public void Return()
    {
        Dispose();
    }

    // 实现 IDisposable, 支持 using 语句自动归还
    public void Dispose()
    {
        // 原子检查并设置：如果当前是 0，则设为 1；如果当前已经是 1，说明已归还，直接返回
        if (Interlocked.CompareExchange(ref _disposedState, 1, 0) != 0)
        {
            return;
        }

        // 只有抢到“从 0 变 1”权利的线程才能执行归还逻辑
        // 调用子类的归还逻辑
        ReturnToPool();

        GC.SuppressFinalize(this);
    }

    // 此方法主要用于 WriterPool 拒绝回收该对象时，确保 ArrayPool 资源不泄漏
    internal void DisposeAndReturnArrayBuffer()
    {
        try
        {
            // 增加对环境状态的判断：如果进程正在关闭，归还 ArrayPool 可能不再安全或不再必要
            if (Environment.HasShutdownStarted) return;
            // 直接释放内部缓冲区
            // 注意：_bufferWriter.Dispose() 内部已经处理了幂等性（多次调用安全）
            _bufferWriter?.Dispose();
        }
        catch (Exception ex)
        {
            // 析构函数中绝不能抛出异常，否则会导致进程崩溃
            // 我们通过内部日志记录（如果此时内部日志记录器还能用的话）
            InternalLogger.Error(ex, "Error in LogWriter Finalizer");
        }
    }

    // 抽象方法: 子类负责归还到自己的池
    protected abstract void ReturnToPool();

    #endregion
    #endregion

    #region --- 公共渲染入口 ---

    // 渲染日志条目到内部缓冲区（按 TextOutputIncludeConfig 控制字段可见性）
    internal void Render(LogEntry logEntry, TextOutputIncludeConfig config)
    {
        BeginEntry();

        if (config.IncludeTimestamp)
        {
            WriteTimestamp(logEntry.Timestamp);
        }

        if (config.IncludeLevel)
        {
            WriteLevel(logEntry.LogLevel);
        }

        if (config.IncludeLoggerName)
        {
            WriteLoggerName(logEntry.LoggerNameBytes);
        }

        if (config.IncludeContext)
        {
            WriteContext(logEntry.ContextBytes);
        }

        // 🎣 钩子：允许子类在渲染消息前插入额外逻辑(如 JSON 的 OriginalMessage)
        BeforeRenderMessage(logEntry);

        WriteRenderedMessage(logEntry.MessageTemplate.MessageTemplateTokens, logEntry.Properties);

        // 🎣 钩子：允许子类在渲染消息后插入额外逻辑(如 JSON 的 PropertyValue)
        AfterRenderMessage(logEntry);

        WriteException(logEntry.Exception);
        EndEntry();

        _bufferWriter.AppendLine();
    }

    internal void Render(LogEntry logEntry)
    {
        BeginEntry();

        WriteTimestamp(logEntry.Timestamp);
        WriteLevel(logEntry.LogLevel);
        WriteLoggerName(logEntry.LoggerNameBytes);
        WriteContext(logEntry.ContextBytes);

        // 🎣 钩子：允许子类在渲染消息前插入额外逻辑(如 JSON 的 OriginalMessage)
        BeforeRenderMessage(logEntry);

        WriteRenderedMessage(logEntry.MessageTemplate.MessageTemplateTokens, logEntry.Properties);

        // 🎣 钩子：允许子类在渲染消息后插入额外逻辑(如 JSON 的 PropertyValue)
        AfterRenderMessage(logEntry);

        WriteException(logEntry.Exception);
        EndEntry();

        _bufferWriter.AppendLine();
    }

    // 将已渲染内容直接写入流，零拷贝，无分配
    internal void FlushTo(Stream stream) => _bufferWriter.FlushTo(stream);

    // 将已渲染内容写入 TextWriter（降级路径：UTF-8 解码后写入）。
    // 小内容（≤4096 chars）使用 stackalloc，不产生堆分配；更大内容退化为 string 分配。
    internal void FlushTo(TextWriter output)
    {
        ReadOnlySpan<byte> bytes = _bufferWriter.WrittenSpan;
        if (bytes.IsEmpty) return;
        int charCount = Encoding.UTF8.GetCharCount(bytes);
        if (charCount <= 4096)
        {
            Span<char> chars = stackalloc char[charCount];
            Encoding.UTF8.GetChars(bytes, chars);
            output.Write(chars);
        }
        else
        {
            output.Write(Encoding.UTF8.GetString(bytes));
        }
    }

    // 将已渲染内容以 byte[] 形式返回（ByteChannelTarget 使用）
    internal byte[] GetWrittenBytes() => _bufferWriter.WrittenSpan.ToArray();

    public override string ToString()
    {
        return _bufferWriter.ToString();
    }

    #endregion

    #region --- 子类必须实现 ---

    protected abstract LogWriter WriteTimestamp(DateTimeOffset timestamp);
    protected abstract LogWriter WriteLevel(LogLevel level);
    protected abstract LogWriter WriteLoggerName(ReadOnlyMemory<byte> loggerName);
    protected abstract LogWriter WriteContext(ReadOnlyMemory<byte> context);
    protected abstract LogWriter WriteRenderedMessage(IReadOnlyList<MessageTemplateTokens> tokens, object?[] propertys);
    protected abstract LogWriter WriteException(Exception? exception);

    #endregion

    #region --- 子类可选重写 ---

    protected virtual void BeforeRenderMessage(LogEntry logEntry) { }
    protected virtual void AfterRenderMessage(LogEntry logEntry) { }
    protected virtual LogWriter BeginEntry() { return this; }
    protected virtual LogWriter EndEntry() { return this; }

    protected virtual void RenderPropertyToken(PropertyToken propertyToken, object?[] propertys, int i)
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
                _bufferWriter.Append(propertyToken.RawText.TextBytes.Span);
                return;
            }
            // 或值是 null, 直接输出 null (utf8字面量)
            if (value is null)
            {
                _bufferWriter.Append("null"u8);
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

                return; // 处理完就返回, 跳过构建格式字符串, Json不能使用对齐和格式化
            }

            // 高性能快路径：处理所有数值/时间类型 (IUtf8SpanFormattable)
            if (value is IUtf8SpanFormattable formattable)
            {
                if (!propertyToken.Alignment.HasValue)
                {
                    // [极致优化] 无对齐：直接写入 BufferWriter，0 分配
                    _bufferWriter.AppendFormattable(formattable, propertyToken.Format);
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
                _bufferWriter.AppendFormat(propertyToken.FormatString, strValue);
            else
                _bufferWriter.AppendFormat(propertyToken.FormatString, value);
        }
        catch (Exception ex)
        {
            _bufferWriter.Append(propertyToken.RawText.TextBytes.Span);
            InternalLogger.Error(ex, $"LogWriter WriteValue Failed: {propertyToken.PropertyName}");
        }
    }

    // 获取属性值
    protected virtual object? GetPropertyValue(object?[] propertys, int namedIndex, out bool found)
    {
        if (namedIndex >= propertys.Length || namedIndex < 0)
        {
            found = false;
            return null;
        }
        found = true;
        return propertys[namedIndex];
    }

    protected virtual void WriteAligned(int alignment, IUtf8SpanFormattable content, string? format)
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
                _bufferWriter.Append(temp[..written]);
                return;
            }
            // 3. 根据正负值处理左/右对齐
            if (alignment > 0) // 右对齐: [  123]
            {
                AppendPadding(padding);
                _bufferWriter.Append(temp[..written]);
            }
            else // 左对齐: [123  ]
            {
                _bufferWriter.Append(temp[..written]);
                AppendPadding(padding);
            }
        }
        else
        {
            // 极小概率失败（如格式符极其复杂），回退到通用路径或直接输出
            _bufferWriter.AppendFormattable(content, format);
        }
    }

    protected virtual void AppendPadding(int count)
    {
        // 如果需要的空格在池子范围内，直接 MemoryCopy
        if (count <= SpacePool.Length)
        {
            _bufferWriter.Append(SpacePool[..count]);
        }
        else
        {
            // 超过池子范围再回退到 Fill
            _bufferWriter.GetSpan(count)[..count].Fill((byte)' ');
            _bufferWriter.Advance(count);
        }
    }

    #endregion

    #region --- 序列化工具 ---

    // 将对象序列化为 JSON 字符串并追加到缓冲区。序列化失败时回退到序列化前的状态。
    // 若 GlobalConfigurator 注册了 JsonTypeInfoResolver，AOT 环境下可正常工作；
    // 否则在 AOT 下序列化将失败并静默回退。
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Serialization failure returns null and callers fall back gracefully. " +
                        "AOT users can register a JsonTypeInfoResolver via GlobalConfigurator.UseJsonTypeInfoResolver().")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Same as IL2026.")]
    protected void TrySerializeToJson(object? value)
    {
        var index = _bufferWriter.WrittenCount;
        // 无论如何进入时都重置 jsonWriter 并重新绑定 bufferWriter
        _serializerWriter.Reset(_bufferWriter);
        try
        {
            JsonSerializer.Serialize(_serializerWriter, value, JsonSerializationConfig.Options);
            _serializerWriter.Flush();
        }
        catch (Exception ex)
        {
            _bufferWriter.Rewind(index); // 回退到序列化前的状态，丢弃任何部分写入的内容
            InternalLogger.Error(ex, "LogWriter TrySerializeToJson Failed");
        }
        finally
        {
            _serializerWriter.Reset(Stream.Null);
        }
    }

    #endregion

    #region --- 钩子方法：子类可选重写 ---
    // 判断对象是否为C#核心库中常见的集合类型
    protected static bool IsCommonCollectionType(object? obj)
    {
        if (obj == null)
            return false;

        // 排除string（虽然它实现了IEnumerable，但通常不被视为集合）
        if (obj is string)
            return false;

        // 检查是否为数组（使用 is 模式而非反射，AOT 安全）
        if (obj is Array)
            return true;

        // 检查是否实现了常见的集合接口
        return obj is IEnumerable ||
               obj is ICollection ||
               obj is IList ||
               obj is IDictionary;
    }
    #endregion
}
