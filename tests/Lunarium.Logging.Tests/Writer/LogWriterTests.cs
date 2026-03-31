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
using Lunarium.Logging.Config.Configurator;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Target;
using Lunarium.Logging.Writer;

namespace Lunarium.Logging.Tests.Writer;

/// <summary>
/// Unit tests for LogTextWriter, LogColorTextWriter, LogJsonWriter, and the LogWriter base.
///
/// Access pattern: writers are retrieved from WriterPool.Get&lt;T&gt;() (internal, InternalsVisibleTo).
/// Each render call goes through the full Render(LogEntry) pipeline.
/// StringChannelTarget.Emit() is also tested to exercise the JSON/color/plain selection branches.
/// </summary>
public class LogWriterTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Build a minimal LogEntry with a parsed message template.
    /// </summary>
    private static LogEntry MakeEntry(
        string template,
        object?[] props,
        LogLevel level = LogLevel.Info,
        string context = "",
        Exception? ex = null)
    {
        var parsed = LogParser.ParseMessage(template);
        return new LogEntry(
            loggerName: "TestLogger",
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

    /// <summary>
    /// Get a fresh writer from the pool, render the entry, capture the output, then return it.
    /// </summary>
    private static string Render<T>(LogEntry entry) where T : LogWriter, new()
    {
        var writer = WriterPool.Get<T>();
        writer.Render(entry);
        var result = writer.ToString();
        writer.Return();
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. LogTextWriter — timestamp modes
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LogTextWriter_UnixTimestamp_RendersCorrectly()
    {
        TimestampFormatConfig.ConfigTextMode(TextTimestampMode.Unix);
        var entry = MakeEntry("msg", []);
        var output = Render<LogTextWriter>(entry);
        output.Should().MatchRegex(@"\[\d+\]");
    }

    [Fact]
    public void LogTextWriter_UnixMsTimestamp_RendersCorrectly()
    {
        TimestampFormatConfig.ConfigTextMode(TextTimestampMode.UnixMs);
        var entry = MakeEntry("msg", []);
        var output = Render<LogTextWriter>(entry);
        output.Should().MatchRegex(@"\[\d{13,}\]");
    }

    [Fact]
    public void LogTextWriter_ISO8601Timestamp_RendersCorrectly()
    {
        TimestampFormatConfig.ConfigTextMode(TextTimestampMode.ISO8601);
        var entry = MakeEntry("msg", []);
        var output = Render<LogTextWriter>(entry);
        // ISO8601 round-trip format contains 'T'
        output.Should().MatchRegex(@"\[\d{4}-\d{2}-\d{2}T");
    }

    [Fact]
    public void LogTextWriter_CustomTimestamp_RendersWithCustomFormat()
    {
        TimestampFormatConfig.ConfigTextMode(TextTimestampMode.Custom);
        TimestampFormatConfig.ConfigTextCustomFormat("yyyy/MM/dd");
        var entry = MakeEntry("msg", []);
        var output = Render<LogTextWriter>(entry);
        output.Should().MatchRegex(@"\[\d{4}/\d{2}/\d{2}\]");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. LogTextWriter — all log levels
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LogLevel.Debug, "DBG")]
    [InlineData(LogLevel.Info, "INF")]
    [InlineData(LogLevel.Warning, "WRN")]
    [InlineData(LogLevel.Error, "ERR")]
    [InlineData(LogLevel.Critical, "CRT")]
    public void LogTextWriter_Level_RendersCorrectAbbreviation(LogLevel level, string abbr)
    {
        TimestampFormatConfig.ConfigTextMode(TextTimestampMode.Custom);
        TimestampFormatConfig.ConfigTextCustomFormat("s");
        var entry = MakeEntry("msg", [], level: level);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain($"[{abbr}]");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. LogTextWriter — context
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LogTextWriter_WithContext_RendersContext()
    {
        var entry = MakeEntry("msg", [], context: "Service.Auth");
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("[Service.Auth]");
    }

    [Fact]
    public void LogTextWriter_NoContext_NoContextBracket()
    {
        var entry = MakeEntry("msg", [], context: "");
        var output = Render<LogTextWriter>(entry);
        // No [context] section (aside from level & timestamp)
        output.Should().NotMatchRegex(@"\[\]\s");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. LogTextWriter — property rendering paths
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LogTextWriter_NullProperty_RendersNull()
    {
        var entry = MakeEntry("Value: {V}", [null]);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("null");
    }

    [Fact]
    public void LogTextWriter_NoProperties_RendersRawTemplate()
    {
        // When propertys array is empty, falls back to raw token text
        var entry = MakeEntry("{Missing} prop", []);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("{Missing}");
    }

    [Fact]
    public void LogTextWriter_MissingProperty_RendersRawToken()
    {
        // More tokens than property values: extra tokens get raw text
        var entry = MakeEntry("{A} and {B}", ["only-one"]);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("only-one");
        output.Should().Contain("{B}");
    }

    [Fact]
    public void LogTextWriter_Format_AppliedToValue()
    {
        var entry = MakeEntry("{N:F2}", [3.14159]);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("3.14");
    }

    [Fact]
    public void LogTextWriter_Alignment_AppliedToValue()
    {
        var entry = MakeEntry("{N,10}", [42]);
        var output = Render<LogTextWriter>(entry);
        // Right-aligned in 10 chars: should contain spaces + "42"
        output.Should().Contain("        42");
    }

    [Fact]
    public void LogTextWriter_AlignmentAndFormat_BothApplied()
    {
        var entry = MakeEntry("{N,-8:D3}", [5]);
        var output = Render<LogTextWriter>(entry);
        // Left-aligned, minimum 3 digits: "005     "
        output.Should().Contain("005");
    }

    [Fact]
    public void LogTextWriter_LongFormat_BuildsViaHeap()
    {
        // >96 chars in format string triggers BuildFormatStringByHeap
        var longFormat = new string('0', 100); // 100 zeros
        var entry = MakeEntry($"{{N:{longFormat}}}", [123]);
        var output = Render<LogTextWriter>(entry);
        // Should contain the number (formatted as 123 padded with zeros)
        output.Should().Contain("123");
    }

    [Fact]
    public void LogTextWriter_DestructureProperty_RendersJson()
    {
        var obj = new { Name = "Alice", Age = 30 };
        var entry = MakeEntry("{@Person}", [obj]);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("Alice");
        output.Should().Contain("30");
    }

    [Fact]
    public void LogTextWriter_CollectionProperty_RendersJson()
    {
        DestructuringConfig.EnableAutoDestructuring();
        var list = new List<int> { 1, 2, 3 };
        var entry = MakeEntry("{List}", [list]);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("1");
        output.Should().Contain("2");
        output.Should().Contain("3");
    }

    [Fact]
    public void LogTextWriter_WithException_RendersException()
    {
        var ex = new InvalidOperationException("kaboom");
        var entry = MakeEntry("Error", [], ex: ex);
        var output = Render<LogTextWriter>(entry);
        output.Should().Contain("kaboom");
        output.Should().Contain("InvalidOperationException");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. LogColorTextWriter — all levels produce ANSI color codes
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Info)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void LogColorTextWriter_AllLevels_ContainAnsiEscape(LogLevel level)
    {
        var entry = MakeEntry("test", [], level: level);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("\x1b[");
    }

    [Fact]
    public void LogColorTextWriter_WithContext_RendersContext()
    {
        var entry = MakeEntry("msg", [], context: "Svc.Worker");
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("Svc.Worker");
    }

    [Fact]
    public void LogColorTextWriter_NullProperty_HasNullText()
    {
        var entry = MakeEntry("Val: {V}", [null]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("null");
    }

    [Fact]
    public void LogColorTextWriter_Format_Applied()
    {
        var entry = MakeEntry("{N:F1}", [1.5]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("1.5");
    }

    [Fact]
    public void LogColorTextWriter_LongFormat_UsesHeapPath()
    {
        var longFormat = new string('0', 100);
        var entry = MakeEntry($"{{N:{longFormat}}}", [7]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("7");
    }

    // SetValueColor branches: numbers, bool, string, Guid, DateTime, other
    [Theory]
    [InlineData(42)]
    [InlineData(3.14)]
    [InlineData(true)]
    [InlineData("hello")]
    public void LogColorTextWriter_SetValueColor_PrimitiveTypes_ContainValue(object value)
    {
        var entry = MakeEntry("{V}", [value]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain(value.ToString()!);
    }

    [Fact]
    public void LogColorTextWriter_SetValueColor_Guid_ContainsGuidString()
    {
        var g = Guid.NewGuid();
        var entry = MakeEntry("{V}", [g]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain(g.ToString());
    }

    [Fact]
    public void LogColorTextWriter_SetValueColor_DateTime_ContainsDate()
    {
        var dt = new DateTime(2026, 3, 10);
        var entry = MakeEntry("{V}", [dt]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("2026");
    }

    [Fact]
    public void LogColorTextWriter_DestructureProperty_RendersJson()
    {
        var obj = new { Id = 99 };
        var entry = MakeEntry("{@Obj}", [obj]);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("99");
    }

    [Fact]
    public void LogColorTextWriter_WithException_RendersException()
    {
        var ex = new Exception("color-ex");
        var entry = MakeEntry("Err", [], ex: ex);
        var output = Render<LogColorTextWriter>(entry);
        output.Should().Contain("color-ex");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. LogJsonWriter — structure and field presence
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LogJsonWriter_BasicRender_IsValidJson()
    {
        var entry = MakeEntry("Hello {Name}", ["World"]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().StartWith("{").And.Contain("}");
    }

    [Fact]
    public void LogJsonWriter_ContainsAllExpectedFields()
    {
        var entry = MakeEntry("Hello {Name}", ["World"], context: "Svc");
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"Timestamp\"");
        output.Should().Contain("\"Level\"");
        output.Should().Contain("\"Context\"");
        output.Should().Contain("\"OriginalMessage\"");
        output.Should().Contain("\"RenderedMessage\"");
        output.Should().Contain("\"Propertys\"");
    }

    [Fact]
    public void LogJsonWriter_NoContext_NoContextField()
    {
        var entry = MakeEntry("msg", []);
        var output = Render<LogJsonWriter>(entry);
        output.Should().NotContain("\"Context\"");
    }

    [Fact]
    public void LogJsonWriter_WithException_ContainsExceptionField()
    {
        var ex = new InvalidOperationException("json-ex");
        var entry = MakeEntry("Boom", [], ex: ex);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"Exception\"");
        output.Should().Contain("json-ex");
    }

    [Fact]
    public void LogJsonWriter_NullProperty_RendersNullInPropertiesSection()
    {
        var entry = MakeEntry("{V}", [null]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"V\":null");
    }

    // JSON timestamp modes
    [Fact]
    public void LogJsonWriter_UnixTimestampMode_IsNumeric()
    {
        TimestampFormatConfig.ConfigJsonMode(JsonTimestampMode.Unix);
        var entry = MakeEntry("msg", []);
        var output = Render<LogJsonWriter>(entry);
        // "Timestamp": followed by a number (no quotes)
        output.Should().MatchRegex("\"Timestamp\":\\d+");
    }

    [Fact]
    public void LogJsonWriter_UnixMsTimestampMode_IsNumeric()
    {
        TimestampFormatConfig.ConfigJsonMode(JsonTimestampMode.UnixMs);
        var entry = MakeEntry("msg", []);
        var output = Render<LogJsonWriter>(entry);
        output.Should().MatchRegex("\"Timestamp\":\\d{13,}");
    }

    [Fact]
    public void LogJsonWriter_ISO8601TimestampMode_IsString()
    {
        TimestampFormatConfig.ConfigJsonMode(JsonTimestampMode.ISO8601);
        var entry = MakeEntry("msg", []);
        var output = Render<LogJsonWriter>(entry);
        output.Should().MatchRegex("\"Timestamp\":\"\\d{4}-");
    }

    [Fact]
    public void LogJsonWriter_CustomTimestampMode_UsesCustomFormat()
    {
        TimestampFormatConfig.ConfigJsonMode(JsonTimestampMode.Custom);
        TimestampFormatConfig.ConfigJsonCustomFormat("yyyy");
        var entry = MakeEntry("msg", []);
        var output = Render<LogJsonWriter>(entry);
        output.Should().MatchRegex("\"Timestamp\":\"\\d{4}\"");
    }

    // ToJsonValue branches: numeric types, bool, string, Guid, DateTime, etc.
    [Fact]
    public void LogJsonWriter_IntProperty_IsUnquotedInJson()
    {
        var entry = MakeEntry("{N}", [42]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"N\":42");
    }

    [Fact]
    public void LogJsonWriter_BoolProperty_IsLowercaseUnquoted()
    {
        var entry = MakeEntry("{B}", [true]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"B\":true");
    }

    [Fact]
    public void LogJsonWriter_StringProperty_IsQuotedAndEscaped()
    {
        var entry = MakeEntry("{S}", ["he said \"hi\""]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\\\"hi\\\"");
    }

    [Fact]
    public void LogJsonWriter_DoubleNaN_IsNaNString()
    {
        var entry = MakeEntry("{D}", [double.NaN]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"NaN\"");
    }

    [Fact]
    public void LogJsonWriter_DoubleInfinity_IsInfinityString()
    {
        var entry = MakeEntry("{D}", [double.PositiveInfinity]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"Infinity\"");
    }

    [Fact]
    public void LogJsonWriter_DoubleNegativeInfinity_IsMinusInfinityString()
    {
        var entry = MakeEntry("{D}", [double.NegativeInfinity]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"-Infinity\"");
    }

    [Fact]
    public void LogJsonWriter_FloatNaN_IsNaNString()
    {
        var entry = MakeEntry("{F}", [float.NaN]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"NaN\"");
    }

    [Fact]
    public void LogJsonWriter_GuidProperty_IsQuotedString()
    {
        var g = Guid.NewGuid();
        var entry = MakeEntry("{G}", [g]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain($"\"{g}\"");
    }

    [Fact]
    public void LogJsonWriter_LongProperty_IsUnquoted()
    {
        var entry = MakeEntry("{L}", [123456789L]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"L\":123456789");
    }

    [Fact]
    public void LogJsonWriter_DestructureProperty_IsJsonObject()
    {
        var obj = new { X = 7 };
        var entry = MakeEntry("{@O}", [obj]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"X\"");
        output.Should().Contain("7");
    }

    [Fact]
    public void LogJsonWriter_MissingProperty_PropertySectionHasNull()
    {
        // More tokens than values → "null," for the extra
        var entry = MakeEntry("{A} {B}", ["only-one"]);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain("\"B\":null");
    }

    [Fact]
    public void LogJsonWriter_SpecialCharsInMessage_EscapedCorrectly()
    {
        // Newline and backslash in message template text
        var entry = MakeEntry("line1\nline2", []);
        var output = Render<LogJsonWriter>(entry);
        // JSON renderedmessage should escape the newline
        output.Should().Contain("\\n");
    }

    [Theory]
    [InlineData(LogLevel.Debug, "Debug")]
    [InlineData(LogLevel.Info, "Information")]
    [InlineData(LogLevel.Warning, "Warning")]
    [InlineData(LogLevel.Error, "Error")]
    [InlineData(LogLevel.Critical, "Critical")]
    public void LogJsonWriter_AllLevels_CorrectLevelString(LogLevel level, string expected)
    {
        var entry = MakeEntry("msg", [], level: level);
        var output = Render<LogJsonWriter>(entry);
        output.Should().Contain($"\"Level\":\"{expected}\"");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. LogWriter base — TryReset and pool paths
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LogWriter_TryReset_SmallBuffer_ReturnsTrue()
    {
        var writer = WriterPool.Get<LogTextWriter>();
        var entry = MakeEntry("small msg", []);
        writer.Render(entry);
        // Default capacity threshold is 4KB — a tiny render won't exceed this
        writer.TryReset().Should().BeTrue();
        writer.Return();
    }

    [Fact]
    public void LogWriter_TryReset_LargeBuffer_ReturnsFalse()
    {
        var writer = WriterPool.Get<LogTextWriter>();
        // Force oversized buffer by appending > 4KB
        var bigEntry = MakeEntry(new string('x', 5000), []);
        writer.Render(bigEntry);
        // After render, the buffer is > 4KB → TryReset should return false
        writer.TryReset(maxCapacity: 100).Should().BeFalse();
        // Don't return to pool — writer would be discarded anyway
    }

    [Fact]
    public void LogWriter_Dispose_DoubleDispose_DoesNotThrow()
    {
        var writer = WriterPool.Get<LogTextWriter>();
        Action act = () =>
        {
            writer.Dispose();
            writer.Dispose(); // second dispose should be no-op
        };
        act.Should().NotThrow();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. ChannelSink.Emit — JSON mode vs plain mode selection
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StringChannelTarget_EmitJson_ProducesJsonOutput()
    {
        var ch = Channel.CreateUnbounded<string>();
        var target = new StringChannelTarget(ch.Writer, toJson: true, isColor: false);
        var entry = MakeEntry("Hello {Name}", ["World"]);
        target.Emit(entry);

        ch.Reader.TryRead(out var result).Should().BeTrue();
        result.Should().NotBeNull();
        result!.Should().Contain("\"Level\"");
        result.Should().Contain("\"RenderedMessage\"");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task StringChannelTarget_EmitColor_ProducesAnsiOutput()
    {
        var ch = Channel.CreateUnbounded<string>();
        var target = new StringChannelTarget(ch.Writer, toJson: false, isColor: true);
        var entry = MakeEntry("Hello {Name}", ["World"]);
        target.Emit(entry);

        ch.Reader.TryRead(out var result).Should().BeTrue();
        result.Should().Contain("\x1b[");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task StringChannelTarget_EmitPlain_ProducesPlainOutput()
    {
        var ch = Channel.CreateUnbounded<string>();
        var target = new StringChannelTarget(ch.Writer, toJson: false, isColor: false);
        var entry = MakeEntry("Hello {Name}", ["World"]);
        target.Emit(entry);

        ch.Reader.TryRead(out var result).Should().BeTrue();
        result.Should().Contain("[INF]");
        result.Should().NotContain("\x1b[");
        await Task.CompletedTask;
    }
}
