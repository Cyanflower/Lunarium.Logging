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

namespace Lunarium.Logging.Config.GlobalConfig;

// 时间戳时区模式，决定 GetTimestamp() 返回哪个时区的时间
internal enum LogTimestampMode
{
    Local,   // 系统本地时区（默认）
    Utc,     // UTC
    Custom   // 自定义时区，需配合 CustomTimeZone 使用
}

// 全局时间戳时区配置，所有 Logger 共享同一时区设置
// 通过 GlobalConfigurator 在 Build() 前一次性写入，之后只读
internal static class LogTimestampConfig
{
    internal static LogTimestampMode Mode { get; private set; } = LogTimestampMode.Local;

    // 仅在 Mode == Custom 时生效；默认值为 Utc 是占位，不会被 Local 或 Utc 模式实际读取
    internal static TimeZoneInfo CustomTimeZone { get; private set; } = TimeZoneInfo.Utc;

    internal static void UseLocalTime()
    {
        Mode = LogTimestampMode.Local;
    }

    internal static void UseUtcTime()
    {
        Mode = LogTimestampMode.Utc;
        CustomTimeZone = TimeZoneInfo.Utc;
    }

    // 调用方（GlobalConfigurator）负责在调用前验证 timeZone 非 null
    internal static void UseCustomTimeZone(TimeZoneInfo timeZone)
    {
        if (timeZone == null)
            throw new ArgumentNullException(nameof(timeZone));

        Mode = LogTimestampMode.Custom;
        CustomTimeZone = timeZone;
    }

    internal static DateTimeOffset GetTimestamp()
    {
        return Mode switch
        {
            LogTimestampMode.Local => DateTimeOffset.Now,
            LogTimestampMode.Utc => DateTimeOffset.UtcNow,
            LogTimestampMode.Custom => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, CustomTimeZone),
            _ => DateTimeOffset.UtcNow
        };
    }
}
