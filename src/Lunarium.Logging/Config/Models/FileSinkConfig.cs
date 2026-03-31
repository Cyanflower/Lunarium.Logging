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
/// Complete configuration for a file sink, covering all rotation modes:
/// size-based, time-based (daily), or both combined.
/// </summary>
/// <remarks>
/// Corresponds directly to the <c>FileTarget</c> constructor parameters.
/// Omitting <see cref="MaxFileSizeMB"/> disables size-based rotation;
/// leaving <see cref="RotateOnNewDay"/> as <see langword="false"/> disables daily rotation.
/// </remarks>
public sealed record FileSinkConfig : ISinkConfig
{
    /// <summary>Whether this sink is active. Defaults to <see langword="true"/>.</summary>
    public bool Enabled { get; init; } = true;

    // null 表示不覆盖 Target 的默认行为
    /// <summary>
    /// Output format override. <see langword="true"/> forces JSON output, <see langword="false"/> forces plain text.
    /// <see langword="null"/> (default) leaves the target's own default in effect.
    /// </summary>
    public bool? ToJson { get; init; } = null;

    /// <summary>
    /// Color output override. <see langword="null"/> (default) leaves the target's own default in effect.
    /// </summary>
    public bool? IsColor { get; init; } = null;

    /// <summary>
    /// Controls which fields appear in text/color output.
    /// <see langword="null"/> (default) uses the target's own defaults (all fields visible).
    /// </summary>
    public TextOutputIncludeConfig? TextOutput { get; init; } = null;

    /// <summary>
    /// Base path for the log file, e.g. <c>"Logs/app.log"</c>.
    /// An empty string causes <c>LoggerConfigApplier</c> to skip this sink during config-driven construction.
    /// </summary>
    public string LogFilePath { get; init; } = string.Empty;

    /// <summary>
    /// Maximum size of a single log file in megabytes. Triggers rotation when exceeded.
    /// A value of <c>0</c> or less disables size-based rotation.
    /// </summary>
    public double MaxFileSizeMB { get; init; } = 0;

    /// <summary>Whether to rotate the log file at the start of each new day.</summary>
    public bool RotateOnNewDay { get; init; } = false;

    // 至少要启用一种轮转策略（MaxFileSizeMB > 0 或 RotateOnNewDay = true），否则 MaxFile 限制无意义
    /// <summary>
    /// Maximum number of rotated log files to retain. <c>0</c> or less means unlimited.
    /// </summary>
    /// <remarks>
    /// At least one rotation strategy must be enabled when this value is greater than zero.
    /// </remarks>
    public int MaxFile { get; init; } = 0;

    /// <summary>Filter rules applied before passing entries to the target.</summary>
    public FilterConfig? FilterConfig { get; init; }

    /// <summary>Creates a <see cref="FileTarget"/> configured from this record's properties.</summary>
    public ILogTarget CreateTarget() =>
        new FileTarget(
            logFilePath: LogFilePath,
            maxFileSizeMB: MaxFileSizeMB,
            rotateOnNewDay: RotateOnNewDay,
            maxFile: MaxFile,
            toJson: ToJson,
            isColor: IsColor,
            textOutputIncludeConfig: TextOutput);
}
