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

namespace Lunarium.Logging.Utils;

// LogUtils 提供对内部时间和格式化配置的静态访问，主要供外部扩展包（如 Http Target）使用
/// <summary>
/// Utility methods that expose the log system's globally configured timestamp and format settings.
/// Primarily intended for use by extension packages (e.g. <c>Lunarium.Logging.Http</c>).
/// </summary>
public static class LogUtils
{
    /// <summary>Returns the current timestamp using the globally configured timezone (local or UTC).</summary>
    public static DateTimeOffset GetLogSystemTimestamp()
    {
        return LogTimestampConfig.GetTimestamp();
    }

    /// <summary>
    /// Returns the current timestamp formatted according to the globally configured text timestamp mode
    /// (<c>Unix</c>, <c>UnixMs</c>, <c>ISO8601</c>, or <c>Custom</c>).
    /// </summary>
    public static string GetLogSystemFormattedTimestamp()
    {
        var formattedTimestamp = TimestampFormatConfig.TextMode switch
        {
            TextTimestampMode.Unix => LogTimestampConfig.GetTimestamp().ToUnixTimeSeconds().ToString(),
            TextTimestampMode.UnixMs => LogTimestampConfig.GetTimestamp().ToUnixTimeMilliseconds().ToString(),
            TextTimestampMode.ISO8601 => $"{LogTimestampConfig.GetTimestamp():O}",
            TextTimestampMode.Custom => LogTimestampConfig.GetTimestamp().ToString(TimestampFormatConfig.TextCustomFormat),
            _ => $"{LogTimestampConfig.GetTimestamp():O}"
        };
        return formattedTimestamp;
    }
}