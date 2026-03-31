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

namespace Lunarium.Logging.Http.Tests.Serializer;

/// <summary>
/// ClefSerializer CLEF/NDJSON 格式正确性验证。
/// 策略：将结果按 \n 拆分为行，每行 JsonDocument.Parse，逐字段断言。
/// </summary>
public class ClefSerializerTests
{
    private static readonly ClefSerializer Serializer = ClefSerializer.Default;

    private static async Task<JsonElement[]> SerializeToLinesAsync(IReadOnlyList<LogEntry> entries)
    {
        var content = Serializer.Serialize(entries);
        var text = await content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(text)) return [];
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                   .Select(line => JsonDocument.Parse(line).RootElement)
                   .ToArray();
    }

    private static async Task<JsonElement> SerializeOneLineAsync(LogEntry entry)
    {
        var lines = await SerializeToLinesAsync([entry]);
        return lines[0];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NDJSON 格式
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyBatch_ReturnsEmptyContent()
    {
        var content = Serializer.Serialize([]);
        var text = await content.ReadAsStringAsync();
        text.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleEntries_SeparatedByNewline_EachLineIsValidJson()
    {
        var lines = await SerializeToLinesAsync(EntryFactory.MakeBatch(3));
        lines.Length.Should().Be(3);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // @t 时间戳
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Timestamp_WrittenToAtT_InIso8601()
    {
        var ts = new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero);
        var e = await SerializeOneLineAsync(EntryFactory.Make(timestamp: ts));
        var str = e.GetProperty("@t").GetString();
        str.Should().Contain("2026-06-15");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // @l 级别
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Level_Info_AtLOmitted()
    {
        var e = await SerializeOneLineAsync(EntryFactory.Make(level: LogLevel.Info));
        e.TryGetProperty("@l", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(LogLevel.Debug,   "Debug")]
    [InlineData(LogLevel.Warning, "Warning")]
    [InlineData(LogLevel.Error,   "Error")]
    public async Task Level_NonInfo_AtLPresent(LogLevel level, string expected)
    {
        var e = await SerializeOneLineAsync(EntryFactory.Make(level: level));
        e.GetProperty("@l").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task Level_Critical_MapsToFatal()
    {
        var e = await SerializeOneLineAsync(EntryFactory.Make(level: LogLevel.Critical));
        e.GetProperty("@l").GetString().Should().Be("Fatal");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // @mt 消息模板
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MessageTemplate_WrittenRaw_NotRendered()
    {
        var entry = EntryFactory.Make("Hello {Name}", props: ["World"]);
        var e = await SerializeOneLineAsync(entry);
        // @mt 应是原始模板，不是渲染后的 "Hello World"
        e.GetProperty("@mt").GetString().Should().Be("Hello {Name}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // @x 异常
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Exception_Present_WrittenToAtX()
    {
        var ex = new ArgumentException("bad arg");
        var e = await SerializeOneLineAsync(EntryFactory.Make(ex: ex));
        e.GetProperty("@x").GetString().Should().Contain("bad arg");
    }

    [Fact]
    public async Task Exception_Absent_AtXOmitted()
    {
        var e = await SerializeOneLineAsync(EntryFactory.Make());
        e.TryGetProperty("@x", out _).Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 附加结构化字段
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoggerName_WrittenToLoggerNameField()
    {
        var e = await SerializeOneLineAsync(EntryFactory.Make(loggerName: "PaymentService"));
        e.GetProperty("LoggerName").GetString().Should().Be("PaymentService");
    }

    [Fact]
    public async Task Context_Present_WrittenToContextField()
    {
        var e = await SerializeOneLineAsync(EntryFactory.Make(context: "Order.Handler"));
        e.GetProperty("Context").GetString().Should().Be("Order.Handler");
    }

    [Fact]
    public async Task Context_Empty_ContextFieldOmitted()
    {
        var e = await SerializeOneLineAsync(EntryFactory.Make(context: ""));
        e.TryGetProperty("Context", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Scope_Present_WrittenToScopeField()
    {
        var e = await SerializeOneLineAsync(EntryFactory.Make(scope: "req-123"));
        e.GetProperty("Scope").GetString().Should().Be("req-123");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Properties 平铺到顶级
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Properties_FlattenedToTopLevel_NoNestedObject()
    {
        var entry = EntryFactory.Make("User {Id} logged in", props: [42]);
        var e = await SerializeOneLineAsync(entry);
        // 不应有嵌套的 "properties" 对象
        e.TryGetProperty("properties", out _).Should().BeFalse();
        // 属性直接在顶级
        e.GetProperty("Id").GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task Properties_Stringify_WrittenAsString()
    {
        var entry = EntryFactory.Make("{$Count}", props: [99]);
        var e = await SerializeOneLineAsync(entry);
        var prop = e.GetProperty("Count");
        prop.ValueKind.Should().Be(JsonValueKind.String);
        prop.GetString().Should().Be("99");
    }

    [Fact]
    public async Task Properties_Destructure_IDestructured_EmbeddedAsObject()
    {
        var entry = EntryFactory.Make("{@Data}", props: [new RawJsonObj()]);
        var e = await SerializeOneLineAsync(entry);
        e.GetProperty("Data").GetProperty("raw").GetInt32().Should().Be(7);
    }

    private sealed class RawJsonObj : IDestructured
    {
        public ReadOnlyMemory<byte> Destructured() => "{\"raw\":7}"u8.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Content-Type
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ContentType_IsClef()
    {
        var content = Serializer.Serialize([]);
        content.Headers.ContentType!.MediaType.Should().Be("application/vnd.serilog.clef");
    }
}
