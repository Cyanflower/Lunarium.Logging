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

using BenchmarkDotNet.Attributes;

namespace Lunarium.Logging.Benchmarks;

/// <summary>
/// 测量调用方线程的 Log() 吞吐量。
///
/// 核心架构背景：Logger.Log() 是同步方法，其内部仅构造 LogEntry 并调用
/// Channel.TryWrite()，然后立即返回。实际的 Emit（过滤/解析/渲染/输出）
/// 在后台任务中异步执行。因此本 Benchmark 测量的是：
///   LogEntry 构造开销 + Channel TryWrite() 开销
///
/// 关注点：
/// - 不同属性数量对 Log() 调用开销的影响（params object?[] 数组分配）
/// - ForContext (LoggerWrapper) 的额外包装开销
/// - 批量写入 100 条的平均开销
///
/// 所有测试使用 NullTarget（不做任何输出），排除渲染和 I/O 干扰。
/// </summary>
[MemoryDiagnoser]
public class LoggerThroughputBenchmarks
{
    private ILogger _logger = null!;
    private ILogger _loggerWithContext = null!;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelper.EnsureGlobalConfig();

        _logger = new LoggerBuilder()
            .SetLoggerName("ThroughputBench")
            .AddSink(new NullTarget())
            .Build();

        // ForContext 返回 LoggerWrapper，测量其额外开销
        _loggerWithContext = _logger.ForContext("Benchmark.Throughput.Component");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // 等待后台任务处理完队列中的所有消息后释放资源
        if (_logger is IAsyncDisposable asyncDisposable)
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    // ==================== 单次 Log() 调用 ====================

    [Benchmark(Baseline = true, Description = "Log(): 无属性，纯文本消息")]
    public void Log_PlainMessage()
        => _logger.Info("Application started successfully");

    [Benchmark(Description = "Log(): 单属性 (params object?[1] 分配)")]
    public void Log_OneProperty()
        => _logger.Info("User {Name} logged in", "Alice");

    [Benchmark(Description = "Log(): 三属性 (params object?[3] 分配)")]
    public void Log_ThreeProperties()
        => _logger.Info("Request {Method} {Url} completed in {Duration}ms", "GET", "/api/users", 42);

    [Benchmark(Description = "Log(): 五属性 (params object?[5] 分配)")]
    public void Log_FiveProperties()
        => _logger.Info("Job {Id} {Status}: processed {Count} items in {Duration}ms on host {Host}",
            "job-001", "OK", 500, 123, "worker-1");

    [Benchmark(Description = "Log(): 通过 ForContext 包装器 (LoggerWrapper 额外调用)")]
    public void Log_ViaForContext()
        => _loggerWithContext.Info("User {Name} logged in", "Bob");

    // ==================== 批量写入 ====================

    [Benchmark(Description = "Log(): 批量 100 条（测量批量分摊后每条的开销）")]
    public void Log_Batch100()
    {
        for (int i = 0; i < 100; i++)
            _logger.Info("Batch message {Index} processed", i);
    }
}
