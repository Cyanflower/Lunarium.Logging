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

namespace Lunarium.Logging.Config.Configurator;

// JSON 输出使用的时间戳格式模式，默认 ISO8601（对齐大多数日志平台的期望格式）
internal enum JsonTimestampMode
{
    Unix,     // 秒级 Unix 时间戳
    UnixMs,   // 毫秒级 Unix 时间戳
    ISO8601,  // ISO 8601（默认，如 "2026-01-01T12:00:00.000+08:00"）
    Custom    // 自定义格式字符串，由 JsonCustomFormat 指定
}

// 纯文本输出使用的时间戳格式模式，默认 Custom（使用 TextCustomFormat 中的格式字符串）
internal enum TextTimestampMode
{
    Unix,    // 秒级 Unix 时间戳
    UnixMs,  // 毫秒级 Unix 时间戳
    ISO8601, // ISO 8601
    Custom   // 自定义格式字符串（默认），初始值为 "s"
}

// 全局时间戳格式配置，JSON 和文本输出格式分别独立配置
// 通过 GlobalConfigurator 在 Build() 前一次性写入
internal static class TimestampFormatConfig
{
    internal static JsonTimestampMode JsonMode { get; private set; } = JsonTimestampMode.ISO8601;
    internal static TextTimestampMode TextMode { get; private set; } = TextTimestampMode.Custom;

    // "O" 是 DateTimeOffset 的 round-trip 格式，包含时区偏移
    internal static string JsonCustomFormat { get; private set; } = "O";

    // "s" 为 ISO 8601 可排序格式（yyyy-MM-ddTHH:mm:ss），不含毫秒和时区
    internal static string TextCustomFormat { get; private set; } = "s";

    internal static void ConfigJsonMode(JsonTimestampMode jsonTimestampMode)
    {
        JsonMode = jsonTimestampMode;
    }

    internal static void ConfigTextMode(TextTimestampMode textTimestampMode)
    {
        TextMode = textTimestampMode;
    }

    internal static void ConfigJsonCustomFormat(string format)
    {
        JsonCustomFormat = format;
    }

    internal static void ConfigTextCustomFormat(string format)
    {
        TextCustomFormat = format;
    }
}
