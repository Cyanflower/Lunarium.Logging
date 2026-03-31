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

// ============================================================
//  本文件是 Lunarium.Logging 的进阶用法示例，仅供参考，不参与编译。
//  涵盖自定义 Sink（ILogTarget / ChannelTarget<T>）、高性能解构接口
//  （IDestructurable / IDestructured）、AOT 配置，以及 LoggerManager
//  运行时动态配置。
// ============================================================

using System.Text.Json.Serialization;
using System.Threading.Channels;
using Lunarium.Logging.Config;
using Lunarium.Logging.Target;
using Lunarium.Logging.Configuration;
using Lunarium.Logging.Extensions;
using Microsoft.Extensions.Hosting;

namespace Lunarium.Logging;

// ================================================================
//  第一节：LogEntry 字段说明
//
//  LogEntry 是不可变的日志事件对象，由 Logger 创建后推入 Channel，
//  在 Sink.Emit() 中经过过滤和解析后传递给 ILogTarget。
// ================================================================

/*  LogEntry 字段一览：

  | 字段               | 类型                    | 说明                                              |
  |--------------------|-------------------------|---------------------------------------------------|
  | LoggerName         | string                  | Logger 实例名称（构建时通过 .SetLoggerName() 设置）  |
  | LoggerNameBytes    | ReadOnlyMemory<byte>    | LoggerName 的 UTF-8 预编码字节，Writer 层直接写入 |
  | LogLevel           | LogLevel                | 日志级别（Debug/Info/Warning/Error/Critical）     |
  | Timestamp          | DateTimeOffset          | 时间戳（按 GlobalConfigurator 时区设置）          |
  | Message            | string                  | 原始消息模板字符串（未渲染）                      |
  | Properties         | object?[]               | 消息模板属性值数组                                |
  | Context            | string                  | 上下文字符串（ForContext 设置）                   |
  | ContextBytes       | ReadOnlyMemory<byte>    | Context 的 UTF-8 预编码字节                       |
  | Scope              | string                  | MEL Scope 信息（由 ILoggingBuilder 填充）         |
  | Exception          | Exception?              | 关联异常                                          |
  | MessageTemplate    | MessageTemplate         | 延迟解析的模板（Emit 时由 Sink 内部填充）         |
*/

// ================================================================
//  第二节：自定义 ILogTarget — 最底层扩展点
//
//  三条扩展路径：
//  A. 实现 ILogTarget（同步，最底层）
//  B. 继承 ChannelTarget<T>（异步 Channel 桥接，只需实现 Transform）
//  C. 实现能力接口（IJsonTextTarget / ITextTarget）——让 FilterConfig 统一控制格式
// ================================================================

// ── 路径 A：实现 ILogTarget（内存缓冲 Sink 示例） ──────────────────────────────
public sealed class InMemoryLogTarget : ILogTarget
{
    private readonly List<LogEntry> _entries = new();
    private readonly Lock _lock = new();

    public void Emit(LogEntry entry)
    {
        lock (_lock)
            _entries.Add(entry);
    }

    /// <summary>取出并清空已缓冲的日志条目。</summary>
    public IReadOnlyList<LogEntry> TakeAll()
    {
        lock (_lock)
        {
            var snapshot = _entries.ToList();
            _entries.Clear();
            return snapshot;
        }
    }

    public void Dispose() { }
}

// 注册方式：
// var memTarget = new InMemoryLogTarget();
// var logger = new LoggerBuilder()
//     .SetLoggerName("Test")
//     .AddSink(memTarget, filterConfig: null, name: "memory")
//     .Build();

// ── 路径 B：继承 ChannelTarget<T>（异步数据库写入 Sink 示例） ─────────────────
// ChannelTarget<T> 基类接受 ChannelWriter<T>；调用方负责创建 Channel 并保留 Reader。
// 子类只需实现 Transform(LogEntry) → T，指定如何将 LogEntry 转换为目标类型。
public sealed class DatabaseLogTarget : ChannelTarget<DatabaseLogRecord>
{
    private readonly string _connectionString;

    // 调用方传入 Channel 的写入端；读取端由调用方持有（见下方注册示例）
    public DatabaseLogTarget(string connectionString, ChannelWriter<DatabaseLogRecord> writer)
        : base(writer)
    {
        _connectionString = connectionString;
    }

    protected override DatabaseLogRecord Transform(LogEntry entry)
    {
        // 将 LogEntry 转换为数据库记录对象
        return new DatabaseLogRecord
        {
            Timestamp   = entry.Timestamp.UtcDateTime,
            Level       = entry.LogLevel.ToString(),
            Logger      = entry.LoggerName,
            Context     = entry.Context,
            Message     = entry.Message,       // 原始模板，也可渲染后存入
            Exception   = entry.Exception?.ToString(),
        };
    }
}

// 注册方式：调用方创建 Channel，分别传入 Writer（给 Target）和持有 Reader（后台消费）
// var channel  = Channel.CreateBounded<DatabaseLogRecord>(1000);
// var dbTarget = new DatabaseLogTarget("Server=...;", channel.Writer);
// var logger   = new LoggerBuilder()
//     .SetLoggerName("MyApp")
//     .AddSink(dbTarget)
//     .Build();
//
// // 后台消费：
// _ = Task.Run(async () =>
// {
//     await foreach (var record in channel.Reader.ReadAllAsync())
//     {
//         await using var conn = new SqlConnection(_connectionString);
//         await conn.ExecuteAsync("INSERT INTO Logs ...", record);
//     }
// });

public sealed class DatabaseLogRecord
{
    public DateTime Timestamp { get; set; }
    public string Level     { get; set; } = "";
    public string Logger    { get; set; } = "";
    public string Context   { get; set; } = "";
    public string Message   { get; set; } = "";
    public string? Exception { get; set; }
}

// ================================================================
//  第三节：IDestructurable — 高性能自定义解构（{@Object}）
//
//  比 JsonSerializer 回退路径快，且与 AOT 完全兼容。
//  通过 DestructureHelper（包装 Utf8JsonWriter）直接写入输出缓冲区，
//  无中间字符串分配。
//
//  优先级：IDestructurable > IDestructured > JsonSerializer 回退
// ================================================================
public sealed class Order : IDestructurable
{
    public int    Id     { get; init; }
    public string Name   { get; init; } = "";
    public decimal Amount { get; init; }

    public void Destructure(DestructureHelper helper)
    {
        helper.WriteStartObject();
        helper.WriteNumber("id",     Id);
        helper.WriteString("name",   Name);
        helper.WriteNumber("amount", (double)Amount);
        helper.WriteEndObject();
    }
}

/*  DestructureHelper 常用方法速查：

  | 方法                                  | 说明                            |
  |---------------------------------------|---------------------------------|
  | WriteStartObject() / WriteEndObject() | 开始/结束 JSON 对象             |
  | WriteStartArray()  / WriteEndArray()  | 开始/结束 JSON 数组             |
  | WriteString(key, value)               | 写入字符串字段                  |
  | WriteNumber(key, int/long/double)     | 写入数字字段                    |
  | WriteBoolean(key, bool)               | 写入布尔字段                    |
  | WriteNull(key)                        | 写入 null 字段                  |
  | WritePropertyName(key)                | 写入字段名（后接任意值方法）    |
  | WriteStringValue(value)               | 写入字符串数组元素              |
  | WriteNumberValue(int/long/double)     | 写入数字数组元素                |
*/

// 使用示例：
// var order = new Order { Id = 42, Name = "书", Amount = 99.9m };
// logger.Info("新订单 {@Order}", order);
// JSON 输出：{"id":42,"name":"书","amount":99.9}

// ================================================================
//  第四节：IDestructured — 极致零分配（预序列化字节）
//
//  适合静态或不可变数据：提前计算好 UTF-8 JSON 字节，
//  Emit 时直接嵌入输出缓冲区，无任何分配。
// ================================================================
public sealed class AppVersion : IDestructured
{
    // 静态只读字节，进程内复用
    private static readonly byte[] _bytes =
        System.Text.Encoding.UTF8.GetBytes("""{"major":1,"minor":2,"patch":0}""");

    public ReadOnlyMemory<byte> Destructured() => _bytes;
}

// 使用示例：
// logger.Info("应用版本 {@Version}", new AppVersion());
// JSON 输出：{"major":1,"minor":2,"patch":0}

// ================================================================
//  第五节：AOT（Native AOT / Trimming）配置
//
//  主库标记 IsAotCompatible=true，但 {$Object} 解构在 AOT 下依赖
//  JsonSerializer，需要提前注册 Source Generated JsonSerializerContext。
//  未注册的类型会静默降级为 ToString()，无运行时异常。
// ================================================================

// 步骤一：为需要 {@} 解构的类型创建 JsonSerializerContext
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(DatabaseLogRecord))]
[JsonSerializable(typeof(List<Order>))]
internal partial class MyAppJsonContext : JsonSerializerContext { }

// 步骤二：在 GlobalConfigurator 中注册（Build() 之前，只调用一次）
public static class AotConfigurationExample
{
    public static void Configure()
    {
        GlobalConfigurator.Configure()
            // 注册 Source Generated Context，AOT 下 {@Object} 解构走 Source Generated 路径
            .UseJsonTypeInfoResolver(MyAppJsonContext.Default)
            // 多个 Context 可在外部合并后传入：
            // .UseJsonTypeInfoResolver(
            //     JsonTypeInfoResolver.Combine(MyAppJsonContext.Default, AnotherContext.Default))
            .Apply();
    }
}

// ================================================================
//  第六节：LoggerManager — 运行时动态配置
//
//  所有通过 Build() 创建的 Logger 会自动注册到 LoggerManager。
//  可在运行时不重启的情况下更新 Sink 配置（FilterConfig、Enabled 等）。
//  Configuration 包的热更新功能底层即调用此 API。
// ================================================================
public static class LoggerManagerExample
{
    public static void RuntimeUpdate()
    {
        // ── 查看当前已注册的 Logger 名称 ─────────────────────────────────────
        var loggerNames = LoggerManager.GetLoggerList();
        // 返回：["Runtime", "Analytics", ...]

        // ── 更新单个 Logger 中的单个 Sink 配置 ───────────────────────────────
        // 场景：运行时临时开启某个 Sink 的 Debug 级别，排查生产问题
        LoggerManager.UpdateSinkConfig(
            loggerName: "Runtime",
            sinkName:   "console",           // 构建时通过 sinkName 参数指定的名称
            sinkConfig: new ConsoleSinkConfig
            {
                Enabled      = true,
                FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Debug },
            });

        // ── 用 LoggerConfig 更新单个 Logger 的全部 Sink 配置 ─────────────────
        // 适合从配置文件重新读取后全量替换
        LoggerManager.UpdateLoggerConfig(
            loggerConfigs: new LoggerConfig
            {
                LoggerName = "Runtime",
                ConsoleSinks = new Dictionary<string, ConsoleSinkConfig>
                {
                    ["console"] = new() { FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Info } }
                },
                FileSinks = new Dictionary<string, FileSinkConfig>
                {
                    ["main"] = new()
                    {
                        LogFilePath  = "Logs/Runtime.log",
                        MaxFileSizeMB = 10,
                        MaxFile      = 5,
                        FilterConfig = new FilterConfig { LogMinLevel = LogLevel.Info },
                    }
                }
            });

        // ── 用 LoggerConfig 列表批量更新所有已注册的 Logger ──────────────────
        // 典型用途：配置文件热更新时，将新配置全量下发
        // LoggerManager.UpdateAllLoggerConfig(newLoggerConfigs);

        // ── 注意事项 ──────────────────────────────────────────────────────────
        // • UpdateSinkConfig / UpdateLoggerConfig 只更新现有 Sink 的 FilterConfig 和 Enabled，
        //   不能新增或删除 Sink（Sink 列表在 Build() 时固定）。
        // • LogFilePath 等影响 Target 构造的参数无法热更新，需重启。
        // • 线程安全：所有方法均为原子操作（Interlocked.Exchange）。
    }
}
