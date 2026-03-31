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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lunarium.Logging;
using Lunarium.Logging.Configuration;
using Lunarium.Logging.Models;

namespace Lunarium.Logging.Extensions;

// LunariumLoggerProvider 不接管 _lunariumLogger 的生命周期：
// 参数中的 logger 由外部创建，应由外部或 DI 容器销毁
/// <summary>
/// <see cref="ILoggerProvider"/> implementation that bridges
/// Microsoft.Extensions.Logging to a <see cref="Lunarium.Logging.ILogger"/> instance.
/// Each category name is mapped to a <see cref="LunariumMsLoggerAdapter"/> backed by
/// <c>ForContext(string)</c>.
/// </summary>
public class LunariumLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly Lunarium.Logging.ILogger _lunariumLogger;
    private IExternalScopeProvider _scopeProvider = NullScopeProvider.Instance;

    /// <summary>Initializes the provider with an externally managed <see cref="Lunarium.Logging.ILogger"/>.</summary>
    public LunariumLoggerProvider(Lunarium.Logging.ILogger lunariumLogger)
    {
        _lunariumLogger = lunariumLogger;
    }

    /// <inheritdoc/>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    /// <summary>Creates a <see cref="LunariumMsLoggerAdapter"/> scoped to <paramref name="categoryName"/>.</summary>
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        return new LunariumMsLoggerAdapter(_lunariumLogger.ForContext(categoryName), _scopeProvider);
    }

    /// <summary>
    /// Does not dispose the underlying logger; its lifecycle is managed by the caller.
    /// </summary>
    public void Dispose()
    {
        // -------------------------------------------------------
        // LunariumLogger 的生命周期由外部管理
        // 由于“谁创建、谁管理生命周期”原则，这里的 Dispose 不做任何操作
        // -------------------------------------------------------
    }
}

// MEL 适配器内部类，每个类别名称对应一个实例（由 CreateLogger 创建）
// scope 链从 IExternalScopeProvider 收集后拼接为单一字符串写入 scope 参数
/// <summary>
/// Internal per-category adapter that translates MEL log calls to
/// <see cref="Lunarium.Logging.ILogger.Log"/> calls.
/// </summary>
internal sealed class LunariumMsLoggerAdapter : Microsoft.Extensions.Logging.ILogger
{
    private readonly Lunarium.Logging.ILogger _lunariumLogger;
    private readonly IExternalScopeProvider _scopeProvider;

    internal LunariumMsLoggerAdapter(Lunarium.Logging.ILogger lunariumLogger, IExternalScopeProvider scopeProvider)
    {
        _lunariumLogger = lunariumLogger;
        _scopeProvider = scopeProvider;
    }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _scopeProvider.Push(state);

    /// <inheritdoc/>
    /// <remarks>Always returns <see langword="true"/>; actual filtering is delegated to the Sink layer.</remarks>
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        // 实际过滤在 Sink 层执行，此处保守地返回 true
        return true;
    }

    /// <inheritdoc/>
    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        var level = ConvertLogLevel(logLevel);

        // 收集异步流中的 scope 链
        var scopeParts = new List<string>();
        _scopeProvider.ForEachScope((scope, list) =>
        {
            var str = scope?.ToString();
            if (!string.IsNullOrEmpty(str)) list.Add(str);
        }, scopeParts);

        // eventId 有意义时追加到末尾
        if (!string.IsNullOrEmpty(eventId.Name))
            scopeParts.Add(eventId.Name);
        else if (eventId.Id != 0)
            scopeParts.Add(eventId.Id.ToString());

        var scope = string.Join(".", scopeParts);

        _lunariumLogger.Log(level: level, ex: exception, message: message, scope: scope);
    }

    // MEL 有 Trace 级别，映射到 Lunarium 的 Debug；其他级别一一对应
    private static Lunarium.Logging.Models.LogLevel ConvertLogLevel(Microsoft.Extensions.Logging.LogLevel msLogLevel)
    {
        return msLogLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => Lunarium.Logging.Models.LogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Debug => Lunarium.Logging.Models.LogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => Lunarium.Logging.Models.LogLevel.Info,
            Microsoft.Extensions.Logging.LogLevel.Warning => Lunarium.Logging.Models.LogLevel.Warning,
            Microsoft.Extensions.Logging.LogLevel.Error => Lunarium.Logging.Models.LogLevel.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => Lunarium.Logging.Models.LogLevel.Critical,
            _ => Lunarium.Logging.Models.LogLevel.Info
        };
    }
}

// 所有重载均委托到 ILoggingBuilder 手动版本，避免重复逻辑
/// <summary>
/// Extension methods for integrating LunariumLogger with the
/// Microsoft.Extensions.Logging pipeline.
/// </summary>
public static class LunariumLoggerExtensions
{
    /// <summary>
    /// Registers an externally constructed <see cref="Lunarium.Logging.ILogger"/> as the sole
    /// MEL log provider. Clears any previously registered providers.
    /// </summary>
    /// <remarks>
    /// Use this overload when you manage the logger's lifecycle yourself
    /// (e.g., testing or console apps). For hosted apps, prefer
    /// <see cref="UseLunariumLog(ILoggingBuilder,Action{LoggerBuilder},Action{GlobalConfigurator.ConfigurationBuilder}?)"/>.
    /// </remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to configure.</param>
    /// <param name="lunariumLogger">The pre-built logger instance.</param>
    /// <returns><paramref name="builder"/> for chaining.</returns>
    public static ILoggingBuilder AddLunariumLogger(
        this ILoggingBuilder builder,
        Lunarium.Logging.ILogger lunariumLogger)
    {
        builder.ClearProviders();
        builder.AddProvider(new LunariumLoggerProvider(lunariumLogger));
        return builder;
    }

    /// <summary>
    /// Builds and registers a LunariumLogger as the sole MEL log provider,
    /// letting the DI container manage the logger's lifecycle.
    /// </summary>
    /// <remarks>
    /// <para>Supported hosts:</para>
    /// <list type="bullet">
    /// <item>.NET 8+ <c>WebApplicationBuilder</c>: <c>builder.Logging.UseLunariumLog(...)</c></item>
    /// <item><c>IHostBuilder</c>: <c>hostBuilder.ConfigureLogging(l =&gt; l.UseLunariumLog(...))</c></item>
    /// </list>
    /// <para>
    /// The logger is registered as a singleton in the DI container.
    /// The host automatically calls <see cref="IAsyncDisposable.DisposeAsync"/> on shutdown,
    /// ensuring all buffered log entries are flushed before the process exits.
    /// </para>
    /// </remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to configure.</param>
    /// <param name="configureSinks">Delegate that configures output sinks on a <see cref="LoggerBuilder"/>.</param>
    /// <param name="configureGlobal">
    /// Optional delegate for global settings (timezone, timestamp format, etc.).
    /// Omit if you have already called <see cref="GlobalConfigurator.Configure"/> separately.
    /// Cannot coexist with an external <see cref="GlobalConfigurator.Configure"/> call.
    /// </param>
    /// <returns><paramref name="builder"/> for chaining.</returns>
    public static ILoggingBuilder UseLunariumLog(
        this ILoggingBuilder builder,
        Action<LoggerBuilder> configureSinks,
        Action<GlobalConfigurator.ConfigurationBuilder>? configureGlobal = null)
    {
        ArgumentNullException.ThrowIfNull(configureSinks);

        Exception? cgEx = null;
        try
        {
            if (configureGlobal != null)
            {
                var cb = GlobalConfigurator.Configure();
                configureGlobal(cb);
                cb.Apply();
            }
        }
        catch (Exception ex)
        {
            cgEx = ex;
        }

        var lb = new LoggerBuilder();
        configureSinks(lb);
        var logger = lb.Build();

        // 注册为单例：DI 容器 Dispose 时自动触发 DisposeAsync，剩余日志得以刷完
        builder.Services.AddSingleton(logger);

        builder.ClearProviders();
        builder.AddProvider(new LunariumLoggerProvider(logger));

        if (cgEx != null)
        {
            logger.ForContext("UseLunariumLog").Error(cgEx.Message);
        }

        return builder;
    }

    /// <summary>
    /// Reads the configuration section <paramref name="sectionKey"/> from <paramref name="configuration"/>,
    /// builds the logger named <paramref name="loggerName"/>, and registers it as the sole MEL log provider.
    /// </summary>
    /// <remarks>The <c>GlobalConfig</c> node takes effect only on the first call (one-shot design).</remarks>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to configure.</param>
    /// <param name="configuration">Application configuration, e.g. <c>builder.Configuration</c>.</param>
    /// <param name="loggerName">The <c>LoggerName</c> value in <c>LoggerConfigs[]</c> to build.</param>
    /// <param name="sectionKey">Configuration section key; defaults to <c>"LunariumLogging"</c>.</param>
    /// <param name="watchForChanges">Whether to watch for configuration changes and hot-reload sink configs.</param>
    /// <returns><paramref name="builder"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">No logger with the given name was found in the config.</exception>
    public static ILoggingBuilder UseLunariumLog(
        this ILoggingBuilder builder,
        IConfiguration configuration,
        string loggerName,
        string sectionKey = "LunariumLogging",
        bool watchForChanges = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(loggerName);

        var logger = configuration.BuildLunariumLogger(loggerName, sectionKey, watchForChanges)
            ?? throw new InvalidOperationException(
                $"Logger '{loggerName}' not found in configuration section '{sectionKey}'.");

        builder.Services.AddSingleton(logger);
        builder.ClearProviders();
        builder.AddProvider(new LunariumLoggerProvider(logger));

        return builder;
    }
}

// 所有重载均委托到 ILoggingBuilder 版本，保持逻辑一致
/// <summary>
/// Extension methods for <see cref="IHostBuilder"/> integration with LunariumLogger.
/// </summary>
public static class LunariumHostBuilderExtensions
{
    /// <summary>
    /// Configures and registers LunariumLogger as the sole MEL log provider on an <see cref="IHostBuilder"/>.
    /// </summary>
    /// <remarks>Usage: <c>Host.CreateDefaultBuilder(args).UseLunariumLog(...)</c></remarks>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <param name="configureSinks">Delegate that configures output sinks.</param>
    /// <param name="configureGlobal">Optional delegate for global settings.</param>
    /// <returns><paramref name="hostBuilder"/> for chaining.</returns>
    public static IHostBuilder UseLunariumLog(
        this IHostBuilder hostBuilder,
        Action<LoggerBuilder> configureSinks,
        Action<GlobalConfigurator.ConfigurationBuilder>? configureGlobal = null)
    {
        ArgumentNullException.ThrowIfNull(configureSinks);

        return hostBuilder.ConfigureServices((_, services) =>
            services.AddLogging(logging =>
                logging.UseLunariumLog(configureSinks, configureGlobal)));
    }

    /// <summary>
    /// Reads the specified configuration section and builds the named LunariumLogger
    /// as the sole MEL log provider on an <see cref="IHostBuilder"/>.
    /// </summary>
    /// <remarks>Usage: <c>Host.CreateDefaultBuilder(args).UseLunariumLog(configuration, "MyApp")</c></remarks>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="loggerName">The <c>LoggerName</c> value in the configuration.</param>
    /// <param name="sectionKey">Configuration section key; defaults to <c>"LunariumLogging"</c>.</param>
    /// <param name="watchForChanges">Whether to hot-reload sink configs on configuration changes.</param>
    /// <returns><paramref name="hostBuilder"/> for chaining.</returns>
    public static IHostBuilder UseLunariumLog(
        this IHostBuilder hostBuilder,
        IConfiguration configuration,
        string loggerName,
        string sectionKey = "LunariumLogging",
        bool watchForChanges = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(loggerName);

        return hostBuilder.ConfigureServices((_, services) =>
            services.AddLogging(logging =>
                logging.UseLunariumLog(configuration, loggerName, sectionKey, watchForChanges)));
    }
}

/// <summary>
/// Provides extension methods for converting a <see cref="Lunarium.Logging.ILogger"/> into
/// a <see cref="Microsoft.Extensions.Logging.ILogger"/> without registering it with a DI container.
/// Useful in libraries or tests that need to interop with MEL-consuming code.
/// </summary>
public static class LunariumLoggerConversionExtensions
{
    /// <summary>
    /// Wraps this <see cref="Lunarium.Logging.ILogger"/> in a <see cref="Microsoft.Extensions.Logging.ILogger"/>
    /// scoped to <paramref name="categoryName"/>.
    /// </summary>
    /// <param name="logger">The Lunarium logger to wrap.</param>
    /// <param name="categoryName">MEL category name; forwarded as a context to the Lunarium logger.</param>
    /// <returns>An <see cref="Microsoft.Extensions.Logging.ILogger"/> backed by this Lunarium logger.</returns>
    public static Microsoft.Extensions.Logging.ILogger ToMicrosoftLogger(
        this Lunarium.Logging.ILogger logger,
        string categoryName)
    {
        return new LunariumMsLoggerAdapter(logger.ForContext(categoryName), NullScopeProvider.Instance);
    }
}

// NullScopeProvider/NullScope 用于不经过 DI 直接使用适配器的场景，避免传入 null 导致 NRE
/// <summary>
/// No-op <see cref="IExternalScopeProvider"/> used when no DI scope infrastructure is available.
/// </summary>
internal sealed class NullScopeProvider : IExternalScopeProvider
{
    public static NullScopeProvider Instance { get; } = new();

    public void ForEachScope<TState>(Action<object?, TState> callback, TState state) { }

    public IDisposable Push(object? state) => NullScope.Instance;

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
