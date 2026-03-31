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

using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Lunarium.Logging;

/// <summary>
/// Entry point for one-time global configuration of the logging system.
/// </summary>
public static class GlobalConfigurator
{
    private static readonly List<Action> _configOperations = new();
    private static bool _isConfiguring = false;

    #region 公共 API: 主方法
    
    /// <summary>
    /// Starts the global configuration flow. Returns a <see cref="ConfigurationBuilder"/> for fluent setup.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if configuration has already been applied, or is already in progress.</exception>
    public static ConfigurationBuilder Configure()
    {
        if (GlobalConfigLock.Configured)
        {
            throw new InvalidOperationException(
                "Global configuration has already been applied. " +
                "Configuration can only be done once during application startup.");
        }
        
        if (_isConfiguring)
        {
            throw new InvalidOperationException(
                "Configuration is already in progress. " +
                "Call Apply() to complete the current configuration.");
        }

        _isConfiguring = true;
        _configOperations.Clear();
        return new ConfigurationBuilder();
    }

    // 应用配置（在 ConfigurationBuilder.Apply() 中调用）
    internal static void ApplyConfiguration()
    {
        if (!_isConfiguring)
        {
            throw new InvalidOperationException(
                "No configuration in progress. Call Configure() first.");
        }

        try
        {
            // 应用默认配置
            ApplyDefaultConfiguration();
            
            // 应用用户自定义配置(会覆盖默认配置)
            foreach (var operation in _configOperations)
            {
                operation.Invoke();
            }
            
            // 锁定配置
            GlobalConfigLock.CompleteConfig();
        }
        finally
        {
            _isConfiguring = false;
            _configOperations.Clear();
        }
    }

    // 仅应用默认配置（在 LoggerBuilder.Build() 中调用）
    internal static void ApplyDefaultIfNotConfigured()
    {
        if (!GlobalConfigLock.Configured)
        {
            ApplyDefaultConfiguration();
            GlobalConfigLock.CompleteConfig();
        }
    }

    #endregion

    #region 内部方法

    internal static void AddConfigOperation(Action operation)
    {
        if (!_isConfiguring)
        {
            throw new InvalidOperationException(
                "Configuration not started. Call Configure() first.");
        }
        _configOperations.Add(operation);
    }

    private static void ApplyDefaultConfiguration()
    {
        // 默认配置：本地时间 + ISO8601(JSON) + 自定义格式(Text)
        LogTimestampConfig.UseLocalTime();
        TimestampFormatConfig.ConfigJsonMode(JsonTimestampMode.ISO8601);
        TimestampFormatConfig.ConfigTextMode(TextTimestampMode.Custom);
        TimestampFormatConfig.ConfigJsonCustomFormat("O");
        TimestampFormatConfig.ConfigTextCustomFormat("yyyy-MM-dd HH:mm:ss.fff");
    }

    #endregion

    /// <summary>
    /// Fluent builder returned by <see cref="Configure"/> for setting global logging options.
    /// </summary>
    public sealed class ConfigurationBuilder
    {
        internal ConfigurationBuilder() { }

        #region 日志时间系统

        /// <summary>Configures the logging system to use UTC time.</summary>
        public ConfigurationBuilder UseUtcTimeZone()
        {
            AddConfigOperation(() => LogTimestampConfig.UseUtcTime());
            return this;
        }

        /// <summary>Configures the logging system to use local time.</summary>
        public ConfigurationBuilder UseLocalTimeZone()
        {
            AddConfigOperation(() => LogTimestampConfig.UseLocalTime());
            return this;
        }

        /// <summary>Configures the logging system to use a custom time zone.</summary>
        /// <param name="timeZone">The time zone to use for log timestamps.</param>
        public ConfigurationBuilder UseCustomTimezone(TimeZoneInfo timeZone)
        {
            ArgumentNullException.ThrowIfNull(timeZone);
            AddConfigOperation(() => LogTimestampConfig.UseCustomTimeZone(timeZone));
            return this;
        }

        #endregion

        #region JSON 时间戳格式

        /// <summary>JSON output uses a Unix timestamp in seconds (always UTC).</summary>
        public ConfigurationBuilder UseJsonUnixTimestamp()
        {
            AddConfigOperation(() => TimestampFormatConfig.ConfigJsonMode(JsonTimestampMode.Unix));
            return this;
        }

        /// <summary>JSON output uses a Unix timestamp in milliseconds (always UTC).</summary>
        public ConfigurationBuilder UseJsonUnixMsTimestamp()
        {
            AddConfigOperation(() => TimestampFormatConfig.ConfigJsonMode(JsonTimestampMode.UnixMs));
            return this;
        }

        /// <summary>JSON output uses ISO 8601 format (default: <c>"O"</c>).</summary>
        public ConfigurationBuilder UseJsonISO8601Timestamp()
        {
            AddConfigOperation(() => TimestampFormatConfig.ConfigJsonMode(JsonTimestampMode.ISO8601));
            return this;
        }

        /// <summary>JSON output uses the specified custom timestamp format.</summary>
        /// <param name="format">A standard or custom date/time format string.</param>
        public ConfigurationBuilder UseJsonCustomTimestamp(string format)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(format);
            AddConfigOperation(() =>
            {
                TimestampFormatConfig.ConfigJsonMode(JsonTimestampMode.Custom);
                TimestampFormatConfig.ConfigJsonCustomFormat(format);
            });
            return this;
        }

        #endregion

        #region 文本时间戳格式

        /// <summary>Text output uses a Unix timestamp in seconds (always UTC).</summary>
        public ConfigurationBuilder UseTextUnixTimestamp()
        {
            AddConfigOperation(() => TimestampFormatConfig.ConfigTextMode(TextTimestampMode.Unix));
            return this;
        }

        /// <summary>Text output uses a Unix timestamp in milliseconds (always UTC).</summary>
        public ConfigurationBuilder UseTextUnixMsTimestamp()
        {
            AddConfigOperation(() => TimestampFormatConfig.ConfigTextMode(TextTimestampMode.UnixMs));
            return this;
        }

        /// <summary>Text output uses ISO 8601 format.</summary>
        public ConfigurationBuilder UseTextISO8601Timestamp()
        {
            AddConfigOperation(() => TimestampFormatConfig.ConfigTextMode(TextTimestampMode.ISO8601));
            return this;
        }

        /// <summary>Text output uses the specified custom timestamp format (default: <c>"yyyy-MM-dd HH:mm:ss.fff"</c>).</summary>
        /// <param name="format">A standard or custom date/time format string.</param>
        public ConfigurationBuilder UseTextCustomTimestamp(string format)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(format);
            AddConfigOperation(() =>
            {
                TimestampFormatConfig.ConfigTextMode(TextTimestampMode.Custom);
                TimestampFormatConfig.ConfigTextCustomFormat(format);
            });
            return this;
        }

        #endregion

        #region 自动解构集合

        /// <summary>Enables automatic destructuring of collection types without requiring the <c>{@...}</c> syntax.</summary>
        public ConfigurationBuilder EnableAutoDestructuring()
        {
            AddConfigOperation(() => DestructuringConfig.EnableAutoDestructuring());
            return this;
        }

        #endregion

        #region JSON 序列化配置

        /// <summary>Enables <c>UnsafeRelaxedJsonEscaping</c> for JSON serialization (default: enabled). Chinese and emoji characters are not over-escaped.</summary>
        public ConfigurationBuilder EnableUnsafeRelaxedJsonEscaping()
        {
            AddConfigOperation(() => JsonSerializationConfig.ConfigUnsafeRelaxedJsonEscaping(true));
            return this;
        }

        /// <summary>Disables <c>UnsafeRelaxedJsonEscaping</c>; non-ASCII characters are escaped as Unicode sequences.</summary>
        public ConfigurationBuilder DisableUnsafeRelaxedJsonEscaping()
        {
            AddConfigOperation(() => JsonSerializationConfig.ConfigUnsafeRelaxedJsonEscaping(false));
            return this;
        }

        /// <summary>Configures JSON serialization to use indented (multi-line) output.</summary>
        public ConfigurationBuilder UseIndentedJson()
        {
            AddConfigOperation(() => JsonSerializationConfig.ConfigWriteIndented(true));
            return this;
        }

        /// <summary>Configures JSON serialization to use compact (single-line) output (default).</summary>
        public ConfigurationBuilder UseCompactJson()
        {
            AddConfigOperation(() => JsonSerializationConfig.ConfigWriteIndented(false));
            return this;
        }

        /// <summary>
        /// Registers a custom <see cref="IJsonTypeInfoResolver"/> for AOT-compatible source-generated contexts.
        /// When registered, <c>{@Object}</c> destructuring uses this resolver and requires no runtime reflection.
        /// </summary>
        /// <param name="resolver">The resolver to register. Use <c>JsonTypeInfoResolver.Combine()</c> to merge multiple contexts.</param>
        public ConfigurationBuilder UseJsonTypeInfoResolver(IJsonTypeInfoResolver resolver)
        {
            ArgumentNullException.ThrowIfNull(resolver);
            AddConfigOperation(() => JsonSerializationConfig.ConfigCustomResolver(resolver));
            return this;
        }

        /// <summary>Convenience overload of <see cref="UseJsonTypeInfoResolver(IJsonTypeInfoResolver)"/> that accepts a <see cref="JsonSerializerContext"/>.</summary>
        /// <param name="context">The source-generated serializer context to register.</param>
        public ConfigurationBuilder UseJsonTypeInfoResolver(JsonSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            AddConfigOperation(() => JsonSerializationConfig.ConfigCustomResolver(context));
            return this;
        }

        #endregion
        
        /// <summary>Applies all queued configuration operations and locks the global configuration.</summary>
        public void Apply()
        {
            ApplyConfiguration();
        }
    }
}