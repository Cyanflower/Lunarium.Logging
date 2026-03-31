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

// 供 appsettings.json 反序列化使用的顶层配置容器
/// <summary>
/// Top-level configuration container that maps to the <c>Lunarium.Logging</c> section
/// in <c>appsettings.json</c>. Used by <c>IConfiguration.BuildLunariumLoggers()</c>.
/// </summary>
public class LoggingConfig
{
    /// <summary>Optional global settings applied before any logger is built.</summary>
    public GlobalConfig? GlobalConfig { get; set; }

    /// <summary>List of logger configurations to build.</summary>
    public List<LoggerConfig> LoggerConfigs { get; set; } = new();
}

/// <summary>
/// Configuration for a single named logger instance, including its sink definitions.
/// </summary>
public class LoggerConfig
{
    // 默认空字符串而非 required，确保 Configuration Binder 不会因缺字段而抛出异常
    /// <summary>
    /// The unique name identifying this logger instance.
    /// An empty string is permitted for compatibility with the configuration binder.
    /// </summary>
    public string LoggerName { get; set; } = string.Empty;

    /// <summary>Named console sinks for this logger. Keys are sink names used at runtime.</summary>
    public Dictionary<string, ConsoleSinkConfig> ConsoleSinks { get; set; } = new();

    /// <summary>Named file sinks for this logger. Keys are sink names used at runtime.</summary>
    public Dictionary<string, FileSinkConfig> FileSinks { get; set; } = new();
}
