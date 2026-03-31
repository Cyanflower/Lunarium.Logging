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

namespace Lunarium.Logging.Http.Target;

/// <summary>
/// A log target that asynchronously batches and sends log entries to an HTTP endpoint.
/// <para>
/// Maintains an internal bounded channel for backpressure. A background task handles
/// batch serialization and sending. <see cref="Emit"/> does not block the caller thread;
/// when the channel is full, the newest entries are dropped to prevent memory exhaustion.
/// </para>
/// </summary>
public sealed class HttpTarget : ILogTarget
{
    // ===== 配置 =====
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly IHttpLogSerializer _serializer;
    private readonly IReadOnlyDictionary<string, string>? _headers;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private readonly TimeSpan _disposeTimeout;
    private readonly TimeSpan _requestTimeout;

    // ===== 内部状态 =====
    private readonly Channel<LogEntry> _channel;
    private readonly Task _backgroundTask;
    private readonly CancellationTokenSource _shutdownCts = new();

    // Interlocked int：0 = 正常，1 = 已触发溢出警告（每次溢出事件各一次）
    private int _overflowWarned = 0;
    // Interlocked int：0 = 活跃，1 = 已 Dispose
    private int _disposed = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpTarget"/> class.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to send requests. Its lifecycle must be managed externally; the target will not dispose it.</param>
    /// <param name="endpoint">The log ingestion endpoint URI. Must use the <c>http</c> or <c>https</c> scheme.</param>
    /// <param name="serializer">The log serializer. Defaults to <see cref="JsonArraySerializer.Default"/>.</param>
    /// <param name="headers">Custom headers (e.g., for authentication) to attach to requests. Header names and values cannot contain CR or LF characters.</param>
    /// <param name="batchSize">The maximum number of entries to send in a single batch. Defaults to 100.</param>
    /// <param name="flushInterval">The time to wait before forcing a batch flush. Defaults to 5 seconds.</param>
    /// <param name="disposeTimeout">The maximum time to wait for the background queue to drain when disposed. Defaults to 5 seconds.</param>
    /// <param name="channelCapacity">The capacity of the internal channel. When exceeded, new entries are dropped. Defaults to 1000.</param>
    /// <param name="requestTimeout">The timeout for a single HTTP request. Defaults to 30 seconds.</param>
    /// <exception cref="ArgumentException">Thrown when the endpoint scheme is invalid or headers contain CR/LF characters.</exception>
    public HttpTarget(
        HttpClient httpClient,
        Uri endpoint,
        IHttpLogSerializer? serializer = null,
        IReadOnlyDictionary<string, string>? headers = null,
        int batchSize = 100,
        TimeSpan? flushInterval = null,
        TimeSpan? disposeTimeout = null,
        int channelCapacity = 1000,
        TimeSpan? requestTimeout = null)
    {
        if (endpoint.Scheme is not "http" and not "https")
            throw new ArgumentException(
                $"Endpoint must use http or https scheme, but got '{endpoint.Scheme}'.",
                nameof(endpoint));

        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                if (key.Contains('\r') || key.Contains('\n') ||
                    value.Contains('\r') || value.Contains('\n'))
                    throw new ArgumentException(
                        $"Header name or value must not contain CR or LF characters. Key: \"{key}\"",
                        nameof(headers));
            }
        }

        _httpClient = httpClient;
        _endpoint = endpoint;
        _serializer = serializer ?? JsonArraySerializer.Default;
        _headers = headers;
        _batchSize = batchSize > 0 ? batchSize : 100;
        _flushInterval = flushInterval ?? TimeSpan.FromSeconds(5);
        _disposeTimeout = disposeTimeout ?? TimeSpan.FromSeconds(5);
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(30);

        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(channelCapacity > 0 ? channelCapacity : 1000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleWriter = false,
            SingleReader = true
        });

        // 包装为顶层异常处理，防止未处理异常触发 TaskScheduler.UnobservedTaskException
        _backgroundTask = Task.Run(async () =>
        {
            try { await RunBackgroundAsync(_shutdownCts.Token).ConfigureAwait(false); }
            catch (Exception ex) { InternalLogger.Error(ex, $"[HttpTarget] Background task crashed unexpectedly. Endpoint: {_endpoint}"); }
        });
    }

    // ===== ILogTarget =====

    /// <inheritdoc/>
    public void Emit(LogEntry entry)
    {
        if (Volatile.Read(ref _disposed) == 1) return;

        if (!_channel.Writer.TryWrite(entry))
        {
            // 再次检查 _disposed：TryWrite 失败也可能是 Dispose 恰好关闭了 channel writer，而非真正满载
            if (Volatile.Read(ref _disposed) == 1) return;

            // 每次溢出事件仅警告一次（Flush 成功后重置）
            if (Interlocked.CompareExchange(ref _overflowWarned, 1, 0) == 0)
            {
                InternalLogger.Error(
                    $"[HttpTarget] Internal channel is full (capacity reached). " +
                    $"Dropping log entries until the next successful flush. Endpoint: {_endpoint}");
            }
        }
    }

    // ===== IDisposable =====

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

        // 1. 停止接受新条目，唤醒等待中的 Reader
        _channel.Writer.TryComplete();

        // 2. 等待后台任务排空（有超时上限）
        if (!_backgroundTask.Wait(_disposeTimeout))
        {
            // 3. 超时后通知后台任务强制取消飞行中的请求并退出
            _shutdownCts.Cancel();

            // 超时后报告剩余丢失条数
            int remaining = _channel.Reader.CanCount ? _channel.Reader.Count : -1;
            string countStr = remaining >= 0 ? remaining.ToString() : "unknown number of";
            InternalLogger.Error(
                $"[HttpTarget] Dispose timeout ({_disposeTimeout.TotalSeconds}s) exceeded. " +
                $"Dropping {countStr} remaining log entries. Endpoint: {_endpoint}");
            
            // 后台任务仍在运行，不能 Dispose CTS（token 仍被任务持有），让 GC 回收
            return;
        }

        _shutdownCts.Dispose();
    }

    // ===== 后台任务 =====

    private async Task RunBackgroundAsync(CancellationToken shutdownToken)
    {
        var buffer = new List<LogEntry>(_batchSize);
        var reader = _channel.Reader;

        while (true)
        {
            // 双触发：BatchSize 满 OR FlushInterval 到期，先到者触发 flush。
            // 每次外层循环创建一个 FlushInterval 计时器，在此区间内持续读取直到 batch 满。
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
            cts.CancelAfter(_flushInterval);

            try
            {
                // 持续累积，直到 batch 满（BatchSize 触发）或计时器到期（FlushInterval 触发）
                while (buffer.Count < _batchSize)
                {
                    if (!await reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
                        break; // channel 已完成（writer TryComplete 且队列已空）

                    while (buffer.Count < _batchSize && reader.TryRead(out var entry))
                        buffer.Add(entry);
                }
            }
            catch (OperationCanceledException)
            {
                // FlushInterval 到期 或 shutdownToken 触发，drain 剩余可读条目后继续
                while (buffer.Count < _batchSize && reader.TryRead(out var entry))
                    buffer.Add(entry);
            }

            if (buffer.Count > 0)
            {
                // 确保模板已解析（通过 Sink 调用路径已解析；直接调用路径此处补充解析）
                foreach (var e in buffer)
                    e.ParseMessage();

                await FlushBatchAsync(buffer, shutdownToken).ConfigureAwait(false);
                buffer.Clear();
            }

            // ChannelReader.Completion 在 writer 已完成且所有条目均已被读取后才变为 IsCompleted，
            // 此时 TryRead 一定返回 false，直接退出即可。
            if (_channel.Reader.Completion.IsCompleted) break;
        }
    }

    private async Task FlushBatchAsync(List<LogEntry> batch, CancellationToken shutdownToken)
    {
        HttpContent content;
        try
        {
            content = _serializer.Serialize(batch);
        }
        catch (Exception ex)
        {
            InternalLogger.Error(ex, $"[HttpTarget] Serialization failed. Dropping {batch.Count} entries. Endpoint: {_endpoint}");
            return;
        }

        bool success = await TrySendAsync(content, shutdownToken).ConfigureAwait(false);

        if (!success)
        {
            // shutdown 期间不重试，避免拖延 Dispose
            if (shutdownToken.IsCancellationRequested) return;

            // 1 次立即重试
            HttpContent retryContent;
            try
            {
                retryContent = _serializer.Serialize(batch);
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, $"[HttpTarget] Retry serialization failed. Dropping {batch.Count} entries. Endpoint: {_endpoint}");
                return;
            }

            success = await TrySendAsync(retryContent, shutdownToken).ConfigureAwait(false);
            if (!success)
            {
                InternalLogger.Error(
                    $"[HttpTarget] HTTP send failed after retry. Dropping {batch.Count} entries. Endpoint: {_endpoint}");
                return;
            }
        }

        // 发送成功：重置溢出警告，允许下次溢出再次报告
        Volatile.Write(ref _overflowWarned, 0);
    }

    private async Task<bool> TrySendAsync(HttpContent content, CancellationToken shutdownToken)
    {
        // 请求超时 + shutdownToken 联合取消：Dispose 时可立即中断飞行中的请求
        using var timeoutCts = new CancellationTokenSource(_requestTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken, timeoutCts.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };

            if (_headers is not null)
            {
                foreach (var (key, value) in _headers)
                    request.Headers.TryAddWithoutValidation(key, value);
            }

            using var response = await _httpClient
                .SendAsync(request, linkedCts.Token)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode) return true;

            string reason = response.ReasonPhrase is { Length: > 0 } rp ? $" {rp}" : string.Empty;
            InternalLogger.Error(
                $"[HttpTarget] HTTP {(int)response.StatusCode}{reason}. Endpoint: {_endpoint}");
            return false;
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
            // Dispose 触发，正常退出，不记录错误
            return false;
        }
        catch (OperationCanceledException)
        {
            // 请求超时（shutdownToken 未触发）
            InternalLogger.Error(
                $"[HttpTarget] HTTP request timed out after {_requestTimeout.TotalSeconds:F0}s. Endpoint: {_endpoint}");
            return false;
        }
        catch (Exception ex)
        {
            InternalLogger.Error(ex, $"[HttpTarget] HTTP send exception. Endpoint: {_endpoint}");
            return false;
        }
    }
}
