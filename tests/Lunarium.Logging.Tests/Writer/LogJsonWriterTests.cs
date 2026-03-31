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

using System.Text.Json;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Writer;

namespace Lunarium.Logging.Tests.Writer;

/// <summary>
/// Tests for LogJsonWriter — JSON log rendering.
///
/// Strategy: render a LogEntry through LogJsonWriter, parse the resulting
/// JSON string with System.Text.Json, and assert on the parsed document.
/// This avoids fragile string matching while precisely verifying the JSON
/// structure.
/// </summary>
public class LogJsonWriterTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static LogEntry MakeEntry(
        string message,
        object?[]? props = null,
        LogLevel level = LogLevel.Info,
        string context = "",
        Exception? ex = null)
    {
        var entry = new LogEntry(
            loggerName: "Test",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Test"),
            timestamp: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            logLevel: level,
            message: message,
            properties: props ?? [],
            context: context,
            contextBytes: System.Text.Encoding.UTF8.GetBytes(context),
            scope: "",
            messageTemplate: LogParser.EmptyMessageTemplate,
            exception: ex);
        entry.ParseMessage();
        return entry;
    }

    private static JsonDocument RenderJson(LogEntry entry)
    {
        var writer = WriterPool.Get<LogJsonWriter>();
        try
        {
            writer.Render(entry);
            var raw = writer.ToString().TrimEnd('\r', '\n');
            return JsonDocument.Parse(raw);
        }
        finally
        {
            writer.Return();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. JSON structural validity
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_Output_IsValidJson()
    {
        var entry = MakeEntry("Hello world");
        // Should not throw
        Action act = () => RenderJson(entry).Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Render_JsonObject_ContainsTimestampField()
    {
        var entry = MakeEntry("msg");
        using var doc = RenderJson(entry);
        doc.RootElement.TryGetProperty("Timestamp", out _).Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Level field
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LogLevel.Debug, "Debug", 0)]
    [InlineData(LogLevel.Info, "Information", 1)]
    [InlineData(LogLevel.Warning, "Warning", 2)]
    [InlineData(LogLevel.Error, "Error", 3)]
    [InlineData(LogLevel.Critical, "Critical", 4)]
    public void Render_LevelField_CorrectStringAndInt(LogLevel level, string expectedStr, int expectedInt)
    {
        var entry = MakeEntry("msg", level: level);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("Level").GetString().Should().Be(expectedStr);
        doc.RootElement.GetProperty("LogLevel").GetInt32().Should().Be(expectedInt);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Context field
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithContext_ContextFieldPresent()
    {
        var entry = MakeEntry("msg", context: "MyService");
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("Context").GetString().Should().Be("MyService");
    }

    [Fact]
    public void Render_EmptyContext_NoContextField()
    {
        var entry = MakeEntry("msg", context: "");
        using var doc = RenderJson(entry);
        doc.RootElement.TryGetProperty("Context", out _).Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. OriginalMessage field (template before rendering)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_OriginalMessage_ContainsPlaceholder()
    {
        var entry = MakeEntry("Hello {Name}", props: ["Alice"]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("OriginalMessage").GetString().Should().Be("Hello {Name}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. RenderedMessage field (template after substitution)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_RenderedMessage_PropertySubstituted()
    {
        var entry = MakeEntry("Hello {Name}", props: ["Alice"]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("RenderedMessage").GetString().Should().Be("Hello Alice");
    }

    [Fact]
    public void Render_RenderedMessage_NoProperties_UsesRawPlaceholder()
    {
        var entry = MakeEntry("Hello {Name}", props: []);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("RenderedMessage").GetString().Should().Be("Hello {Name}");
    }

    [Fact]
    public void Render_RenderedMessage_MultipleProperties_AllSubstituted()
    {
        var entry = MakeEntry("{A} + {B}", props: [1, 2]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("RenderedMessage").GetString().Should().Be("1 + 2");
    }

    [Fact]
    public void Render_RenderedMessage_NullProperty_RendersAsNull()
    {
        var entry = MakeEntry("val={V}", props: [null]);
        using var doc = RenderJson(entry);
        // "null" (literal) in JSON string content
        doc.RootElement.GetProperty("RenderedMessage").GetString().Should().Be("val=null");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Propertys field — named property values
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_PropertysField_StringValue()
    {
        var entry = MakeEntry("Hi {Name}", props: ["Bob"]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("Propertys").GetProperty("Name").GetString()
            .Should().Be("Bob");
    }

    [Fact]
    public void Render_PropertysField_IntValue()
    {
        var entry = MakeEntry("Count={N}", props: [42]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("Propertys").GetProperty("N").GetInt32()
            .Should().Be(42);
    }

    [Fact]
    public void Render_PropertysField_BoolValue()
    {
        var entry = MakeEntry("Flag={F}", props: [true]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("Propertys").GetProperty("F").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    public void Render_PropertysField_LongValue()
    {
        var entry = MakeEntry("Big={N}", props: [long.MaxValue]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("Propertys").GetProperty("N").GetInt64()
            .Should().Be(long.MaxValue);
    }

    [Fact]
    public void Render_PropertysField_NullValue_IsJsonNull()
    {
        var entry = MakeEntry("V={V}", props: [null]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("Propertys").GetProperty("V").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Render_PropertysField_DoubleNaN_IsStringNaN()
    {
        var entry = MakeEntry("V={V}", props: [double.NaN]);
        using var doc = RenderJson(entry);
        // NaN is not valid JSON number — rendered as "NaN" string
        doc.RootElement.GetProperty("Propertys").GetProperty("V").GetString()
            .Should().Be("NaN");
    }

    [Fact]
    public void Render_PropertysField_DoubleInfinity_IsStringInfinity()
    {
        var entry = MakeEntry("V={V}", props: [double.PositiveInfinity]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("Propertys").GetProperty("V").GetString()
            .Should().Be("Infinity");
    }

    [Fact]
    public void Render_PropertysField_FloatNegativeInfinity_IsStringNegInfinity()
    {
        var entry = MakeEntry("V={V}", props: [float.NegativeInfinity]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("Propertys").GetProperty("V").GetString()
            .Should().Be("-Infinity");
    }

    [Fact]
    public void Render_PropertysField_GuidValue_IsString()
    {
        var g = Guid.NewGuid();
        var entry = MakeEntry("G={G}", props: [g]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("Propertys").GetProperty("G").GetString()
            .Should().Be(g.ToString());
    }

    [Fact]
    public void Render_PropertysField_NoProperties_EmptyObject()
    {
        var entry = MakeEntry("plain text");
        using var doc = RenderJson(entry);
        var props = doc.RootElement.GetProperty("Propertys");
        props.EnumerateObject().Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Destructuring (@) — produces nested JSON object in Propertys
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_DestructurePrefix_PropertyIsJsonObject()
    {
        var entry = MakeEntry("{@Obj}", props: [new { Name = "Alice", Age = 30 }]);
        using var doc = RenderJson(entry);
        var obj = doc.RootElement.GetProperty("Propertys").GetProperty("Obj");
        obj.ValueKind.Should().Be(JsonValueKind.Object);
        obj.GetProperty("Name").GetString().Should().Be("Alice");
        obj.GetProperty("Age").GetInt32().Should().Be(30);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. Exception field
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithException_ExceptionFieldPresent()
    {
        var ex = new InvalidOperationException("boom");
        var entry = MakeEntry("msg", ex: ex);
        using var doc = RenderJson(entry);
        doc.RootElement.TryGetProperty("Exception", out var exProp).Should().BeTrue();
        exProp.GetString().Should().Contain("InvalidOperationException");
        exProp.GetString().Should().Contain("boom");
    }

    [Fact]
    public void Render_NoException_NoExceptionField()
    {
        var entry = MakeEntry("msg");
        using var doc = RenderJson(entry);
        doc.RootElement.TryGetProperty("Exception", out _).Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. JSON string escaping — special characters in messages
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_MessageWithDoubleQuote_EscapedInJson()
    {
        var entry = MakeEntry("Say \"hello\"");
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("RenderedMessage").GetString()
            .Should().Be("Say \"hello\"");
    }

    [Fact]
    public void Render_MessageWithBackslash_EscapedInJson()
    {
        var entry = MakeEntry(@"Path: C:\Users\test");
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("RenderedMessage").GetString()
            .Should().Be(@"Path: C:\Users\test");
    }

    [Fact]
    public void Render_MessageWithNewline_EscapedInJson()
    {
        var entry = MakeEntry("Line1\nLine2");
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("RenderedMessage").GetString()
            .Should().Be("Line1\nLine2");
    }

    [Fact]
    public void Render_MessageWithChinese_PassedThroughDirectly()
    {
        // Chinese characters must NOT be \uXXXX-escaped (RFC 8259 supports UTF-8 directly)
        var entry = MakeEntry("你好，世界");
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("RenderedMessage").GetString()
            .Should().Be("你好，世界");
    }

    [Fact]
    public void Render_MessageWithEmoji_HandledAsSurrogatePair()
    {
        var entry = MakeEntry("Hello 😀");
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("RenderedMessage").GetString()
            .Should().Be("Hello 😀");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. Format specifier in RenderedMessage
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_RenderedMessage_StringProperty_Substituted()
    {
        // Simple string property substituted in RenderedMessage (no format specifier)
        var entry = MakeEntry("Hello {User}", props: ["Alice"]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("RenderedMessage").GetString()
            .Should().Be("Hello Alice");
    }

    [Fact]
    public void Render_PropertysField_DecimalValue()
    {
        // decimal maps to JSON number in Propertys
        var entry = MakeEntry("V={V}", props: [3.14m]);
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("Propertys").GetProperty("V").GetDecimal()
            .Should().Be(3.14m);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. Multiple properties — Propertys object has all keys
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_MultipleProperties_AllKeysInPropertysObject()
    {
        var entry = MakeEntry("{A} {B} {C}", props: ["x", 2, true]);
        using var doc = RenderJson(entry);
        var props = doc.RootElement.GetProperty("Propertys");
        props.GetProperty("A").GetString().Should().Be("x");
        props.GetProperty("B").GetInt32().Should().Be(2);
        props.GetProperty("C").GetBoolean().Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12. LoggerName field
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithLoggerName_ContainsLoggerNameField()
    {
        var entry = MakeEntry("msg"); // loggerName = "Test"
        using var doc = RenderJson(entry);
        doc.RootElement.GetProperty("LoggerName").GetString().Should().Be("Test");
    }
}
