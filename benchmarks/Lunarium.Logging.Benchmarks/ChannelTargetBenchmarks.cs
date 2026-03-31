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
using BenchmarkDotNet.Attributes;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Target;

namespace Lunarium.Logging.Benchmarks;

/// <summary>
/// 测量 ChannelTarget 三变体的 Emit() 开销。
///
/// 关注点：
/// - LogEntryChannelTarget（零编码，传递引用）vs ByteChannelTarget（渲染 + ToArray）
///   vs StringChannelTarget（渲染 + UTF-8→string 解码）的性能差距
/// - [MemoryDiagnoser] 的 Allocated 列反映各变体的编码堆分配差异：
///     LogEntryChannelTarget → 无额外编码分配
///     ByteChannelTarget     → 1x byte[] 分配（ToArray 拷贝）
///     StringChannelTarget   → 1x string 分配（Encoding.UTF8.GetString）
///
/// 每个 Channel 配一个后台排空任务，保持 Channel 始终近空：
/// - TryWrite 始终成功，排除背压干扰
/// - 防止无界 Channel 无限积累导致 OOM
/// - [MemoryDiagnoser] 仅统计 benchmark 线程的分配，后台排空不干扰结果
/// </summary>
[MemoryDiagnoser]
public class ChannelTargetBenchmarks
{
    private Channel<LogEntry> _entryChannel = null!;
    private Channel<byte[]> _byteChannel = null!;
    private Channel<string> _stringChannel = null!;

    private LogEntryChannelTarget _entryTarget = null!;
    private ByteChannelTarget _byteTarget = null!;
    private StringChannelTarget _stringTarget = null!;

    private Task[] _drainers = null!;
    private LogEntry _entry = null!;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelper.EnsureGlobalConfig();

        var ts = DateTimeOffset.UtcNow;
        _entry = new LogEntry(
            loggerName: "Bench",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Bench"),
            timestamp: ts,
            logLevel: LogLevel.Info,
            message: "User {Name} logged in",
            properties: new object?[] { "Alice" },
            context: "Auth.Service",
            contextBytes: "Auth.Service"u8.ToArray(),
            scope: "",
            messageTemplate: LogParser.ParseMessage("User {Name} logged in"));

        _entryChannel = Channel.CreateUnbounded<LogEntry>();
        _byteChannel = Channel.CreateUnbounded<byte[]>();
        _stringChannel = Channel.CreateUnbounded<string>();

        _entryTarget = new LogEntryChannelTarget(_entryChannel.Writer);
        _byteTarget = new ByteChannelTarget(_byteChannel.Writer, toJson: false, isColor: false);
        _stringTarget = new StringChannelTarget(_stringChannel.Writer, toJson: false, isColor: false);

        // 后台排空：防止 Channel 无限积累，保证 TryWrite 始终走快速路径
        _drainers =
        [
            Drain(_entryChannel.Reader),
            Drain(_byteChannel.Reader),
            Drain(_stringChannel.Reader),
        ];
    }

    private static Task Drain<T>(ChannelReader<T> reader)
        => Task.Run(async () =>
        {
            // WaitToReadAsync 在 Channel 完成（writer 关闭）后返回 false，自然退出
            while (await reader.WaitToReadAsync())
                while (reader.TryRead(out _)) { }
        });

    [GlobalCleanup]
    public void Cleanup()
    {
        // 关闭 writer → Drain 的 WaitToReadAsync 返回 false → 排空任务退出
        _entryTarget.Dispose();
        _byteTarget.Dispose();
        _stringTarget.Dispose();
        Task.WaitAll(_drainers);
    }

    [Benchmark(Baseline = true, Description = "LogEntryChannelTarget: 零编码，原样传递 LogEntry 引用")]
    public void Emit_LogEntry()
    {
        _entryTarget.Emit(_entry);
    }

    [Benchmark(Description = "ByteChannelTarget: 渲染 + byte[] 拷贝（ToArray，跳过 UTF-8→string 解码）")]
    public void Emit_ByteArray()
    {
        _byteTarget.Emit(_entry);
    }

    [Benchmark(Description = "StringChannelTarget: 渲染 + UTF-8→string 解码（Encoding.UTF8.GetString）")]
    public void Emit_String()
    {
        _stringTarget.Emit(_entry);
    }
}
