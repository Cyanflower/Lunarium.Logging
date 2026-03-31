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
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Writer;

namespace Lunarium.Logging.Benchmarks;

/// <summary>
/// 测量渲染层 (LogWriter) 的性能。
///
/// 关注点：
/// - 三种 Writer 的渲染速度对比：Text / ColorText / JSON
/// - 不同消息复杂度（无属性 / 单属性 / 多属性 / 对齐+格式化）的渲染开销
/// - WriterPool 对象池的池化收益（池化 vs 直接 new）
///
/// 所有 Benchmark 均写入 TextWriter.Null，排除 I/O 干扰。
/// </summary>
[MemoryDiagnoser]
public class LogWriterBenchmarks
{
    // IDestructured 实现：预序列化字节，静态单例，零分配
    private sealed class CachedPayload : IDestructured
    {
        public static readonly CachedPayload Instance = new();
        private static readonly ReadOnlyMemory<byte> _bytes =
            "{\"Id\":1,\"Name\":\"Test\",\"Tags\":[\"tag1\",\"tag2\"]}"u8.ToArray();
        public ReadOnlyMemory<byte> Destructured() => _bytes;
    }

    private LogEntry _entryPlainText = null!;
    private LogEntry _entrySingleProp = null!;
    private LogEntry _entryMultiProp = null!;
    private LogEntry _entryWithFormatAlign = null!;
    private LogEntry _entryNumeric = null!;
    private LogEntry _entryComplex = null!;
    private LogEntry _entryIDestructured = null!;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelper.EnsureGlobalConfig();
        var ts = DateTimeOffset.UtcNow;

        _entryPlainText = new LogEntry(
            loggerName: "Bench",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Bench"),
            timestamp: ts,
            logLevel: LogLevel.Info,
            message: "Application started successfully",
            properties: Array.Empty<object?>(),
            context: "Bench",
            contextBytes: "Bench"u8.ToArray(),
            scope: "",
            messageTemplate: LogParser.ParseMessage("Application started successfully"));

        _entrySingleProp = new LogEntry(
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

        _entryMultiProp = new LogEntry(
            loggerName: "Bench",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Bench"),
            timestamp: ts,
            logLevel: LogLevel.Info,
            message: "Request {Method} {Url} completed in {Duration}ms with status {Status}",
            properties: new object?[] { "GET", "/api/users", 42, 200 },
            context: "Http.Middleware",
            contextBytes: "Http.Middleware"u8.ToArray(),
            scope: "",
            messageTemplate: LogParser.ParseMessage("Request {Method} {Url} completed in {Duration}ms with status {Status}"));

        _entryWithFormatAlign = new LogEntry(
            loggerName: "Bench",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Bench"),
            timestamp: ts,
            logLevel: LogLevel.Warning,
            message: "{Count,8:D} items processed, {Percent:P1} complete",
            properties: new object?[] { 12345, 0.8765 },
            context: "Worker",
            contextBytes: "Worker"u8.ToArray(),
            scope: "",
            messageTemplate: LogParser.ParseMessage("{Count,8:D} items processed, {Percent:P1} complete"));

        _entryNumeric = new LogEntry(
            loggerName: "Bench",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Bench"),
            timestamp: ts,
            logLevel: LogLevel.Info,
            message: "Process {Id} at {Time} spent {Duration}",
            properties: new object?[] { 12345, ts, TimeSpan.FromMilliseconds(420) },
            context: "System",
            contextBytes: "System"u8.ToArray(),
            scope: "",
            messageTemplate: LogParser.ParseMessage("Process {Id} at {Time} spent {Duration}"));

        _entryComplex = new LogEntry(
            loggerName: "Bench",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Bench"),
            timestamp: ts,
            logLevel: LogLevel.Info,
            message: "Request context: {@Payload}",
            properties: new object?[] { new { Id = 1, Name = "Test", Tags = new[] { "tag1", "tag2" } } },
            context: "API",
            contextBytes: "API"u8.ToArray(),
            scope: "",
            messageTemplate: LogParser.ParseMessage("Request context: {@Payload}"));

        _entryIDestructured = new LogEntry(
            loggerName: "Bench",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Bench"),
            timestamp: ts,
            logLevel: LogLevel.Info,
            message: "Request context: {@Payload}",
            properties: new object?[] { CachedPayload.Instance },
            context: "API",
            contextBytes: "API"u8.ToArray(),
            scope: "",
            messageTemplate: LogParser.ParseMessage("Request context: {@Payload}"));
    }

    // ==================== LogTextWriter ====================

    [Benchmark(Baseline = true, Description = "Text: 纯文本消息（无属性）")]
    public void Text_PlainText()
    {
        var w = WriterPool.Get<LogTextWriter>();
        try { w.Render(_entryPlainText); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    [Benchmark(Description = "Text: 单属性")]
    public void Text_SingleProperty()
    {
        var w = WriterPool.Get<LogTextWriter>();
        try { w.Render(_entrySingleProp); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    [Benchmark(Description = "Text: 四属性")]
    public void Text_MultiProperty()
    {
        var w = WriterPool.Get<LogTextWriter>();
        try { w.Render(_entryMultiProp); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    [Benchmark(Description = "Text: 对齐 + 格式化 ({Count,8:D} {Percent:P1})")]
    public void Text_AlignmentAndFormat()
    {
        var w = WriterPool.Get<LogTextWriter>();
        try { w.Render(_entryWithFormatAlign); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    [Benchmark(Description = "Text: Numeric/Formattable (int, DateTimeOffset, TimeSpan)")]
    public void Text_Numeric()
    {
        var w = WriterPool.Get<LogTextWriter>();
        try { w.Render(_entryNumeric); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    // ==================== LogColorTextWriter ====================

    [Benchmark(Description = "Color: 单属性（含 ANSI 颜色转义代码）")]
    public void Color_SingleProperty()
    {
        var w = WriterPool.Get<LogColorTextWriter>();
        try { w.Render(_entrySingleProp); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    [Benchmark(Description = "Color: 四属性")]
    public void Color_MultiProperty()
    {
        var w = WriterPool.Get<LogColorTextWriter>();
        try { w.Render(_entryMultiProp); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    [Benchmark(Description = "Color: 对齐 + 格式化 ({Count,8:D} {Percent:P1})")]
    public void Color_AlignmentAndFormat()
    {
        var w = WriterPool.Get<LogColorTextWriter>();
        try { w.Render(_entryWithFormatAlign); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    [Benchmark(Description = "Color: Numeric/Formattable (int, DateTimeOffset, TimeSpan)")]
    public void Color_Numeric()
    {
        var w = WriterPool.Get<LogColorTextWriter>();
        try { w.Render(_entryNumeric); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    // ==================== LogJsonWriter ====================

    [Benchmark(Description = "JSON: 单属性（含 RenderedMessage + Propertys 字段）")]
    public void Json_SingleProperty()
    {
        var w = WriterPool.Get<LogJsonWriter>();
        try { w.Render(_entrySingleProp); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    [Benchmark(Description = "JSON: 四属性")]
    public void Json_MultiProperty()
    {
        var w = WriterPool.Get<LogJsonWriter>();
        try { w.Render(_entryMultiProp); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    [Benchmark(Description = "JSON: Numeric/Formattable")]
    public void Json_Numeric()
    {
        var w = WriterPool.Get<LogJsonWriter>();
        try { w.Render(_entryNumeric); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    [Benchmark(Description = "JSON: {@Payload} 匿名类型（JsonSerializer.Serialize 回退路径）")]
    public void Json_ComplexObject()
    {
        var w = WriterPool.Get<LogJsonWriter>();
        try { w.Render(_entryComplex); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    [Benchmark(Description = "JSON: {@Payload} IDestructured（预序列化字节，WriteRawValue 零分配路径）")]
    public void Json_Destructure_IDestructured()
    {
        var w = WriterPool.Get<LogJsonWriter>();
        try { w.Render(_entryIDestructured); w.FlushTo(Stream.Null); }
        finally { w.Return(); }
    }

    // ==================== WriterPool 对象池收益 ====================

    [Benchmark(Description = "Pool: WriterPool.Get<LogTextWriter>() + Return()（池化路径）")]
    public object Pool_GetAndReturn()
    {
        var w = WriterPool.Get<LogTextWriter>();
        WriterPool.Return(w);
        return w;
    }

    [Benchmark(Description = "Alloc: new LogTextWriter()（直接分配，用于对比池化收益）")]
    public object Alloc_NewWriter() => new LogTextWriter();
}
