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

// 全局配置 POCO，与 appsettings.json 节点一对一映射
// 所有属性必须可空：null 表示不修改对应内部默认值，GlobalConfigApplier 会跳过 null 属性
/// <summary>
/// Global configuration model that maps directly to the <c>Lunarium.Logging</c> section
/// in <c>appsettings.json</c>. All properties are nullable; a <see langword="null"/> value
/// means "leave the library default unchanged."
/// </summary>
public sealed class GlobalConfig
{
    // ── 时间系统 ──────────────────────────────────────────────────────────

    /// <summary>
    /// Timezone for log timestamps. Accepted values: <c>"Local"</c>, <c>"Utc"</c>,
    /// or a platform timezone ID such as <c>"Asia/Shanghai"</c> or <c>"China Standard Time"</c>.
    /// </summary>
    public string? TimeZone { get; set; }

    // ── 纯文本格式化 ──────────────────────────────────────────────────────

    /// <summary>
    /// Timestamp format mode for plain-text output.
    /// Accepted values: <see cref="TimestampMode.Unix"/>, <see cref="TimestampMode.UnixMs"/>,
    /// <see cref="TimestampMode.ISO8601"/>, <see cref="TimestampMode.Custom"/>.
    /// </summary>
    public TimestampMode? TextTimestampMode { get; set; }

    /// <summary>
    /// Custom timestamp format string for plain-text output.
    /// Only used when <see cref="TextTimestampMode"/> is <see cref="TimestampMode.Custom"/>.
    /// </summary>
    public string? TextCustomTimestampFormat { get; set; }

    // ── JSON 格式化 ───────────────────────────────────────────────────────

    /// <summary>
    /// Timestamp format mode for JSON output.
    /// Accepted values: <see cref="TimestampMode.Unix"/>, <see cref="TimestampMode.UnixMs"/>,
    /// <see cref="TimestampMode.ISO8601"/>, <see cref="TimestampMode.Custom"/>.
    /// </summary>
    public TimestampMode? JsonTimestampMode { get; set; }

    /// <summary>
    /// Custom timestamp format string for JSON output.
    /// Only used when <see cref="JsonTimestampMode"/> is <see cref="TimestampMode.Custom"/>.
    /// </summary>
    public string? JsonCustomTimestampFormat { get; set; }

    /// <summary>
    /// When <see langword="true"/>, enables <c>UnsafeRelaxedJsonEscaping</c>, allowing
    /// characters such as Chinese and emoji to pass through unescaped in JSON output.
    /// </summary>
    public bool? EnableUnsafeRelaxedJsonEscaping { get; set; }

    /// <summary>
    /// When <see langword="true"/>, JSON output is pretty-printed (indented).
    /// Defaults to single-line compact output.
    /// </summary>
    public bool? WriteIndentedJson { get; set; }

    // ── 对象解构行为 ──────────────────────────────────────────────────────

    /// <summary>
    /// When <see langword="true"/>, collection-type property values are automatically
    /// destructured to JSON arrays when logged with the default <c>{value}</c> syntax.
    /// </summary>
    public bool? EnableAutoDestructuring { get; set; }
}

// 供 Microsoft.Extensions.Configuration Binder 反序列化使用
// Binder 会将 JSON 字符串（如 "ISO8601"）自动映射到此枚举，无需手动解析
/// <summary>
/// Timestamp format mode used in <see cref="GlobalConfig"/> for JSON configuration binding.
/// </summary>
public enum TimestampMode
{
    /// <summary>Unix timestamp in seconds.</summary>
    Unix,
    /// <summary>Unix timestamp in milliseconds.</summary>
    UnixMs,
    /// <summary>ISO 8601 extended format (e.g. <c>2026-01-01T12:00:00.000+08:00</c>).</summary>
    ISO8601,
    /// <summary>Custom format string specified by the corresponding <c>CustomTimestampFormat</c> property.</summary>
    Custom
}
