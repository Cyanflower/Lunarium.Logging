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

namespace Lunarium.Logging.Target;

/// <summary>
/// Defines the contract for log output targets.
/// All concrete targets (console, file, channel, etc.) must implement this interface.
/// </summary>
public interface ILogTarget : IDisposable
{
    /// <summary>
    /// Writes or forwards a single log entry to the underlying output destination.
    /// Called from the logger's background processing loop; must be thread-safe.
    /// </summary>
    /// <param name="entry">The log entry to emit.</param>
    void Emit(LogEntry entry);
}

// 实现此接口的 Target 支持 JSON 格式切换；如同时实现 ITextTarget 则两者独立控制
/// <summary>
/// Marks a target as supporting JSON output mode.
/// When <see cref="ToJson"/> is <see langword="true"/>, the target should select
/// <c>LogJsonWriter</c> over <c>LogTextWriter</c>.
/// </summary>
public interface IJsonTextTarget
{
    /// <summary>Whether JSON-formatted output is enabled for this target.</summary>
    public bool ToJson { get; init; }
}

// UpdateTextOutputIncludeConfig 支持运行时热更新（它不需要重启 Logger）；内部应使用 Interlocked.Exchange 保证并发安全
/// <summary>
/// Marks a target as supporting colored ANSI text output and field-visibility control.
/// </summary>
public interface ITextTarget
{
    /// <summary>Whether ANSI color output is enabled for this target.</summary>
    public bool IsColor { get; init; }
    /// <summary>Returns the current field-visibility configuration for text output.</summary>
    public TextOutputIncludeConfig GetTextOutputIncludeConfig();
    /// <summary>Replaces the current field-visibility configuration. Thread-safe via <c>Interlocked.Exchange</c>.</summary>
    public void UpdateTextOutputIncludeConfig(TextOutputIncludeConfig config);
}

// 标记接口：该 Target 不进行格式化，选择将 LogEntry 实例直接传递给消费者
/// <summary>
/// Marker interface indicating that this target forwards raw <see cref="LogEntry"/> objects
/// to a consumer without any text serialization.
/// </summary>
public interface ILogEntryTarget;

// 键山小类，支持 init（构造时一次性设置）和运行时热更新（UpdateTextOutputIncludeConfig）
/// <summary>
/// Specifies which header fields are included when writing text-format log entries.
/// Instances should be treated as immutable; create a new instance to update at runtime.
/// </summary>
public record class TextOutputIncludeConfig
{
    /// <summary>Include the entry timestamp.</summary>
    public bool IncludeTimestamp { get; init; } = true;
    /// <summary>Include the logger name.</summary>
    public bool IncludeLoggerName { get; init; } = true;
    /// <summary>Include the severity level.</summary>
    public bool IncludeLevel { get; init; } = true;
    /// <summary>Include the context string.</summary>
    public bool IncludeContext { get; init; } = true;
}