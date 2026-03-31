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
using System.Text;
using Lunarium.Logging.Parser;

namespace Lunarium.Logging.Core;

// LunariumLogger 的核心实现。
// 通过一个后台任务和 .NET Channel 异步处理日志消息，
// 将日志分发到所有已配置的 Sink，从而避免阻塞调用方线程。
internal sealed class Logger : ILogger, IAsyncDisposable
{
    // === 内部变量 ===
    // 传递日志消息的无界Channel，用于解耦日志写入和处理
    private readonly Channel<LogEntry> _queue = Channel.CreateUnbounded<LogEntry>();
    // 用于在关闭时通知后台任务停止
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    // 负责从 Channel 读取并处理日志的后台任务
    private readonly Task _processTask;

    // === 日志配置 ===
    // 日志记录器名称
    private readonly string _loggerName;
    private readonly byte[] _loggerNameBytes;

    // 所有输出目标的列表
    private readonly List<Sink> _sinks;

    // 初始化一个新的 LunariumLogger 实例。
    // sinks：日志要发送到的 Sink 列表。
    // loggerName：此日志记录器的名称。
    internal Logger(List<Sink> sinks, string loggerName)
    {
        _sinks = sinks;
        _loggerName = loggerName;
        _loggerNameBytes = Encoding.UTF8.GetBytes(loggerName);
        // 启动后台处理任务
        _processTask = Task.Run(ProcessQueueAsync);
    }

    // 在后台运行，持续从 Channel 中读取日志条目并分发给所有 Sink
    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var logEntry in _queue.Reader.ReadAllAsync(CancellationToken.None))
            {
                // 解包
                foreach (var sink in _sinks)
                {
                    try
                    {
                        sink.Emit(logEntry);
                    }
                    catch (Exception ex)
                    {
                        InternalLogger.Error(ex,"ProcessQueueAsync To Sink Emit Failed:");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 这是正常关闭时预期的异常，无需处理
        }
        catch (Exception ex)
        {
            InternalLogger.Error(ex,"ProcessQueueAsync Catch Exception:");
        }
    }

    // 创建一个日志条目并将其放入队列中等待异步处理
    public void Log(LogLevel level, Exception? ex = null, string message = "", string context = "", ReadOnlyMemory<byte> contextBytes = default, string scope = "", params object?[] propertyValues)
    {
        try
        {
            // [Debug]
            // Console.WriteLine($"{_loggerName}: Log 被调用");
            // 如果日志记录器正在关闭，则不再接受新的日志
            if (_cancellationTokenSource.IsCancellationRequested) return;
    
            var entry = new LogEntry(
                loggerName: _loggerName,
                loggerNameBytes: _loggerNameBytes,
                timestamp: LogTimestampConfig.GetTimestamp(),
                logLevel: level,
                message: message,
                properties: propertyValues,
                context: context,
                contextBytes: contextBytes,
                scope: scope,
                messageTemplate: LogParser.EmptyMessageTemplate,
                exception: ex
            );
    
            // 尝试将日志条目写入 Channel
            _queue.Writer.TryWrite(entry);
        }
        catch (Exception catchEx)
        {
            InternalLogger.Error(catchEx,$"Log Failed: ");
        }
    }

    internal void UpdateSinkConfig(string sinkName, ISinkConfig sinkConfig)
    {
        var sink = _sinks.FirstOrDefault(s => s.Name == sinkName);
        if (sink != null)
        {
            sink.UpdateSinkConfig(sinkConfig);
        }
    }

    internal void UpdateLoggerConfig(LoggerConfig loggerConfig)
    {
        foreach (var sink in _sinks)
        {
            if (loggerConfig.ConsoleSinks.TryGetValue(sink.Name, out var consoleSinkConfig))
            {
                sink.UpdateSinkConfig(consoleSinkConfig);
            }
            else if (loggerConfig.FileSinks.TryGetValue(sink.Name, out var fileSinkConfig))
            {
                sink.UpdateSinkConfig(fileSinkConfig);
            }
            else
            {
                // 如果没有找到对应的配置，则禁用该 Sink
                sink.DisableSink();
                // 只 Dispose Sink 内部的 Target, 不更改 _sinks 列表, Sink Disable 之后 Emit 会直接返回, 不执行任何操作
                sink.Dispose();
            }
        }
    }
    
    // 关闭日志记录器。
    // 停止接受新的日志，等待所有已在队列中的日志处理完毕，然后释放所有 Sink 的资源。
    public async ValueTask DisposeAsync()
    {
        // Console.WriteLine($"[LunariumLogger.Internal] Dispose: Logger is shutting down. Flushing queues...");
        _queue.Writer.Complete();
        _cancellationTokenSource.Cancel();
        
        try
        {
            await _processTask;
        }
        catch (Exception ex)
        {
            // 记录任务异常，但继续清理
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[LunariumLogger.InternalError] ProcessTask ended with error: {ex.Message}");
            Console.ResetColor();
        }
        
        // 即使任务失败，也要释放target
        foreach (var sink in _sinks)
        {
            try
            {
                sink.Target.Dispose();
            }
            catch (Exception ex)
            {            
                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"[LunariumLogger.InternalError] Sink disposal failed: {ex.Message}");
                Console.ResetColor();
            }
        }
        
        _cancellationTokenSource.Dispose();
    }
}