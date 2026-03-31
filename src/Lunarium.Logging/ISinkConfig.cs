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
/// Represents a complete sink configuration combining sink-specific parameters
/// with a common <see cref="FilterConfig"/> and output-format overrides.
/// Implement this interface to create first-party or third-party sink configs
/// that can be registered via <see cref="LoggerBuilder.AddSink(ISinkConfig,string?)"/>.
/// </summary>
public interface ISinkConfig
{
    /// <summary>Whether this sink is active. Defaults to <see langword="true"/>.</summary>
    public bool Enabled { get; init; }

    // null 表示不覆盖 Target 的默认行为（与 false 语义不同）
    /// <summary>
    /// Output format override. <see langword="true"/> forces JSON output, <see langword="false"/> forces plain text.
    /// <see langword="null"/> (default) leaves the target's own default in effect.
    /// </summary>
    public bool? ToJson { get; init; }

    // 仅对实现了 ITextTarget （如 ConsoleTarget）的 Target 生效；文件 Target 默认不启用颜色
    /// <summary>
    /// Color output override. <see langword="true"/> enables ANSI color, <see langword="false"/> disables it.
    /// <see langword="null"/> (default) leaves the target's own default in effect.
    /// Only applies to targets implementing <see cref="ITextTarget"/> (e.g. <see cref="ConsoleTarget"/>).
    /// </summary>
    public bool? IsColor { get; init; }

    // 仅对实现了 ITextTarget 的 Target 生效；不指定时保持 Target 自身默认值
    /// <summary>
    /// Field visibility config for text/color writers (timestamp, level, logger name, context).
    /// <see langword="null"/> leaves the target's own default in effect.
    /// </summary>
    public TextOutputIncludeConfig? TextOutput { get; init; }

    /// <summary>
    /// Common filter settings (level range, context include/exclude rules).
    /// <see langword="null"/> applies the default pass-all filter.
    /// </summary>
    FilterConfig? FilterConfig { get; }

    /// <summary>
    /// Creates the <see cref="ILogTarget"/> instance described by this configuration.
    /// Called by <see cref="LoggerBuilder.AddSink(ISinkConfig,string?)"/> during logger construction.
    /// </summary>
    ILogTarget CreateTarget();
}
