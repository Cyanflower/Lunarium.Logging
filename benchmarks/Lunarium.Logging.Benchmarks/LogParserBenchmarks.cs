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
using Lunarium.Logging.Parser;

namespace Lunarium.Logging.Benchmarks;

/// <summary>
/// 测量消息模板解析器 (LogParser) 的性能。
///
/// 关注点：
/// - 缓存命中路径：仅 ConcurrentDictionary 查找
/// - 缓存未命中路径：状态机解析 + 字典写入
/// - 不同模板复杂度的解析开销对比
///
/// 注意：LogParser 的模板缓存是进程级静态字典 (上限 4096 条)。
/// CacheMiss_Approx 使用 6000 个唯一字符串池循环调用：
///   超出 4096 后缓存自动清空，从而模拟持续的缓存未命中场景。
/// </summary>
[MemoryDiagnoser]
public class LogParserBenchmarks
{
    // 缓存未命中测试：字符串池大小需超过缓存上限 (4096)，以触发缓存清空
    private const int UniqueMsgCount = 6000;
    private static readonly string[] _uniqueMessages;
    private int _missIndex;

    static LogParserBenchmarks()
    {
        _uniqueMessages = new string[UniqueMsgCount];
        for (int i = 0; i < UniqueMsgCount; i++)
            _uniqueMessages[i] = $"Benchmark message {i:D5} completed in {i % 1000}ms for endpoint /api/v{i % 10}/resource";
    }

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelper.EnsureGlobalConfig();

        // 预热：确保常用模板在缓存命中测试前已写入缓存
        LogParser.ParseMessage("Application started successfully");
        LogParser.ParseMessage("User {Name} logged in");
        LogParser.ParseMessage("Request {Method} {Url} completed in {Duration}ms");
        LogParser.ParseMessage("{Count,8:D} items processed by {@Worker} ({Percent:P1} done)");
        LogParser.ParseMessage("{{ escaped }} text with {Value}");
    }

    // ==================== 缓存命中 ====================

    [Benchmark(Baseline = true, Description = "纯文本，无占位符 (缓存命中)")]
    public MessageTemplate PlainText_CacheHit()
        => LogParser.ParseMessage("Application started successfully");

    [Benchmark(Description = "单属性模板 (缓存命中)")]
    public MessageTemplate SingleProperty_CacheHit()
        => LogParser.ParseMessage("User {Name} logged in");

    [Benchmark(Description = "三属性模板 (缓存命中)")]
    public MessageTemplate ThreeProperty_CacheHit()
        => LogParser.ParseMessage("Request {Method} {Url} completed in {Duration}ms");

    [Benchmark(Description = "复杂模板：对齐 + 格式化 + 解构前缀 (缓存命中)")]
    public MessageTemplate ComplexTemplate_CacheHit()
        => LogParser.ParseMessage("{Count,8:D} items processed by {@Worker} ({Percent:P1} done)");

    [Benchmark(Description = "含转义 {{ }} 的模板 (缓存命中)")]
    public MessageTemplate EscapedBraces_CacheHit()
        => LogParser.ParseMessage("{{ escaped }} text with {Value}");

    // ==================== 缓存未命中（近似） ====================

    [Benchmark(Description = "缓存未命中 (近似，6000 个唯一字符串池，超出 4096 后缓存清空)")]
    public MessageTemplate CacheMiss_Approx()
    {
        var msg = _uniqueMessages[_missIndex % UniqueMsgCount];
        _missIndex++;
        return LogParser.ParseMessage(msg);
    }
}
