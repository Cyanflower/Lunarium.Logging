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

using System.Threading.Channels;
using Lunarium.Logging.Target;
using Lunarium.Logging.Models;

namespace Lunarium.Logging;


/// <summary>
/// Extension methods for <see cref="LoggerBuilder"/> that provide convenient helpers for adding log output targets (sinks).
/// </summary>
public static class LoggerBuilderExtensions
{
    /// <summary>
    /// Adds a console output sink to the logger builder.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerBuilder"/> to configure.</param>
    /// <param name="sinkName">Optional name for this sink, used with <see cref="LoggerManager"/> for runtime updates.</param>
    /// <param name="toJson">When <see langword="true"/>, entries are rendered as JSON. Defaults to <see langword="false"/>.</param>
    /// <param name="isColor">When <see langword="true"/>, ANSI color codes are included. Defaults to <see langword="true"/>.</param>
    /// <param name="FilterConfig">Optional filter configuration for level and context filtering.</param>
    /// <param name="textOutputIncludeConfig">Controls which fields are included in text output.</param>
    /// <returns>The same <see cref="LoggerBuilder"/> instance for chaining.</returns>
    public static LoggerBuilder AddConsoleSink(
        this LoggerBuilder builder,
        string? sinkName = null,
        bool toJson = false,
        bool isColor = true,
        FilterConfig? FilterConfig = null,
        TextOutputIncludeConfig? textOutputIncludeConfig = null)
    {
        return builder.AddSink(
            target: new ConsoleTarget(toJson, isColor, textOutputIncludeConfig),
            cfg: FilterConfig,
            name: sinkName);
    }

    /// <summary>
    /// Adds a file output sink that writes log entries to the specified path.
    /// The file is created automatically if it does not exist; entries are appended.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerBuilder"/> to configure.</param>
    /// <param name="logFilePath">Path to the log file.</param>
    /// <returns>The same <see cref="LoggerBuilder"/> instance for chaining.</returns>
    public static LoggerBuilder AddFileSink(
        this LoggerBuilder builder,
        string logFilePath)
    {
        return builder.AddSink(
            target: new FileTarget(logFilePath));
    }

    /// <summary>
    /// Adds a daily-rotating file sink. A new log file is created each day.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerBuilder"/> to configure.</param>
    /// <param name="logFilePath">Base path for the log file. The date is appended automatically, e.g. <c>logs/app-2023-10-27.log</c>.</param>
    /// <param name="maxFile">Maximum number of log files to retain. 0 or negative means unlimited.</param>
    /// <param name="toJson">When <see langword="true"/>, entries are rendered as JSON. Defaults to <see langword="false"/>.</param>
    /// <param name="isColor">When <see langword="true"/>, ANSI color codes are included. Defaults to <see langword="false"/>.</param>
    /// <param name="sinkName">Optional name for this sink, used with <see cref="LoggerManager"/> for runtime updates.</param>
    /// <param name="FilterConfig">Optional filter configuration for level and context filtering.</param>
    /// <param name="textOutputIncludeConfig">Controls which fields are included in text output.</param>
    /// <returns>The same <see cref="LoggerBuilder"/> instance for chaining.</returns>
    public static LoggerBuilder AddTimedRotatingFileSink(
        this LoggerBuilder builder,
        string logFilePath,
        int maxFile = 0,
        bool toJson = false,
        bool isColor = false,
        string? sinkName = null,
        FilterConfig? FilterConfig = null,
        TextOutputIncludeConfig? textOutputIncludeConfig = null)
    {
        return builder.AddSink(
            target: new FileTarget(
                logFilePath: logFilePath,
                rotateOnNewDay: true,
                maxFile: maxFile,
                toJson: toJson,
                isColor: isColor,
                textOutputIncludeConfig: textOutputIncludeConfig),
            cfg: FilterConfig,
            name: sinkName);
    }

    /// <summary>
    /// Adds a size-based rotating file sink. A new timestamped file is created when the current file reaches the size limit.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerBuilder"/> to configure.</param>
    /// <param name="logFilePath">Base path for the log file.</param>
    /// <param name="maxFileSizeMB">Maximum size of a single file in MB.</param>
    /// <param name="maxFile">Maximum number of log files to retain.</param>
    /// <param name="toJson">When <see langword="true"/>, entries are rendered as JSON. Defaults to <see langword="false"/>.</param>
    /// <param name="isColor">When <see langword="true"/>, ANSI color codes are included. Defaults to <see langword="false"/>.</param>
    /// <param name="sinkName">Optional name for this sink, used with <see cref="LoggerManager"/> for runtime updates.</param>
    /// <param name="FilterConfig">Optional filter configuration for level and context filtering.</param>
    /// <param name="textOutputIncludeConfig">Controls which fields are included in text output.</param>
    /// <returns>The same <see cref="LoggerBuilder"/> instance for chaining.</returns>
    public static LoggerBuilder AddSizedRotatingFileSink(
        this LoggerBuilder builder,
        string logFilePath,
        double maxFileSizeMB = 10,
        int maxFile = 10,
        bool toJson = false,
        bool isColor = false,
        string? sinkName = null,
        FilterConfig? FilterConfig = null,
        TextOutputIncludeConfig? textOutputIncludeConfig = null)
    {
        return builder.AddSink(
            target: new FileTarget(
                logFilePath: logFilePath,
                maxFileSizeMB: maxFileSizeMB,
                maxFile: maxFile,
                toJson: toJson,
                isColor: isColor,
                textOutputIncludeConfig: textOutputIncludeConfig),
            cfg: FilterConfig,
            name: sinkName);
    }

    /// <summary>
    /// Adds a file sink with flexible rotation. Size-based and daily rotation can be enabled independently;
    /// either condition triggers a rotation when both are active.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerBuilder"/> to configure.</param>
    /// <param name="logFilePath">Base path for the log file, e.g. <c>logs/app.log</c>.</param>
    /// <param name="maxFileSizeMB">Maximum size of a single file in MB. ≤0 disables size-based rotation.</param>
    /// <param name="rotateOnNewDay">When <see langword="true"/>, a new file is created at the start of each day.</param>
    /// <param name="maxFile">Maximum number of log files to retain. ≤0 means unlimited.
    /// At least one rotation strategy must be enabled when this is positive.</param>
    /// <param name="toJson">When <see langword="true"/>, entries are rendered as JSON. Defaults to <see langword="false"/>.</param>
    /// <param name="isColor">When <see langword="true"/>, ANSI color codes are included. Defaults to <see langword="false"/>.</param>
    /// <param name="sinkName">Optional name for this sink, used with <see cref="LoggerManager"/> for runtime updates.</param>
    /// <param name="FilterConfig">Optional filter configuration for level and context filtering.</param>
    /// <param name="textOutputIncludeConfig">Controls which fields are included in text output.</param>
    /// <returns>The same <see cref="LoggerBuilder"/> instance for chaining.</returns>
    public static LoggerBuilder AddRotatingFileSink(
        this LoggerBuilder builder, string logFilePath,
        double maxFileSizeMB = 10,
        bool rotateOnNewDay = true,
        int maxFile = 10,
        bool toJson = false,
        bool isColor = false,
        string? sinkName = null,
        FilterConfig? FilterConfig = null,
        TextOutputIncludeConfig? textOutputIncludeConfig = null)
    {
        return builder.AddSink(
            target: new FileTarget(
                logFilePath: logFilePath,
                maxFileSizeMB: maxFileSizeMB,
                rotateOnNewDay: rotateOnNewDay,
                maxFile: maxFile,
                toJson: toJson,
                isColor: isColor,
                textOutputIncludeConfig: textOutputIncludeConfig),
            cfg: FilterConfig,
            name: sinkName);
    }
    
    /// <summary>
    /// Adds a sink that formats each entry as a string and writes it to a channel.
    /// Consume the formatted log text via the returned <paramref name="reader"/>.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerBuilder"/> to configure.</param>
    /// <param name="reader">Channel reader for consuming the formatted log strings.</param>
    /// <param name="isColor">When <see langword="true"/>, ANSI color codes are included. Defaults to <see langword="false"/>.</param>
    /// <param name="toJson">When <see langword="true"/>, entries are rendered as JSON. Takes precedence over <paramref name="isColor"/>. Defaults to <see langword="false"/>.</param>
    /// <param name="capacity">Channel capacity. <see langword="null"/> creates an unbounded channel; when set, new entries are dropped when the channel is full.</param>
    /// <param name="sinkName">Optional name for this sink, used with <see cref="LoggerManager"/> for runtime updates.</param>
    /// <param name="FilterConfig">Optional filter configuration.</param>
    /// <param name="textOutputIncludeConfig">Controls which fields are included in text output.</param>
    /// <returns>The same <see cref="LoggerBuilder"/> instance for chaining.</returns>
    public static LoggerBuilder AddStringChannelSink(
        this LoggerBuilder builder,
        out ChannelReader<string> reader,
        bool isColor = false,
        bool toJson = false,
        int? capacity = null,
        string? sinkName = null,
        FilterConfig? FilterConfig = null,
        TextOutputIncludeConfig? textOutputIncludeConfig = null)
    {
        var bridge = new LogChannelBridge<string>(capacity: capacity);
        reader = bridge.Reader;
        var target = new StringChannelTarget(
            writer: bridge.Writer,
            toJson: toJson,
            isColor: isColor,
            textOutputIncludeConfig: textOutputIncludeConfig);
        return builder.AddSink(target: target, cfg: FilterConfig, name: sinkName);
    }

    /// <summary>
    /// Adds a sink that formats each entry as a UTF-8 byte array and writes it to a channel.
    /// Consume the formatted bytes via the returned <paramref name="reader"/>.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerBuilder"/> to configure.</param>
    /// <param name="reader">Channel reader for consuming the formatted byte arrays.</param>
    /// <param name="isColor">When <see langword="true"/>, ANSI color codes are included. Defaults to <see langword="false"/>.</param>
    /// <param name="toJson">When <see langword="true"/>, entries are rendered as JSON. Takes precedence over <paramref name="isColor"/>. Defaults to <see langword="false"/>.</param>
    /// <param name="capacity">Channel capacity. <see langword="null"/> creates an unbounded channel; when set, new entries are dropped when the channel is full.</param>
    /// <param name="sinkName">Optional name for this sink, used with <see cref="LoggerManager"/> for runtime updates.</param>
    /// <param name="FilterConfig">Optional filter configuration.</param>
    /// <param name="textOutputIncludeConfig">Controls which fields are included in text output.</param>
    /// <returns>The same <see cref="LoggerBuilder"/> instance for chaining.</returns>
    public static LoggerBuilder AddUtf8ByteChannelSink(
        this LoggerBuilder builder,
        out ChannelReader<Byte[]> reader,
        bool isColor = false,
        bool toJson = false,
        int? capacity = null,
        string? sinkName = null,
        FilterConfig? FilterConfig = null,
        TextOutputIncludeConfig? textOutputIncludeConfig = null)
    {
        var bridge = new LogChannelBridge<Byte[]>(capacity: capacity);
        reader = bridge.Reader;
        var target = new ByteChannelTarget(
            writer: bridge.Writer,
            toJson: toJson,
            isColor: isColor,
            textOutputIncludeConfig: textOutputIncludeConfig);
        return builder.AddSink(target: target, cfg: FilterConfig, name: sinkName);
    }

    /// <summary>
    /// Adds a sink that passes <see cref="LogEntry"/> instances to a channel without formatting.
    /// Use this when the consumer needs the full structured entry.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerBuilder"/> to configure.</param>
    /// <param name="reader">Channel reader for consuming <see cref="LogEntry"/> instances.</param>
    /// <param name="capacity">Channel capacity. <see langword="null"/> creates an unbounded channel; when set, new entries are dropped when the channel is full.</param>
    /// <param name="sinkName">Optional name for this sink, used with <see cref="LoggerManager"/> for runtime updates.</param>
    /// <param name="FilterConfig">Optional filter configuration.</param>
    /// <returns>The same <see cref="LoggerBuilder"/> instance for chaining.</returns>
    public static LoggerBuilder AddLogEntryChannelSink(
        this LoggerBuilder builder,
        out ChannelReader<LogEntry> reader,
        int? capacity = null,
        string? sinkName = null,
        FilterConfig? FilterConfig = null)
    {
        var bridge = new LogChannelBridge<LogEntry>(capacity: capacity);
        reader = bridge.Reader;
        return builder.AddSink(target: new LogEntryChannelTarget(writer: bridge.Writer), cfg: FilterConfig, name: sinkName);
    }

    /// <summary>
    /// Adds a channel sink with a custom transform delegate. No base class or interface implementation is required.
    /// </summary>
    /// <typeparam name="T">The output type written to the channel.</typeparam>
    /// <param name="builder">The <see cref="LoggerBuilder"/> to configure.</param>
    /// <param name="reader">Channel reader for consuming transformed <typeparamref name="T"/> values.</param>
    /// <param name="transform">Delegate that converts a <see cref="LogEntry"/> into <typeparamref name="T"/>.</param>
    /// <param name="capacity">Channel capacity. <see langword="null"/> creates an unbounded channel; when set, new entries are dropped when the channel is full.</param>
    /// <param name="sinkName">Optional name for this sink, used with <see cref="LoggerManager"/> for runtime updates.</param>
    /// <param name="FilterConfig">Optional filter configuration.</param>
    /// <returns>The same <see cref="LoggerBuilder"/> instance for chaining.</returns>
    public static LoggerBuilder AddChannelSink<T>(
        this LoggerBuilder builder,
        out ChannelReader<T> reader,
        Func<LogEntry, T> transform,
        int? capacity = null,
        string? sinkName = null,
        FilterConfig? FilterConfig = null)
    {
        var bridge = new LogChannelBridge<T>(capacity: capacity);
        reader = bridge.Reader;
        return builder.AddSink(target: new DelegateChannelTarget<T>(writer: bridge.Writer, transform: transform), cfg: FilterConfig, name: sinkName);
    }

    /// <summary>
    /// Creates the target via <see cref="ISinkConfig.CreateTarget"/> and registers the sink. Supports any third-party implementation.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerBuilder"/> to configure.</param>
    /// <param name="sinkConfig">Configuration object containing sink-specific parameters and filter settings.</param>
    /// <returns>The same <see cref="LoggerBuilder"/> instance for chaining.</returns>
    public static LoggerBuilder AddSinkByConfig(this LoggerBuilder builder, ISinkConfig sinkConfig)
        => builder.AddSink(sinkConfig);
}