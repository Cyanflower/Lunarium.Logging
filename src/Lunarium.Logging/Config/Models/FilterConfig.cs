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

namespace Lunarium.Logging.Config.Models;

/// <summary>
/// Encapsulates the filter settings for a single log sink, controlling which
/// log entries are passed through to the output target.
/// </summary>
public record FilterConfig
{
    // Include 列表为 null 或空时视为通配，所有上下文均通过
    /// <summary>
    /// Context prefix include list. Only entries whose <c>Context</c> starts with one of
    /// these prefixes will be emitted. An empty or <see langword="null"/> list matches all entries.
    /// </summary>
    public List<string>? ContextFilterIncludes { get; init; }

    /// <summary>
    /// Context prefix exclude list. Entries whose <c>Context</c> matches any of these
    /// prefixes will be suppressed, evaluated after include rules.
    /// </summary>
    public List<string>? ContextFilterExcludes { get; init; }

    /// <summary>Whether context prefix matching is case-insensitive. Defaults to <see langword="false"/>.</summary>
    public bool IgnoreFilterCase { get; init; } = false;

    // 由 IgnoreFilterCase 自动派生，不对外暴露，禁止直接修改
    internal StringComparison ComparisonType =>
        IgnoreFilterCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>The minimum log level this sink will emit. Defaults to <see cref="LogLevel.Info"/>.</summary>
    public LogLevel LogMinLevel { get; init; } = LogLevel.Info;

    /// <summary>The maximum log level this sink will emit. Defaults to <see cref="LogLevel.Critical"/>.</summary>
    public LogLevel LogMaxLevel { get; init; } = LogLevel.Critical;
}
