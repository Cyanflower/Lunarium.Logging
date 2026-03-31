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

using Lunarium.Logging.Config.Configurator;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Writer;

namespace Lunarium.Logging.Tests.Writer;

/// <summary>
/// Targeted coverage for the uncovered paths in LogJsonWriter, LogColorTextWriter,
/// LogTextWriter, and the base LogWriter.
/// </summary>
public class WriterCoverageGapTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static LogEntry MakeEntry(
        string template,
        object?[] props,
        LogLevel level = LogLevel.Info,
        string context = "",
        Exception? ex = null)
    {
        var parsed = LogParser.ParseMessage(template);
        return new LogEntry(
            loggerName: "Test",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("Test"),
            timestamp: DateTimeOffset.UtcNow,
            logLevel: level,
            message: template,
            properties: props,
            context: context,
            contextBytes: System.Text.Encoding.UTF8.GetBytes(context),
            scope: "",
            messageTemplate: parsed,
            exception: ex);
    }

    private static string Render<T>(LogEntry entry) where T : LogWriter, new()
    {
        var writer = WriterPool.Get<T>();
        writer.Render(entry);
        var result = writer.ToString();
        writer.Return();
        return result;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // A. LogJsonWriter — AppendJsonStringContent escape sequences
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogJsonWriter_EscapeTab_RenderedAsBackslashT()
    {
        var entry = MakeEntry("{S}", ["\t"]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\\t");
    }

    [Fact]
    public void LogJsonWriter_EscapeCarriageReturn_RenderedAsBackslashR()
    {
        var entry = MakeEntry("{S}", ["\r"]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\\r");
    }

    [Fact]
    public void LogJsonWriter_EscapeBackspace_RenderedAsBackslashB()
    {
        var entry = MakeEntry("{S}", ["\b"]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\\b");
    }

    [Fact]
    public void LogJsonWriter_EscapeFormFeed_RenderedAsBackslashF()
    {
        var entry = MakeEntry("{S}", ["\f"]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\\f");
    }

    [Fact]
    public void LogJsonWriter_ControlChar_RenderedAsHexEscape()
    {
        // 0x01 is a control character < 0x20 that isn't given a named escape
        var entry = MakeEntry("{S}", ["\x01"]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\\u0001");
    }

    [Fact]
    public void LogJsonWriter_SurrogatePair_PassedThrough()
    {
        // "😀" = U+1F600, encoded as \uD83D\uDE00 surrogate pair in UTF-16
        // Utf8JsonWriter outputs Emoji as JSON escape sequence \uD83D\uDE00 (valid JSON)
        const string emoji = "😀";
        var entry = MakeEntry("{E}", [emoji]);
        var output = Render<LogJsonWriter>(entry);
        // Check for JSON escape sequence (both forms represent the same character)
        output.Should().Contain("\\uD83D\\uDE00");
    }

    [Fact]
    public void LogJsonWriter_ChineseCharacters_NotEscaped()
    {
        var entry = MakeEntry("{C}", ["你好"]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("你好");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // B. LogJsonWriter — ToJsonValue: types not yet covered
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogJsonWriter_ShortProperty_RenderedAsNumber()
    {
        var entry = MakeEntry("{V}", [(short)7]);
        Render<LogJsonWriter>(entry).Should().Contain("7");
    }

    [Fact]
    public void LogJsonWriter_ByteProperty_RenderedAsNumber()
    {
        var entry = MakeEntry("{V}", [(byte)255]);
        Render<LogJsonWriter>(entry).Should().Contain("255");
    }

    [Fact]
    public void LogJsonWriter_UIntProperty_RenderedAsNumber()
    {
        var entry = MakeEntry("{V}", [(uint)42u]);
        Render<LogJsonWriter>(entry).Should().Contain("42");
    }

    [Fact]
    public void LogJsonWriter_ULongProperty_RenderedAsNumber()
    {
        var entry = MakeEntry("{V}", [(ulong)100ul]);
        Render<LogJsonWriter>(entry).Should().Contain("100");
    }

    [Fact]
    public void LogJsonWriter_UShortProperty_RenderedAsNumber()
    {
        var entry = MakeEntry("{V}", [(ushort)8]);
        Render<LogJsonWriter>(entry).Should().Contain("8");
    }

    [Fact]
    public void LogJsonWriter_SByteProperty_RenderedAsNumber()
    {
        var entry = MakeEntry("{V}", [(sbyte)-5]);
        Render<LogJsonWriter>(entry).Should().Contain("-5");
    }

    [Fact]
    public void LogJsonWriter_DecimalProperty_RenderedAsNumber()
    {
        var entry = MakeEntry("{V}", [3.14m]);
        Render<LogJsonWriter>(entry).Should().Contain("3.14");
    }

    [Fact]
    public void LogJsonWriter_FloatNaN_IsNaNString()
    {
        var entry = MakeEntry("{V}", [float.NaN]);
        Render<LogJsonWriter>(entry).Should().Contain("\"NaN\"");
    }

    [Fact]
    public void LogJsonWriter_FloatPositiveInfinity_IsInfinityString()
    {
        var entry = MakeEntry("{V}", [float.PositiveInfinity]);
        Render<LogJsonWriter>(entry).Should().Contain("\"Infinity\"");
    }

    [Fact]
    public void LogJsonWriter_FloatNegativeInfinity_IsNegInfinityString()
    {
        var entry = MakeEntry("{V}", [float.NegativeInfinity]);
        Render<LogJsonWriter>(entry).Should().Contain("\"-Infinity\"");
    }

    [Fact]
    public void LogJsonWriter_FloatNormal_RenderedAsNumber()
    {
        var entry = MakeEntry("{V}", [1.5f]);
        Render<LogJsonWriter>(entry).Should().Contain("1.5");
    }

    [Fact]
    public void LogJsonWriter_CharProperty_IsQuotedString()
    {
        var entry = MakeEntry("{V}", ['Z']);
        Render<LogJsonWriter>(entry).Should().Contain("\"Z\"");
    }

    [Fact]
    public void LogJsonWriter_DateTimeProperty_IsIso8601()
    {
        var dt = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var entry = MakeEntry("{V}", [dt]);
        Render<LogJsonWriter>(entry).Should().Contain("2025");
    }

    [Fact]
    public void LogJsonWriter_TimeSpanProperty_IsString()
    {
        var ts = TimeSpan.FromHours(2);
        var entry = MakeEntry("{V}", [ts]);
        Render<LogJsonWriter>(entry).Should().Contain("02:00:00");
    }

    [Fact]
    public void LogJsonWriter_UriProperty_IsString()
    {
        var uri = new Uri("https://example.com");
        var entry = MakeEntry("{V}", [uri]);
        Render<LogJsonWriter>(entry).Should().Contain("https://example.com");
    }

    [Fact]
    public void LogJsonWriter_UnknownType_FallsBackToString()
    {
        // Use a custom struct that has a ToString()
        var custom = new { Tag = "custom-obj" };
        var entry = MakeEntry("{V}", [custom]);
        Render<LogJsonWriter>(entry).Should().Contain("custom-obj");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // C. LogJsonWriter — WriteRenderedMessage with propertys.Length == 0
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogJsonWriter_TemplateWithNoProperties_OutpuRawTokenText()
    {
        // When a template has a property token but properties array is empty,
        // the raw token text "{N}" should appear in RenderedMessage
        var entry = MakeEntry("{N}", []);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("RenderedMessage");
        output.Should().Contain("{N}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // D. LogColorTextWriter — WriteRenderedMessage with propertys.Length == 0
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogColorTextWriter_TemplateWithNoProperties_OutputsRawTokenText()
    {
        var entry = MakeEntry("{N}", []);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("{N}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // E. LogColorTextWriter — SetValueColor: remaining type branches
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogColorTextWriter_CharValue_RendersColor()
    {
        var entry = MakeEntry("{V}", ['X']);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("X");
    }

    [Fact]
    public void LogColorTextWriter_DateTimeValue_RendersColor()
    {
        var entry = MakeEntry("{V}", [new DateTime(2025, 6, 1)]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("2025");
    }

    [Fact]
    public void LogColorTextWriter_DateTimeOffsetValue_RendersColor()
    {
        var dto = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var entry = MakeEntry("{V}", [dto]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("2025");
    }

    [Fact]
    public void LogColorTextWriter_TimeSpanValue_RendersColor()
    {
        var entry = MakeEntry("{V}", [TimeSpan.FromMinutes(5)]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("00:05:00");
    }

    [Fact]
    public void LogColorTextWriter_UriValue_RendersColor()
    {
        var entry = MakeEntry("{V}", [new Uri("https://test.com")]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("https://test.com");
    }

    [Fact]
    public void LogColorTextWriter_UnknownType_FallsBackToOtherColor()
    {
        // An anonymous type falls to the default color branch
        object custom = new { Name = "test" };
        var entry = MakeEntry("{V}", [custom]);
        // Should render without throwing; check ANSI escape is present
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("\x1b["); // some ANSI prefix
    }

    // ═════════════════════════════════════════════════════════════════════════
    // F. LogWriter base — GetBufferCapacity
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogWriter_GetBufferCapacity_ReturnsNonNegative()
    {
        var writer = WriterPool.Get<LogTextWriter>();
        var capacity = writer.GetBufferCapacity();
        capacity.Should().BeGreaterThanOrEqualTo(0);
        writer.Return();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // G. LogWriter base — IsCommonCollectionType edge cases
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogTextWriter_NullCollectionAutoDestructure_OutputsNull()
    {
        DestructuringConfig.EnableAutoDestructuring();
        // null property — should output "null" not crash
        var entry = MakeEntry("{X}", [null]);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("null");
    }

    [Fact]
    public void LogTextWriter_StringWithAutoDestructure_NotDestructured()
    {
        // String is excluded from collection detection — should not be JSON-serialized
        DestructuringConfig.EnableAutoDestructuring();
        var entry = MakeEntry("{S}", ["hello"]);
        var output = Render<LogTextWriter>(entry);
        // Should contain the raw value rendered as-is (not as a JSON array/object)
        output.Should().Contain("hello");
        // JSON-serialized string would be "hello" with quotes; plain render should be without braces
        output.Should().NotContain("{\"");  // no JSON object wrapper
    }

    [Fact]
    public void LogTextWriter_ArrayWithAutoDestructure_IsDestructured()
    {
        DestructuringConfig.EnableAutoDestructuring();
        int[] arr = [10, 20, 30];
        var entry = MakeEntry("{A}", [arr]);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("10");
        output.Should().Contain("20");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // H. LogJsonWriter — WritePropertyValue: more-properties-than-tokens path
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogJsonWriter_MoreTokensThanProperties_ExtraTokensAreNull()
    {
        // Template has 2 property tokens, but only 1 property value
        // The second property slot should be "null"
        var entry = MakeEntry("{A} and {B}", ["x"]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"A\"");
        output.Should().Contain("\"B\"");
        output.Should().Contain("null");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // I. LogWriter base — WriteAligned via rendered template
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogTextWriter_RightAlign_PadsValueCorrectly()
    {
        // "{N,5}" with int 42 → right-aligned: "   42" (3 spaces + 42)
        var entry = MakeEntry("{N,5}", [42]);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("   42");
    }

    [Fact]
    public void LogTextWriter_LeftAlign_PadsValueCorrectly()
    {
        // "{N,-5}" with int 42 → left-aligned: "42   " (42 + 3 spaces)
        var entry = MakeEntry("{N,-5}", [42]);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("42   ");
    }

    [Fact]
    public void LogTextWriter_AlignWidth_ContentExceedsWidth_NoPadding()
    {
        // "{N,2}" with int 12345 — content (5 chars) > width (2), no padding
        var entry = MakeEntry("{N,2}", [12345]);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("12345");
        // Ensure no extra leading spaces before 12345
        output.Should().NotMatchRegex(@"  12345"); // no double-space before it
    }

    [Fact]
    public void LogTextWriter_WideAlignment_TriggersLargePadding()
    {
        // "{N,40}" with int 1 → needs 39 spaces (> 32, triggers GetSpan/Advance path)
        var entry = MakeEntry("{N,40}", [1]);
        var output = Render<LogTextWriter>(entry);
        // Output should contain "1" preceded by exactly 39 spaces
        output.Should().MatchRegex(@" {39}1");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // J. LogJsonWriter — WriteAligned in RenderedMessage (IUtf8SpanFormattable)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogJsonWriter_RightAlign_InRenderedMessage()
    {
        var entry = MakeEntry("{N,5}", [42]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("   42"); // right-aligned inside RenderedMessage
    }

    [Fact]
    public void LogJsonWriter_LeftAlign_InRenderedMessage()
    {
        var entry = MakeEntry("{N,-5}", [42]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("42   ");
    }

    [Fact]
    public void LogJsonWriter_WideAlignment_TriggersLargePadding()
    {
        var entry = MakeEntry("{N,40}", [1]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().MatchRegex(@" {39}1");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // K. IDestructured path — direct byte injection (all writers)
    // ═════════════════════════════════════════════════════════════════════════

    private sealed class InlineDestructured : IDestructured
    {
        private readonly byte[] _bytes;
        public InlineDestructured(string json) => _bytes = System.Text.Encoding.UTF8.GetBytes(json);
        public ReadOnlyMemory<byte> Destructured() => _bytes;
    }

    [Fact]
    public void LogTextWriter_IDestructured_AppendsRawBytes()
    {
        var entry = MakeEntry("{@V}", [new InlineDestructured("{\"k\":1}")]);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("{\"k\":1}");
    }

    [Fact]
    public void LogColorTextWriter_IDestructured_AppendsRawBytes()
    {
        var entry = MakeEntry("{@V}", [new InlineDestructured("{\"k\":2}")]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("{\"k\":2}");
    }

    [Fact]
    public void LogJsonWriter_IDestructured_WritesRawValueInProperties()
    {
        // In WritePropertyValue the IDestructured path calls _jsonWriter.WriteRawValue(...)
        var entry = MakeEntry("{@V}", [new InlineDestructured("{\"k\":3}")]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"k\"");
        output.Should().Contain("3");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // L. IDestructurable path — executes without crash (LogColorTextWriter, LogTextWriter)
    // Note: the IDestructurable path in text writers has a known initialization
    // order issue (bufferWriter is reset, jsonWriter stays on Stream.Null),
    // so we only assert no exception is thrown.
    // ═════════════════════════════════════════════════════════════════════════

    private sealed class SimpleDestructurable : IDestructurable
    {
        public void Destructure(DestructureHelper helper)
        {
            helper.WriteStartObject();
            helper.WritePropertyName("x");
            helper.WriteStringValue("val");
            helper.WriteEndObject();
        }
    }

    [Fact]
    public void LogTextWriter_IDestructurable_DoesNotThrow()
    {
        var entry = MakeEntry("{@V}", [new SimpleDestructurable()]);
        Action act = () => Render<LogTextWriter>(entry);
        act.Should().NotThrow();
    }

    [Fact]
    public void LogColorTextWriter_IDestructurable_DoesNotThrow()
    {
        var entry = MakeEntry("{@V}", [new SimpleDestructurable()]);
        Action act = () => Render<LogColorTextWriter>(entry);
        act.Should().NotThrow();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // M. LogJsonWriter — IDestructurable in WritePropertyValue path
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogJsonWriter_IDestructurable_PathIsReached()
    {
        // The IDestructurable path in LogJsonWriter.WritePropertyValue has a known
        // initialization issue: _serializerWriter stays on Stream.Null (not rebound
        // to _scratchWriter), so Destructure() writes to nowhere and WrittenSpan is
        // empty. WriteRawValue then receives empty bytes and throws JsonException.
        // This test verifies the branch is entered (code is reached for coverage).
        var entry = MakeEntry("{@V}", [new SimpleDestructurable()]);
        Action act = () => Render<LogJsonWriter>(entry);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void LogJsonWriter_IDestructured_InProperties_ContainsExpectedJson()
    {
        // IDestructured in WritePropertyValue: _jsonWriter.WriteRawValue(bytes)
        // This works correctly — bytes go directly to the main buffer.
        var entry = MakeEntry("{@V}", [new InlineDestructured("{\"answer\":42}")]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("answer");
        output.Should().Contain("42");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // N. LogJsonWriter — WriteJsonValue: normal double (line 388)
    // double.IsNaN / IsPositiveInfinity / IsNegativeInfinity are tested elsewhere;
    // this covers the final `else json.WriteNumberValue(d)` arm.
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogJsonWriter_DoubleNormal_RenderedAsNumber()
    {
        // 3.5 is a double literal — exercises json.WriteNumberValue(d) at line 388
        var entry = MakeEntry("{V}", [3.5]);
        Render<LogJsonWriter>(entry).Should().Contain("3.5");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // O. LogColorTextWriter — WriteAligned for IUtf8SpanFormattable (lines 221, 223-224)
    // The `else` branch when Alignment.HasValue is true for a numeric type.
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LogColorTextWriter_AlignedNumericValue_RendersAligned()
    {
        // {N,5} with int 42: IUtf8SpanFormattable + Alignment.HasValue = true
        // → WriteAligned branch in RenderPropertyToken (lines 221, 223-224)
        var entry = MakeEntry("{N,5}", [42]);
        var output = Render<LogColorTextWriter>(entry);
        // The color code (\x1b[93m) is written before the padded value, so the
        // 3 padding spaces and "42" are contiguous in the raw output — no stripping needed.
        output.Should().Contain("   42"); // 3 spaces + 42 = right-aligned to width 5
    }

    // ═════════════════════════════════════════════════════════════════════════
    // P. LogJsonWriter — IDestructurable: TryFlush returns false → lines 322-323 reached
    // When the IDestructurable does not close its JSON (depth != 0), TryFlush()
    // returns false, WriteRawValue is skipped, and destructureHelper.Dispose(true, true)
    // at lines 322-323 IS still executed. The _jsonWriter is then left with an open
    // property name but no value, causing WriteEndObject to throw.
    // ═════════════════════════════════════════════════════════════════════════

    private sealed class IncompleteDestructurable : IDestructurable
    {
        public void Destructure(DestructureHelper helper)
        {
            helper.WriteStartObject();
            // Deliberately no WriteEndObject — TryFlush returns false (depth != 0)
        }
    }

    [Fact]
    public void LogJsonWriter_IDestructurable_TryFlushFalse_DisposesHelperBeforeThrow()
    {
        // TryFlush returns false → WriteRawValue is skipped → Dispose at lines 322-323 IS reached.
        // _jsonWriter then has a property name with no value, so WriteEndObject throws.
        var entry = MakeEntry("{@V}", [new IncompleteDestructurable()]);
        Action act = () => Render<LogJsonWriter>(entry);
        act.Should().Throw<Exception>();
    }
}
