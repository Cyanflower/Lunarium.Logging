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

using Lunarium.Logging.Models;

namespace Lunarium.Logging.Filter;

// 日志过滤器，负责判断日志条目是否应该被输出。
// 使用缓存机制提升性能，避免重复的前缀匹配计算。
//
// 注意：Context 应该是相对静态的字符串（如类名、模块名），而非包含动态值的字符串。
// 正确用法：logger.ForContext("Order.Processor").Info("Processing {Id}", orderId)
// 错误用法：logger.ForContext($"Order.{orderId}").Info("Processing") ← 会导致缓存迅速膨胀并性能降级，且非结构化日志
internal sealed class LoggerFilter
{
    // 缓存条数上限
    private const int CacheMaxCountLimit = 2048;

    // 缓存已经检查过的上下文及其结果
    // Key: Context, Value: 是否应该输出
    private readonly ConcurrentDictionary<string, bool> _contextCache = new();

    private FilterConfig _config;

    private int _cacheCount = 0;

    internal LoggerFilter(FilterConfig config)
    {
        _config = config;
    }

    internal FilterConfig GetFilterConfig() => _config;

    internal void UpdateConfig(FilterConfig newConfig)
    {
        // 原子性地更新配置
        Interlocked.Exchange(ref _config, newConfig);
        // 清空缓存与计数器，因为新配置可能改变过滤结果
        // 原子性地将计数器重置为 0，且只有一个线程能拿到当时的旧值 >= Limit
        if (Interlocked.Exchange(ref _cacheCount, 0) >= CacheMaxCountLimit)
        {
            _contextCache.Clear();
        }
    }

    // 判断日志条目是否应该被输出
    internal bool ShouldEmit(LogEntry entry)
    {
        // 1. 首先检查日志级别过滤
        if (entry.LogLevel < _config.LogMinLevel || entry.LogLevel > _config.LogMaxLevel)
            return false;

        // 2. 执行上下文的过滤逻辑
        // 2.1 检查缓存 - 缓存命中
        if (_contextCache.TryGetValue(entry.Context, out var cachedResult))
            return cachedResult;

        // 2.2 执行上下文过滤检查 - 缓存未命中
        var shouldEmit = CheckContextFilters(entry.Context, _config);

        // 2.3 更新缓存与计数逻辑 (使用 TryAdd 替代 GetOrAdd + ReferenceEquals)
        // TryAdd 只有在 Key 不存在并成功插入时才会返回 true
        if (_contextCache.TryAdd(entry.Context, shouldEmit))
        {
            // 只有成功插入新项的线程才会增加计数器
            var currentCount = Interlocked.Increment(ref _cacheCount);

            if (currentCount >= CacheMaxCountLimit)
            {
                // 原子性地将计数器重置为 0，且只有一个线程能拿到当时的旧值 >= Limit
                if (Interlocked.Exchange(ref _cacheCount, 0) >= CacheMaxCountLimit)
                {
                    _contextCache.Clear();
                }
            }
        }

        return shouldEmit;
    }

    // 执行上下文过滤检查
    private bool CheckContextFilters(string context, FilterConfig cfg)
    {
        var hasIncludes = cfg.ContextFilterIncludes is { Count: > 0 };
        var hasExcludes = cfg.ContextFilterExcludes is { Count: > 0 };

        // 如果没有配置任何过滤规则,默认全部通过
        if (!hasIncludes && !hasExcludes)
            return true;

        // Include 检查:如果配置了 Include 规则,必须匹配其中之一
        if (hasIncludes)
        {
            var matchedInclude = cfg.ContextFilterIncludes!.Any(prefix =>
                context.StartsWith(prefix, cfg.ComparisonType));

            if (!matchedInclude)
                return false;
        }

        // Exclude 检查:如果匹配了 Exclude 规则,则排除
        if (hasExcludes)
        {
            var matchedExclude = cfg.ContextFilterExcludes!.Any(prefix =>
                context.StartsWith(prefix, cfg.ComparisonType));

            if (matchedExclude)
                return false;
        }

        return true;
    }
}