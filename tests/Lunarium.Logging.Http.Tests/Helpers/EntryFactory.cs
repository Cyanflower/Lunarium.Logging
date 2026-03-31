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

namespace Lunarium.Logging.Http.Tests.Helpers;

/// <summary>
/// 构造已完成模板解析的 LogEntry，供所有测试复用。
/// </summary>
internal static class EntryFactory
{
    public static readonly DateTimeOffset DefaultTimestamp =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 创建一个已调用 ParseMessage() 的 LogEntry。
    /// </summary>
    public static LogEntry Make(
        string message = "hello",
        LogLevel level = LogLevel.Info,
        string loggerName = "TestLogger",
        string context = "",
        object?[]? props = null,
        Exception? ex = null,
        string scope = "",
        DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DefaultTimestamp;
        var entry = new LogEntry(
            loggerName: loggerName,
            loggerNameBytes: Encoding.UTF8.GetBytes(loggerName),
            timestamp: ts,
            logLevel: level,
            message: message,
            properties: props ?? [],
            context: context,
            contextBytes: Encoding.UTF8.GetBytes(context),
            scope: scope,
            messageTemplate: LogParser.EmptyMessageTemplate,
            exception: ex);
        entry.ParseMessage();
        return entry;
    }

    /// <summary>创建一个批次（多条相同结构的条目）。</summary>
    public static List<LogEntry> MakeBatch(int count, string message = "hello") =>
        Enumerable.Range(0, count).Select(_ => Make(message)).ToList();
}
