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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Lunarium.Logging.Configuration;

/// <summary>
/// Provides <see cref="IConfiguration"/> extension methods for building
/// <see cref="ILogger"/> instances from a Lunarium configuration section.
/// </summary>
public static class LunariumConfigurationExtensions
{
    /// <summary>
    /// Reads all logger configs from the specified configuration section and returns the
    /// corresponding <see cref="ILogger"/> instances in the same order as in the config.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="sectionKey">Configuration section key; defaults to <c>"LunariumLogging"</c>.</param>
    /// <param name="watchForChanges">
    /// Whether to watch for configuration changes and hot-reload sink configs.
    /// <see cref="GlobalConfig"/> changes are ignored after the first application (one-shot).
    /// </param>
    /// <returns>A read-only list of built loggers in config order.</returns>
    public static IReadOnlyList<ILogger> BuildLunariumLoggers(
        this IConfiguration configuration,
        string sectionKey = "LunariumLogging",
        bool watchForChanges = false)
    {
        var loggingConfig = BindLoggingConfig(configuration, sectionKey);

        ApplyGlobalConfigIfNeeded(loggingConfig.GlobalConfig);

        var loggers = loggingConfig.LoggerConfigs
            .Select(LoggerConfigApplier.Build)
            .ToList();

        if (watchForChanges)
            RegisterChangeCallback(configuration, sectionKey);

        return loggers;
    }

    /// <summary>
    /// Finds and builds the <see cref="ILogger"/> whose <c>LoggerName</c> matches
    /// <paramref name="loggerName"/> in the specified configuration section.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="loggerName">The logger name to search for in <c>LoggerConfigs[]</c>.</param>
    /// <param name="sectionKey">Configuration section key; defaults to <c>"LunariumLogging"</c>.</param>
    /// <param name="watchForChanges">Whether to hot-reload this logger's sink configs on config changes.</param>
    /// <returns>The built <see cref="ILogger"/>, or <see langword="null"/> if the name was not found.</returns>
    public static ILogger? BuildLunariumLogger(
        this IConfiguration configuration,
        string loggerName,
        string sectionKey = "LunariumLogging",
        bool watchForChanges = false)
    {
        var loggingConfig = BindLoggingConfig(configuration, sectionKey);

        var loggerConfig = loggingConfig.LoggerConfigs
            .FirstOrDefault(lc => lc.LoggerName == loggerName);

        if (loggerConfig is null)
            return null;

        ApplyGlobalConfigIfNeeded(loggingConfig.GlobalConfig);

        var logger = LoggerConfigApplier.Build(loggerConfig);

        if (watchForChanges)
            RegisterChangeCallback(configuration, sectionKey);

        return logger;
    }

    // ── 内部辅助方法 ──────────────────────────────────────────────────────────

    // IConfiguration.GetSection().Get<T>() 在节点不存在时返回 null，回退到空配置对象
    private static LoggingConfig BindLoggingConfig(IConfiguration configuration, string sectionKey)
    {
        return configuration.GetSection(sectionKey).Get<LoggingConfig>() ?? new LoggingConfig();
    }

    // GlobalConfig 是一次性设计：重复调用 GlobalConfigurator.Configure() 会抛 InvalidOperationException
    // 捕获并静默忽略，确保多个 Build 调用互不干扰，首次生效后后续调用无副作用
    private static void ApplyGlobalConfigIfNeeded(GlobalConfig? globalConfig)
    {
        if (globalConfig is null)
            return;

        try
        {
            var cfgBuilder = GlobalConfigurator.Configure();
            GlobalConfigApplier.Apply(globalConfig, cfgBuilder);
            cfgBuilder.Apply();
        }
        catch (InvalidOperationException)
        {
            // GlobalConfigurator 已被配置过（一次性设计），跳过全局配置应用
        }
    }

    // ChangeToken.OnChange 监听 配置文件重载事件（如 appsettings.json 变更）
    // 回调中只更新 LoggerConfigs，不重新应用 GlobalConfig（一次性设计）
    private static void RegisterChangeCallback(IConfiguration configuration, string sectionKey)
    {
        ChangeToken.OnChange(
            () => configuration.GetReloadToken(),
            () =>
            {
                var updated = BindLoggingConfig(configuration, sectionKey);
                if (updated.LoggerConfigs.Count > 0)
                    LoggerManager.UpdateAllLoggerConfig(updated.LoggerConfigs);
            });
    }
}
