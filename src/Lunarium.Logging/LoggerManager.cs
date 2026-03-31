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

using System.Collections.Concurrent;

namespace Lunarium.Logging;

// 运行时全局 Logger 注册表；Build() 时自动注册，支持按名称热更新 Sink 配置
// loggerName 在同一进程内应唯一，重复构建同名 Logger 会覆盖注册（后者生效）
/// <summary>
/// Process-wide registry for all active <see cref="ILogger"/> instances.
/// Provides runtime lookups and live configuration updates without restarting the application.
/// </summary>
public static class LoggerManager
{
    private static ConcurrentDictionary<string, Logger> loggers = new();
    
    // 由 LoggerBuilder.Build() 在构建完成后调用，外部不可直接注册
    internal static void RegisterLogger(string loggerName, Logger logger)
    {
        loggers[loggerName] = logger;
    }

    /// <summary>
    /// Updates the configuration of a single named sink within the specified logger at runtime.
    /// </summary>
    /// <param name="loggerName">Name of the target logger.</param>
    /// <param name="sinkName">Name of the sink to update.</param>
    /// <param name="sinkConfig">New configuration to apply. The sink is disabled if <see cref="ISinkConfig.Enabled"/> is <see langword="false"/>.</param>
    public static void UpdateSinkConfig(string loggerName, string sinkName, ISinkConfig sinkConfig)
    {
        if (loggers.TryGetValue(loggerName, out var logger))
        {
            logger.UpdateSinkConfig(sinkName, sinkConfig);
        }
    }

    /// <summary>
    /// Updates all sink configurations of the specified logger from a <see cref="LoggerConfig"/> snapshot.
    /// Sinks not referenced in the config are disabled.
    /// </summary>
    /// <param name="loggerConfigs">Config snapshot keyed by logger name.</param>
    public static void UpdateLoggerConfig(LoggerConfig loggerConfigs)
    {
        if (loggers.TryGetValue(loggerConfigs.LoggerName, out var logger))
        {
            logger.UpdateLoggerConfig(loggerConfigs);
        }
    }
    
    /// <summary>
    /// Applies a batch of <see cref="LoggerConfig"/> snapshots to their respective loggers.
    /// Typically called by the hot-reload handler when <c>appsettings.json</c> changes.
    /// </summary>
    /// <param name="loggerConfigs">Collection of config snapshots to apply.</param>
    public static void UpdateAllLoggerConfig(IReadOnlyList<LoggerConfig> loggerConfigs)
    {
        foreach (var loggerConfig in loggerConfigs)
        {
            UpdateLoggerConfig(loggerConfig);
        }
    }

    /// <summary>Returns the names of all currently registered loggers.</summary>
    public static IReadOnlyList<string> GetLoggerList()
    {
        return loggers.Keys.ToList();
    }
    
}