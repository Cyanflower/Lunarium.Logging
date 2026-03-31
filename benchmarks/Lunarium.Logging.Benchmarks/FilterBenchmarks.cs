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
using Lunarium.Logging.Filter;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Config.Models;

namespace Lunarium.Logging.Benchmarks;

/// <summary>
/// 测量上下文过滤器 (LoggerFilter) 的性能。
///
/// 关注点：
/// - 缓存命中路径：仅 ConcurrentDictionary 查找（约等于 1 次字典读）
/// - 缓存未命中路径：前缀匹配计算 + 字典写入
/// - 有/无 Include/Exclude 规则的开销差异
///
/// 注意：LoggerFilter 的上下文缓存是实例级别 (上限 2048 条)。
/// 不同 Benchmark 实例持有独立的 LoggerFilter，缓存隔离。
/// CacheMiss_Approx 使用 3000 个唯一 context 字符串池：
///   超出 2048 后缓存自动清空，从而模拟持续的缓存未命中场景。
/// </summary>
[MemoryDiagnoser]
public class FilterBenchmarks
{
    private LoggerFilter _filterNoRules = null!;
    private LoggerFilter _filterWithIncludes = null!;
    private LoggerFilter _filterWithExcludes = null!;

    private FilterConfig _cfgNoRules = null!;
    private FilterConfig _cfgWithIncludes = null!;
    private FilterConfig _cfgWithExcludes = null!;

    // 预构建的 LogEntry（context 固定，反复命中缓存）
    private LogEntry _entryMatchingContext = null!;
    private LogEntry _entryNonMatchingContext = null!;

    // 缓存未命中测试：唯一 context 字符串池
    private const int UniqueCxtCount = 3000;
    private static readonly LogEntry[] _uniqueEntries;
    private int _missIndex;

    static FilterBenchmarks()
    {
        var ts = DateTimeOffset.UtcNow;
        _uniqueEntries = new LogEntry[UniqueCxtCount];
        for (int i = 0; i < UniqueCxtCount; i++)
        {
            var ctx = $"Service.Component.Module_{i:D4}";
            _uniqueEntries[i] = new LogEntry(
                loggerName: "Bench",
                loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Bench"),
                timestamp: ts,
                logLevel: LogLevel.Info,
                message: "test",
                properties: Array.Empty<object?>(),
                context: ctx,
                contextBytes: System.Text.Encoding.UTF8.GetBytes(ctx),
                scope: "",
                messageTemplate: LogParser.EmptyMessageTemplate);
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelper.EnsureGlobalConfig();
        var ts = DateTimeOffset.UtcNow;

        _cfgNoRules = new FilterConfig();

        _cfgWithIncludes = new FilterConfig
        {
            ContextFilterIncludes = ["Order", "Payment", "Auth", "User", "Http"]
        };

        _cfgWithExcludes = new FilterConfig
        {
            ContextFilterExcludes = ["Debug.Internal", "System", "Microsoft"]
        };

        _filterNoRules = new LoggerFilter(_cfgNoRules);
        _filterWithIncludes = new LoggerFilter(_cfgWithIncludes);
        _filterWithExcludes = new LoggerFilter(_cfgWithExcludes);

        _entryMatchingContext = new LogEntry(
            loggerName: "Bench",
                loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Bench"),
            timestamp: ts,
            logLevel: LogLevel.Info,
            message: "test",
            properties: Array.Empty<object?>(),
            context: "Order.Processor",
            contextBytes: "Order.Processor"u8.ToArray(),
            scope: "",
            messageTemplate: LogParser.EmptyMessageTemplate);

        _entryNonMatchingContext = new LogEntry(
            loggerName: "Bench",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Bench"),
            timestamp: ts,
            logLevel: LogLevel.Info,
            message: "test",
            properties: Array.Empty<object?>(),
            context: "System.Internal",
            contextBytes: "System.Internal"u8.ToArray(),
            scope: "",
            messageTemplate: LogParser.EmptyMessageTemplate);

        // 预热缓存，确保命中测试时直接走缓存路径
        _filterNoRules.ShouldEmit(_entryMatchingContext);
        _filterWithIncludes.ShouldEmit(_entryMatchingContext);
        _filterWithIncludes.ShouldEmit(_entryNonMatchingContext);
        _filterWithExcludes.ShouldEmit(_entryMatchingContext);
        _filterWithExcludes.ShouldEmit(_entryNonMatchingContext);
    }

    // ==================== 缓存命中 ====================

    [Benchmark(Baseline = true, Description = "无规则，级别通过 (缓存命中)")]
    public bool NoRules_Pass_CacheHit()
        => _filterNoRules.ShouldEmit(_entryMatchingContext);

    [Benchmark(Description = "Include 规则，context 匹配通过 (缓存命中)")]
    public bool WithIncludes_Pass_CacheHit()
        => _filterWithIncludes.ShouldEmit(_entryMatchingContext);

    [Benchmark(Description = "Include 规则，context 不匹配被拒绝 (缓存命中)")]
    public bool WithIncludes_Reject_CacheHit()
        => _filterWithIncludes.ShouldEmit(_entryNonMatchingContext);

    [Benchmark(Description = "Exclude 规则，context 不在排除列表通过 (缓存命中)")]
    public bool WithExcludes_Pass_CacheHit()
        => _filterWithExcludes.ShouldEmit(_entryMatchingContext);

    [Benchmark(Description = "Exclude 规则，context 命中排除列表被拒绝 (缓存命中)")]
    public bool WithExcludes_Reject_CacheHit()
        => _filterWithExcludes.ShouldEmit(_entryNonMatchingContext);

    // ==================== 缓存未命中（近似） ====================

    [Benchmark(Description = "无规则，缓存未命中 (近似，3000 个唯一 context，超出 2048 后缓存清空)")]
    public bool NoRules_CacheMiss_Approx()
    {
        var entry = _uniqueEntries[_missIndex % UniqueCxtCount];
        _missIndex++;
        return _filterNoRules.ShouldEmit(entry);
    }
}
