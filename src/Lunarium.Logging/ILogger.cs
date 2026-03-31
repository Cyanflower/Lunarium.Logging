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

using System.Runtime.CompilerServices;
using Lunarium.Logging.Models;

namespace Lunarium.Logging;

/// <summary>
/// Core logging interface. Provides structured log methods at five severity levels.
/// </summary>
public interface ILogger : IAsyncDisposable
{
    // context 应为静态字符串（类名、模块名），不含动态值；动态值放在消息模板属性中
    // contextBytes 是 context 的 UTF-8 预编码字节，由 LoggerWrapper 构造时一次性计算并透传，避免重复编码
    // scope 由 MEL 适配器自动填充，业务代码无需手动提供
    /// <summary>
    /// Writes a log entry at the specified severity level. This is the primary method that all
    /// convenience overloads delegate to.
    /// </summary>
    /// <param name="level">Severity level of the log entry.</param>
    /// <param name="ex">Exception associated with the entry, or <see langword="null"/>.</param>
    /// <param name="message">Message template, which may contain <c>{Property}</c> placeholders.</param>
    /// <param name="context">Static context string identifying the log source (e.g. class name).</param>
    /// <param name="contextBytes">Pre-encoded UTF-8 bytes of <paramref name="context"/>. Pass <see langword="default"/> to let the writer encode on demand.</param>
    /// <param name="scope">MEL scope string; populated automatically by the MEL adapter.</param>
    /// <param name="propertyValues">Values substituted into the message template placeholders, in order.</param>
    void Log(LogLevel level, Exception? ex = null, string message = "", string context = "", ReadOnlyMemory<byte> contextBytes = default, string scope = "", params object?[] propertyValues);

    // ========================================================================
    // Debug overloads
    // ========================================================================

    /// <summary>Writes a <c>Debug</c> log entry with a message template and optional property values.</summary>
    /// <param name="message">Message template.</param>
    /// <param name="propertyValues">Template property values.</param>
    void Debug(string message, params object?[] propertyValues)
        => Log(level: LogLevel.Debug, message: message, propertyValues: propertyValues);

    /// <summary>Writes a <c>Debug</c> log entry with an exception, a message template, and optional property values.</summary>
    /// <param name="ex">Exception to attach.</param>
    /// <param name="message">Message template.</param>
    /// <param name="propertyValues">Template property values.</param>
    void Debug(Exception? ex, string message, params object?[] propertyValues)
        => Log(level: LogLevel.Debug, ex: ex, message: message, propertyValues: propertyValues);

    /// <summary>Writes a <c>Debug</c> log entry containing only the given exception.</summary>
    /// <param name="ex">Exception to attach.</param>
    void Debug(Exception? ex)
        => Log(level: LogLevel.Debug, ex: ex);

    // ========================================================================
    // Info overloads
    // ========================================================================

    /// <summary>Writes an <c>Info</c> log entry with a message template and optional property values.</summary>
    /// <param name="message">Message template.</param>
    /// <param name="propertyValues">Template property values.</param>
    void Info(string message, params object?[] propertyValues)
        => Log(level: LogLevel.Info, message: message, propertyValues: propertyValues);

    /// <summary>Writes an <c>Info</c> log entry with an exception, a message template, and optional property values.</summary>
    /// <param name="ex">Exception to attach.</param>
    /// <param name="message">Message template.</param>
    /// <param name="propertyValues">Template property values.</param>
    void Info(Exception? ex, string message, params object?[] propertyValues)
        => Log(level: LogLevel.Info, ex: ex, message: message, propertyValues: propertyValues);

    /// <summary>Writes an <c>Info</c> log entry containing only the given exception.</summary>
    /// <param name="ex">Exception to attach.</param>
    void Info(Exception? ex)
        => Log(level: LogLevel.Info, ex: ex);

    // ========================================================================
    // Warning overloads
    // ========================================================================

    /// <summary>Writes a <c>Warning</c> log entry with a message template and optional property values.</summary>
    /// <param name="message">Message template.</param>
    /// <param name="propertyValues">Template property values.</param>
    void Warning(string message, params object?[] propertyValues)
        => Log(level: LogLevel.Warning, message: message, propertyValues: propertyValues);

    /// <summary>Writes a <c>Warning</c> log entry with an exception, a message template, and optional property values.</summary>
    /// <param name="ex">Exception to attach.</param>
    /// <param name="message">Message template.</param>
    /// <param name="propertyValues">Template property values.</param>
    void Warning(Exception? ex, string message, params object?[] propertyValues)
        => Log(level: LogLevel.Warning, ex: ex, message: message, propertyValues: propertyValues);

    /// <summary>Writes a <c>Warning</c> log entry containing only the given exception.</summary>
    /// <param name="ex">Exception to attach.</param>
    void Warning(Exception? ex)
        => Log(level: LogLevel.Warning, ex: ex);

    // ========================================================================
    // Error overloads
    // ========================================================================

    /// <summary>Writes an <c>Error</c> log entry with a message template and optional property values.</summary>
    /// <param name="message">Message template.</param>
    /// <param name="propertyValues">Template property values.</param>
    void Error(string message, params object?[] propertyValues)
        => Log(level: LogLevel.Error, message: message, ex: null, propertyValues: propertyValues);

    /// <summary>Writes an <c>Error</c> log entry containing only the given exception.</summary>
    /// <param name="ex">Exception to attach.</param>
    void Error(Exception? ex)
        => Log(level: LogLevel.Error, message: "", ex: ex, propertyValues: []);

    /// <summary>Writes an <c>Error</c> log entry with an exception, a message template, and optional property values.</summary>
    /// <param name="ex">Exception to attach.</param>
    /// <param name="message">Message template.</param>
    /// <param name="propertyValues">Template property values.</param>
    void Error(Exception? ex, string message, params object?[] propertyValues)
        => Log(level: LogLevel.Error, ex: ex, message: message, propertyValues: propertyValues);

    // ========================================================================
    // Critical overloads
    // ========================================================================

    /// <summary>Writes a <c>Critical</c> log entry with a message template and optional property values.</summary>
    /// <param name="message">Message template.</param>
    /// <param name="propertyValues">Template property values.</param>
    void Critical(string message, params object?[] propertyValues)
        => Log(level: LogLevel.Critical, message: message, ex: null, propertyValues: propertyValues);

    /// <summary>Writes a <c>Critical</c> log entry containing only the given exception.</summary>
    /// <param name="ex">Exception to attach.</param>
    void Critical(Exception? ex)
        => Log(level: LogLevel.Critical, ex: ex, message: "", propertyValues: []);

    /// <summary>Writes a <c>Critical</c> log entry with an exception, a message template, and optional property values.</summary>
    /// <param name="ex">Exception to attach.</param>
    /// <param name="message">Message template.</param>
    /// <param name="propertyValues">Template property values.</param>
    void Critical(Exception? ex, string message, params object?[] propertyValues)
        => Log(level: LogLevel.Critical, ex: ex, message: message, propertyValues: propertyValues);

}