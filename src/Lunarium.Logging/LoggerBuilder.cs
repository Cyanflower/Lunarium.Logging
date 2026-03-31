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
/// Fluent builder for constructing <see cref="ILogger"/> instances.
/// Add sinks via <c>AddSink</c> / <c>AddConsoleSink</c> / etc., then call <see cref="Build"/> to create the logger.
/// Each <see cref="Build"/> call produces an independent logger and registers it with <see cref="LoggerManager"/>.
/// </summary>
public sealed class LoggerBuilder
{
    // 持有所有已配置的日志输出目标
    private List<Sink> _sinks = new();
    // loggerName 应在进程内唯一；重复调用 Build() 会以新实例覆盖 LoggerManager 中的注册
    private string _loggerName = "LoggerName:Undefined";

    /// <summary>Initializes a new <see cref="LoggerBuilder"/> with no sinks configured.</summary>
    public LoggerBuilder() { }

    /// <summary>Sets the logger name used for display and runtime management lookups.</summary>
    /// <param name="loggerName">Logger name. Should be unique within the process.</param>
    /// <returns>The current builder for chaining.</returns>
    public LoggerBuilder SetLoggerName(string loggerName)
    {
        _loggerName = loggerName;
        return this;
    }

    /// <summary>
    /// Adds a sink backed by an existing <see cref="ILogTarget"/> instance with an optional filter and name.
    /// </summary>
    /// <param name="target">The log target to write to.</param>
    /// <param name="cfg">Optional filter config. <see langword="null"/> applies a pass-all filter.</param>
    /// <param name="name">Optional sink name for runtime hot-update via <see cref="LoggerManager"/>.</param>
    /// <returns>The current builder for chaining.</returns>
    public LoggerBuilder AddSink(ILogTarget target, FilterConfig? cfg = null, string? name = null)
    {
        _sinks.Add(new Sink(target, cfg, name));
        return this;
    }

    /// <summary>
    /// Adds a sink from a complete <see cref="ISinkConfig"/> object.
    /// Calls <see cref="ISinkConfig.CreateTarget"/> to create the target and reads
    /// <see cref="ISinkConfig.FilterConfig"/> for filter settings.
    /// </summary>
    /// <param name="sinkConfig">Sink configuration object.</param>
    /// <param name="name">Optional sink name for runtime hot-update via <see cref="LoggerManager"/>.</param>
    /// <returns>The current builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sinkConfig"/> is <see langword="null"/>.</exception>
    public LoggerBuilder AddSink(ISinkConfig sinkConfig, string? name = null)
    {
        if (sinkConfig is null)
        {
            throw new ArgumentNullException(nameof(sinkConfig));
        }
        _sinks.Add(new Sink(sinkConfig.CreateTarget(), sinkConfig.FilterConfig, name));

        return this;
    }

    /// <summary>
    /// Builds and returns an <see cref="ILogger"/> from the current configuration.
    /// Also applies global defaults if <see cref="GlobalConfigurator"/> has not been called yet,
    /// and registers the logger with <see cref="LoggerManager"/> for runtime management.
    /// </summary>
    /// <returns>The fully configured logger instance.</returns>
    public ILogger Build()
    {
        // 用户未调用 GlobalConfigurator.Configure() 时，自动应用默认全局配置（本地时区、ISO8601 等）
        GlobalConfigurator.ApplyDefaultIfNotConfigured();
        try
        {
            var logger = new Logger(_sinks, _loggerName);
            LoggerManager.RegisterLogger(_loggerName, logger);
            return logger;
        }
        catch (Exception ex)
        {
            InternalLogger.Error(ex, "LoggerBuilder: Build Failed");
            throw;
        }
    }
}