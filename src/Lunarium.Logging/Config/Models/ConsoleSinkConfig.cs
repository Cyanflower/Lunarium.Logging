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

using Lunarium.Logging.Target;

namespace Lunarium.Logging;

/// <summary>
/// Complete configuration for a console sink.
/// Implements <see cref="ISinkConfig"/> as a unified entry point wrapping <see cref="FilterConfig"/>.
/// </summary>
public sealed record ConsoleSinkConfig : ISinkConfig
{
    /// <summary>Whether this sink is active. Defaults to <see langword="true"/>.</summary>
    public bool Enabled { get; init; } = true;

    // null 表示不覆盖 Target 的默认行为，由 Target 自行决定
    /// <summary>
    /// Output format override. <see langword="true"/> forces JSON output, <see langword="false"/> forces plain text.
    /// <see langword="null"/> (default) leaves the target's own default in effect.
    /// </summary>
    public bool? ToJson { get; init; } = null;

    /// <summary>
    /// Color output override. Only applies to targets that support color rendering (e.g. <c>ConsoleTarget</c>).
    /// <see langword="null"/> (default) leaves the target's own default in effect.
    /// </summary>
    public bool? IsColor { get; init; } = null;

    /// <summary>
    /// Controls which fields appear in text/color output.
    /// <see langword="null"/> (default) uses the target's own defaults (all fields visible).
    /// </summary>
    public TextOutputIncludeConfig? TextOutput { get; init; } = null;

    /// <summary>Filter rules applied before passing entries to the target.</summary>
    public FilterConfig? FilterConfig { get; init; }

    /// <summary>Creates a <see cref="ConsoleTarget"/> configured from this record's properties.</summary>
    public ILogTarget CreateTarget() => new ConsoleTarget(toJson: ToJson ?? false, isColor: IsColor ?? true, textOutputIncludeConfig: TextOutput);
}
