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

using Lunarium.Logging.Writer;

namespace Lunarium.Logging.Target;

/// <summary>
/// Writes log entries to the standard console streams.
/// Supports plain text, ANSI-colored text, and JSON output modes.
/// Output is written directly to the underlying byte streams, avoiding intermediate char→byte conversion.
/// <see cref="LogLevel.Error"/> and above are routed to <c>stderr</c>; all others go to <c>stdout</c>.
/// </summary>
public sealed class ConsoleTarget : ILogTarget, IJsonTextTarget, ITextTarget
{
    // 线程安全锁，以防止多条日志消息的输出在控制台中交错
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    // 缓存底层字节流，避免每次 Emit 时重复调用 OpenStandard*()
    private readonly Stream _stdout;
    private readonly Stream _stderr;

    /// <summary>When <see langword="true"/>, entries are rendered as JSON. Defaults to <see langword="false"/>.</summary>
    public bool ToJson { get; init; } = false;
    /// <summary>When <see langword="true"/>, ANSI color codes are applied to text output. Defaults to <see langword="true"/>.</summary>
    public bool IsColor { get; init; } = true;

    private TextOutputIncludeConfig _textOutputIncludeConfig;

    /// <summary>Initializes a new <see cref="ConsoleTarget"/>.</summary>
    /// <param name="toJson">When <see langword="true"/>, entries are rendered as JSON. Defaults to <see langword="false"/>.</param>
    /// <param name="isColor">When <see langword="true"/>, ANSI color codes are applied. Defaults to <see langword="true"/>.</param>
    /// <param name="textOutputIncludeConfig">Controls which fields appear in text output. Uses defaults when <see langword="null"/>.</param>
    public ConsoleTarget(bool? toJson = false, bool? isColor = true, TextOutputIncludeConfig? textOutputIncludeConfig = null)
    {
        ToJson = toJson ?? false;
        IsColor = isColor ?? true;
        _textOutputIncludeConfig = textOutputIncludeConfig ?? new TextOutputIncludeConfig();

        _stdout = Console.OpenStandardOutput();
        _stderr = Console.OpenStandardError();
    }

    /// <inheritdoc/>
    public void UpdateTextOutputIncludeConfig(TextOutputIncludeConfig config)
    {
        // 原子性地更新配置
        Interlocked.Exchange(ref _textOutputIncludeConfig, config);
    }

    /// <inheritdoc/>
    public TextOutputIncludeConfig GetTextOutputIncludeConfig() => _textOutputIncludeConfig;

    // ====[TEST]==========================================
    // 供测试使用的内部构造函数
    internal ConsoleTarget(Stream stdout, Stream stderr, TextOutputIncludeConfig? textOutputIncludeConfig = null)
    {
        _textOutputIncludeConfig = textOutputIncludeConfig ?? new TextOutputIncludeConfig();
        _stdout = stdout;
        _stderr = stderr;
    }

    internal ConsoleTarget(Stream stdout, Stream stderr, bool? toJson, bool? isColor, TextOutputIncludeConfig? textOutputIncludeConfig = null)
    {
        ToJson = toJson ?? false;
        IsColor = isColor ?? true;
        _textOutputIncludeConfig = textOutputIncludeConfig ?? new TextOutputIncludeConfig();

        _stdout = stdout;
        _stderr = stderr;
    }
    // ====[TEST]==========================================

    /// <summary>Formats the log entry and writes it to the appropriate console stream.</summary>
    /// <param name="entry">The log entry to emit.</param>
    public void Emit(LogEntry entry)
    {
        Stream targetStream = entry.LogLevel >= LogLevel.Error ? _stderr : _stdout;

        bool useColor = IsColor && ShouldUseColor(targetStream == _stderr);
        // 根据配置选择格式化器
        LogWriter logWriter;
        if (ToJson)
        {
            logWriter = WriterPool.Get<LogJsonWriter>();
        }
        else if (useColor)
        {
            logWriter = WriterPool.Get<LogColorTextWriter>();
        }
        else
        {
            logWriter = WriterPool.Get<LogTextWriter>();
        }

        try
        {
            // 渲染日志
            if (logWriter is ITextTarget textTarget)
            {
                logWriter.Render(entry, _textOutputIncludeConfig);
            }
            else
            {
                logWriter.Render(entry);
            }

            // 选择输出流
            Stream output = entry.LogLevel >= LogLevel.Error ? _stderr : _stdout;

            // 输出
            lock (_lock)
            {
                logWriter.FlushTo(output);
            }
        }
        finally
        {
            // 归还对象池
            logWriter.Return();
        }
    }

    // 辅助判断逻辑：结合重定向状态和环境变量（NO_COLOR / TERM=dumb）
    private bool ShouldUseColor(bool isErrorStream)
    {
        // 检查对应流的重定向状态
        bool isRedirected = isErrorStream ? Console.IsErrorRedirected : Console.IsOutputRedirected;
        if (isRedirected) return false;

        // 检查 NO_COLOR 规范 (https://no-color.org/)
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))) return false;

        // 检查哑终端
        if (Environment.GetEnvironmentVariable("TERM") == "dumb") return false;

        return true;
    }

    /// <summary>No-op. The underlying console streams are managed by the .NET runtime.</summary>
    public void Dispose() { }
}
