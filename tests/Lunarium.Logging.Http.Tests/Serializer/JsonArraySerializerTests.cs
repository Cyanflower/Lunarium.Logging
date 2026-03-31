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
/// JsonArraySerializer 格式正确性验证。
/// 策略：序列化 → 读取 HttpContent → JsonDocument.Parse → 断言字段。
/// </summary>
public class JsonArraySerializerTests
{
    private static readonly JsonArraySerializer Serializer = JsonArraySerializer.Default;

    private static async Task<JsonDocument> SerializeOneAsync(LogEntry entry)
    {
        var content = Serializer.Serialize([entry]);
        var json = await content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        return doc;
    }

    private static JsonElement GetFirst(JsonDocument doc) =>
        doc.RootElement[0];

    // ─────────────────────────────────────────────────────────────────────────
    // 基础结构
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyBatch_ReturnsEmptyJsonArray()
    {
        var content = Serializer.Serialize([]);
        var json = await content.ReadAsStringAsync();
        json.Trim().Should().Be("[]");
    }

    [Fact]
    public async Task MultipleBatch_ArrayLengthMatchesEntries()
    {
        var content = Serializer.Serialize(EntryFactory.MakeBatch(3));
        var json = await content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(3);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 必填字段
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Entry_RequiredFields_Present()
    {
        using var doc = await SerializeOneAsync(EntryFactory.Make());
        var e = GetFirst(doc);
        e.TryGetProperty("timestamp", out _).Should().BeTrue();
        e.TryGetProperty("level", out _).Should().BeTrue();
        e.TryGetProperty("logger", out _).Should().BeTrue();
        e.TryGetProperty("message", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Entry_Logger_MatchesLoggerName()
    {
        using var doc = await SerializeOneAsync(EntryFactory.Make(loggerName: "MyService"));
        GetFirst(doc).GetProperty("logger").GetString().Should().Be("MyService");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 可选字段
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Entry_EmptyContext_Omitted()
    {
        using var doc = await SerializeOneAsync(EntryFactory.Make(context: ""));
        GetFirst(doc).TryGetProperty("context", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Entry_NonEmptyContext_Present()
    {
        using var doc = await SerializeOneAsync(EntryFactory.Make(context: "Order.Service"));
        GetFirst(doc).GetProperty("context").GetString().Should().Be("Order.Service");
    }

    [Fact]
    public async Task Entry_NoException_ExceptionOmitted()
    {
        using var doc = await SerializeOneAsync(EntryFactory.Make());
        GetFirst(doc).TryGetProperty("exception", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Entry_WithException_ExceptionPresent()
    {
        var ex = new InvalidOperationException("oops");
        using var doc = await SerializeOneAsync(EntryFactory.Make(ex: ex));
        GetFirst(doc).GetProperty("exception").GetString().Should().Contain("oops");
    }

    [Fact]
    public async Task Entry_EmptyScope_Omitted()
    {
        using var doc = await SerializeOneAsync(EntryFactory.Make(scope: ""));
        GetFirst(doc).TryGetProperty("scope", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Entry_NonEmptyScope_Present()
    {
        using var doc = await SerializeOneAsync(EntryFactory.Make(scope: "RequestScope"));
        GetFirst(doc).GetProperty("scope").GetString().Should().Be("RequestScope");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 级别映射
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LogLevel.Debug,    "Debug")]
    [InlineData(LogLevel.Info,     "Information")]
    [InlineData(LogLevel.Warning,  "Warning")]
    [InlineData(LogLevel.Error,    "Error")]
    [InlineData(LogLevel.Critical, "Critical")]
    public async Task Entry_LevelMapping_Correct(LogLevel level, string expected)
    {
        using var doc = await SerializeOneAsync(EntryFactory.Make(level: level));
        GetFirst(doc).GetProperty("level").GetString().Should().Be(expected);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 消息渲染
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Message_NoTokens_OutputsRawString()
    {
        using var doc = await SerializeOneAsync(EntryFactory.Make("plain message"));
        GetFirst(doc).GetProperty("message").GetString().Should().Be("plain message");
    }

    [Fact]
    public async Task Message_WithPropertyToken_RenderedCorrectly()
    {
        var entry = EntryFactory.Make("Hello {Name}", props: ["World"]);
        using var doc = await SerializeOneAsync(entry);
        GetFirst(doc).GetProperty("message").GetString().Should().Be("Hello World");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Properties 类型
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Properties_Null_WrittenAsJsonNull()
    {
        var entry = EntryFactory.Make("{Val}", props: [null]);
        using var doc = await SerializeOneAsync(entry);
        GetFirst(doc).GetProperty("properties").GetProperty("Val").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Properties_Bool_WrittenAsJsonBoolean()
    {
        var entry = EntryFactory.Make("{Val}", props: [true]);
        using var doc = await SerializeOneAsync(entry);
        GetFirst(doc).GetProperty("properties").GetProperty("Val").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData(42)]
    [InlineData(42L)]
    [InlineData(42u)]
    [InlineData(42ul)]
    public async Task Properties_IntegerTypes_WrittenAsNumber(object value)
    {
        var entry = EntryFactory.Make("{Val}", props: [value]);
        using var doc = await SerializeOneAsync(entry);
        GetFirst(doc).GetProperty("properties").GetProperty("Val").ValueKind
            .Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public async Task Properties_FiniteDouble_WrittenAsNumber()
    {
        var entry = EntryFactory.Make("{Val}", props: [3.14]);
        using var doc = await SerializeOneAsync(entry);
        GetFirst(doc).GetProperty("properties").GetProperty("Val").ValueKind
            .Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public async Task Properties_InfiniteDouble_WrittenAsString()
    {
        var entry = EntryFactory.Make("{Val}", props: [double.PositiveInfinity]);
        using var doc = await SerializeOneAsync(entry);
        GetFirst(doc).GetProperty("properties").GetProperty("Val").ValueKind
            .Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task Properties_InfiniteFloat_WrittenAsString()
    {
        var entry = EntryFactory.Make("{Val}", props: [float.NegativeInfinity]);
        using var doc = await SerializeOneAsync(entry);
        GetFirst(doc).GetProperty("properties").GetProperty("Val").ValueKind
            .Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task Properties_Guid_WrittenAsString()
    {
        var guid = Guid.NewGuid();
        var entry = EntryFactory.Make("{Val}", props: [guid]);
        using var doc = await SerializeOneAsync(entry);
        var str = GetFirst(doc).GetProperty("properties").GetProperty("Val").GetString();
        Guid.Parse(str!).Should().Be(guid);
    }

    [Fact]
    public async Task Properties_DateTimeOffset_WrittenAsIso8601String()
    {
        var dt = new DateTimeOffset(2026, 3, 15, 10, 30, 0, TimeSpan.Zero);
        var entry = EntryFactory.Make("{Val}", props: [dt]);
        using var doc = await SerializeOneAsync(entry);
        var str = GetFirst(doc).GetProperty("properties").GetProperty("Val").GetString();
        str.Should().Contain("2026-03-15");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stringify {$Value}
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Properties_Stringify_PrimitiveWrittenAsString()
    {
        // {$Val} 应将 int 42 写为字符串 "42"，而非数字
        var entry = EntryFactory.Make("{$Val}", props: [42]);
        using var doc = await SerializeOneAsync(entry);
        var prop = GetFirst(doc).GetProperty("properties").GetProperty("Val");
        prop.ValueKind.Should().Be(JsonValueKind.String);
        prop.GetString().Should().Be("42");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Destructure {@Object}
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class MyDestructured : IDestructured
    {
        public ReadOnlyMemory<byte> Destructured() =>
            "{\"x\":1}"u8.ToArray();
    }

    private sealed class MyDestructurable : IDestructurable
    {
        public void Destructure(DestructureHelper helper)
        {
            helper.WriteStartObject();
            helper.WriteNumber("y", 2);
            helper.WriteEndObject();
        }
    }

    [Fact]
    public async Task Properties_Destructure_IDestructured_RawJsonEmbedded()
    {
        var entry = EntryFactory.Make("{@Val}", props: [new MyDestructured()]);
        using var doc = await SerializeOneAsync(entry);
        var prop = GetFirst(doc).GetProperty("properties").GetProperty("Val");
        prop.ValueKind.Should().Be(JsonValueKind.Object);
        prop.GetProperty("x").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Properties_Destructure_IDestructurable_CallsDestructure()
    {
        var entry = EntryFactory.Make("{@Val}", props: [new MyDestructurable()]);
        using var doc = await SerializeOneAsync(entry);
        var prop = GetFirst(doc).GetProperty("properties").GetProperty("Val");
        prop.ValueKind.Should().Be(JsonValueKind.Object);
        prop.GetProperty("y").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Properties_Destructure_PlainObject_FallsBackToJsonSerializer()
    {
        var entry = EntryFactory.Make("{@Val}", props: [new { Z = 3 }]);
        using var doc = await SerializeOneAsync(entry);
        var prop = GetFirst(doc).GetProperty("properties").GetProperty("Val");
        prop.ValueKind.Should().Be(JsonValueKind.Object);
        prop.GetProperty("Z").GetInt32().Should().Be(3);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Content-Type
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ContentType_IsApplicationJson()
    {
        var content = Serializer.Serialize([]);
        content.Headers.ContentType!.MediaType.Should().Be("application/json");
        content.Headers.ContentType.CharSet.Should().Be("utf-8");
    }
}
