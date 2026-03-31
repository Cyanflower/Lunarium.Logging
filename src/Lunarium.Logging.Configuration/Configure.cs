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

using Lunarium.Logging;
using Lunarium.Logging.Config;

namespace Lunarium.Logging.Configuration;

// GlobalConfigApplier 将 GlobalConfig POCO 映射到 GlobalConfigurator.ConfigurationBuilder 的流式 API
// Custom 模式下如果未提供自定义格式字符串，自动回退到 ISO8601
internal static class GlobalConfigApplier
{
    internal static void Apply(GlobalConfig config, GlobalConfigurator.ConfigurationBuilder builder)
    {
        if (config.TimeZone is not null)
        {
            if (config.TimeZone.Equals("Local", StringComparison.OrdinalIgnoreCase))
                builder.UseLocalTimeZone();
            else if (config.TimeZone.Equals("Utc", StringComparison.OrdinalIgnoreCase))
                builder.UseUtcTimeZone();
            else
            {
                // 自定义时区 ID（IANA 或 Windows 格式）→ TimeZoneInfo
                var tz = TimeZoneInfo.FindSystemTimeZoneById(config.TimeZone);
                builder.UseCustomTimezone(tz);
            }
        }

        if (config.TextTimestampMode.HasValue)
        {
            switch (config.TextTimestampMode.Value)
            {
                case TimestampMode.Unix:   builder.UseTextUnixTimestamp();   break;
                case TimestampMode.UnixMs: builder.UseTextUnixMsTimestamp(); break;
                case TimestampMode.ISO8601: builder.UseTextISO8601Timestamp(); break;
                case TimestampMode.Custom:
                    if (config.TextCustomTimestampFormat is not null)
                        builder.UseTextCustomTimestamp(config.TextCustomTimestampFormat);
                    else
                        builder.UseTextISO8601Timestamp(); // 无自定义格式时回退 ISO8601
                    break;
            }
        }

        if (config.JsonTimestampMode.HasValue)
        {
            switch (config.JsonTimestampMode.Value)
            {
                case TimestampMode.Unix:   builder.UseJsonUnixTimestamp();   break;
                case TimestampMode.UnixMs: builder.UseJsonUnixMsTimestamp(); break;
                case TimestampMode.ISO8601: builder.UseJsonISO8601Timestamp(); break;
                case TimestampMode.Custom:
                    if (config.JsonCustomTimestampFormat is not null)
                        builder.UseJsonCustomTimestamp(config.JsonCustomTimestampFormat);
                    else
                        builder.UseJsonISO8601Timestamp(); // 无自定义格式时回退 ISO8601
                    break;
            }
        }

        if (config.EnableUnsafeRelaxedJsonEscaping.HasValue)
        {
            if (config.EnableUnsafeRelaxedJsonEscaping.Value)
                builder.EnableUnsafeRelaxedJsonEscaping();
            else
                builder.DisableUnsafeRelaxedJsonEscaping();
        }

        if (config.WriteIndentedJson.HasValue)
        {
            if (config.WriteIndentedJson.Value)
                builder.UseIndentedJson();
            else
                builder.UseCompactJson();
        }

        if (config.EnableAutoDestructuring == true)
            builder.EnableAutoDestructuring();
    }
}

// LoggerConfigApplier 将 LoggerConfig POCO 映射到 LoggerBuilder
// FileSinkConfig 缺少 LogFilePath 时跳过，防止构建非法路径的 FileTarget
internal static class LoggerConfigApplier
{
    internal static ILogger Build(LoggerConfig loggerConfig)
    {
        var builder = new LoggerBuilder().SetLoggerName(loggerConfig.LoggerName);

        foreach (var (sinkName, consoleSinkConfig) in loggerConfig.ConsoleSinks)
            builder.AddSink(consoleSinkConfig, sinkName);

        foreach (var (sinkName, fileSinkConfig) in loggerConfig.FileSinks)
        {
            // LogFilePath 为空不构建：Configure 时路径未配置是合法状态，不应报错
            if (string.IsNullOrEmpty(fileSinkConfig.LogFilePath))
                continue;
            builder.AddSink(fileSinkConfig, sinkName);
        }

        return builder.Build();
    }
}
