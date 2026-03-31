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

using Lunarium.Logging.Parser;

namespace Lunarium.Logging.Models;

/// <summary>
/// Immutable data record representing a single log event.
/// Carries all information needed by sinks to filter and render the entry.
/// </summary>
public sealed record class LogEntry
{
    // 只读属性，在构造函数中初始化后不能更改
    /// <summary>Name of the logger instance that produced this entry.</summary>
    public string LoggerName { get; }
    /// <summary>Pre-encoded UTF-8 bytes of <see cref="LoggerName"/>, used by writers for zero-allocation output.</summary>
    public ReadOnlyMemory<byte> LoggerNameBytes { get; }
    /// <summary>Timestamp of the log event.</summary>
    public DateTimeOffset Timestamp { get; }
    /// <summary>Severity level of the log event.</summary>
    public LogLevel LogLevel { get; }
    /// <summary>Raw message template string (e.g. <c>"User {Id} logged in"</c>).</summary>
    public string Message { get; }
    /// <summary>Positional property values corresponding to the holes in <see cref="Message"/>.</summary>
    public object?[] Properties { get; }
    /// <summary>Context label identifying the source module or component (e.g. a class name).</summary>
    public string Context { get; }
    /// <summary>Pre-encoded UTF-8 bytes of <see cref="Context"/>, used by writers for zero-allocation output.</summary>
    public ReadOnlyMemory<byte> ContextBytes { get; }
    /// <summary>MEL scope string populated by the hosting adapter. Empty for entries logged directly via <see cref="ILogger"/>.</summary>
    public string Scope { get; }
    /// <summary>Exception associated with this entry, or <see langword="null"/> if none.</summary>
    public Exception? Exception { get; }

    // 可读写属性，用于延迟解析和渲染
    /// <summary>
    /// Parsed message template. Populated lazily by <c>Sink.Emit()</c> just before rendering;
    /// holds <c>EmptyMessageTemplate</c> until then.
    /// </summary>
    public MessageTemplate MessageTemplate { get; private set; }

    /// <summary>Initializes a new <see cref="LogEntry"/> with all fields.</summary>
    /// <param name="loggerName">Name of the originating logger.</param>
    /// <param name="loggerNameBytes">Pre-encoded UTF-8 bytes of <paramref name="loggerName"/>.</param>
    /// <param name="timestamp">Time the event occurred.</param>
    /// <param name="logLevel">Severity level.</param>
    /// <param name="message">Raw message template string.</param>
    /// <param name="properties">Property values for the template holes.</param>
    /// <param name="context">Source context label.</param>
    /// <param name="contextBytes">Pre-encoded UTF-8 bytes of <paramref name="context"/>.</param>
    /// <param name="scope">MEL scope information.</param>
    /// <param name="messageTemplate">Initial template (typically <c>EmptyMessageTemplate</c> before lazy parsing).</param>
    /// <param name="exception">Associated exception, if any.</param>
    public LogEntry(
        string loggerName,
        ReadOnlyMemory<byte> loggerNameBytes,
        DateTimeOffset timestamp,
        LogLevel logLevel,
        string message,
        object?[] properties,
        string context,
        ReadOnlyMemory<byte> contextBytes,
        string scope,
        MessageTemplate messageTemplate,
        Exception? exception = null)
    {
        LoggerName = loggerName;
        LoggerNameBytes = loggerNameBytes;
        Timestamp = timestamp;
        LogLevel = logLevel;
        Message = message;
        Properties = properties;
        Context = context;
        ContextBytes = contextBytes;
        Scope = scope;
        MessageTemplate = messageTemplate;
        Exception = exception;
    }

    internal void ParseMessage()
    {
        if (object.ReferenceEquals(MessageTemplate, LogParser.EmptyMessageTemplate))
        {
            MessageTemplate = LogParser.ParseMessage(Message);
        }
    }
}