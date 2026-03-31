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

using FluentAssertions;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Writer;
using Lunarium.Logging.Target;

namespace Lunarium.Logging.Tests.Writer;

/// <summary>
/// Tests for LogTextWriter — plain-text log rendering.
/// We render a LogEntry through LogTextWriter and assert on the string output.
/// All tests call ParseMessage first (simulating ProcessQueueAsync) because
/// LogWriter.Render() consumes MessageTemplate._messageTemplateTokens.
/// </summary>
public class LogTextWriterTests
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
            timestamp: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            logLevel: level,
            message: message,
            properties: props ?? [],
            context: context,
            contextBytes: System.Text.Encoding.UTF8.GetBytes(context),
            scope: "",
            messageTemplate: LogParser.EmptyMessageTemplate,
            exception: ex);
        // Simulate lazy parsing (as ProcessQueueAsync does)
        entry.ParseMessage();
        return entry;
    }

    private static string Render(LogEntry entry)
    {
        var writer = WriterPool.Get<LogTextWriter>();
        try
        {
            writer.Render(entry);
            // Don't Trim() — it strips trailing alignment spaces.
            // Remove only the trailing newline that Render appends.
            return writer.ToString().TrimEnd('\r', '\n');
        }
        finally
        {
            writer.Return();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Level abbreviations
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LogLevel.Debug, "[DBG]")]
    [InlineData(LogLevel.Info, "[INF]")]
    [InlineData(LogLevel.Warning, "[WRN]")]
    [InlineData(LogLevel.Error, "[ERR]")]
    [InlineData(LogLevel.Critical, "[CRT]")]
    public void Render_LevelAbbreviation_CorrectInOutput(LogLevel level, string expectedAbbr)
    {
        var entry = MakeEntry("msg", level: level);
        var output = Render(entry);
        output.Should().Contain(expectedAbbr);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Property rendering — positional substitution
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_SingleProperty_SubstitutedIntoMessage()
    {
        var entry = MakeEntry("Hello {Name}", props: ["Alice"]);
        var output = Render(entry);
        output.Should().Contain("Hello Alice");
    }

    [Fact]
    public void Render_MultipleProperties_AllSubstituted()
    {
        var entry = MakeEntry("{A} and {B}", props: [42, "world"]);
        var output = Render(entry);
        output.Should().Contain("42 and world");
    }

    [Fact]
    public void Render_MorePlaceholdersThanProps_ExcessUseRawText()
    {
        // {B} has no matching property → should appear as raw text "{B}"
        var entry = MakeEntry("{A} and {B}", props: ["only_one"]);
        var output = Render(entry);
        output.Should().Contain("only_one");
        output.Should().Contain("{B}");
    }

    [Fact]
    public void Render_NullProperty_RendersAsNullString()
    {
        var entry = MakeEntry("Value: {Val}", props: [null]);
        var output = Render(entry);
        output.Should().Contain("Value: null");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Alignment rendering
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_RightAlignment_PaddedCorrectly()
    {
        var entry = MakeEntry("{Val,6}", props: [42]);
        var output = Render(entry);
        // "    42" — 6 chars right aligned
        output.Should().Contain("    42");
    }

    [Fact]
    public void Render_LeftAlignment_PaddedCorrectly()
    {
        var entry = MakeEntry("{Val,-6}", props: [42]);
        var output = Render(entry);
        // "42    " — 6 chars left aligned
        output.Should().Contain("42    ");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Format specifier rendering
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_FormatF2_RendersWithTwoDecimals()
    {
        var entry = MakeEntry("{PI:F2}", props: [3.14159]);
        var output = Render(entry);
        output.Should().Contain("3.14");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Destructuring (@) rendering
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_DestructurePrefix_RendersAsJson()
    {
        var entry = MakeEntry("{@Obj}", props: [new { Name = "Alice", Age = 30 }]);
        var output = Render(entry);
        output.Should().Contain("Alice");
        output.Should().Contain("30");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Context rendering
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithContext_ContextAppearsInBrackets()
    {
        var entry = MakeEntry("msg", context: "MyService");
        var output = Render(entry);
        output.Should().Contain("[MyService]");
    }

    [Fact]
    public void Render_EmptyContext_NoBracketContext()
    {
        var entry = MakeEntry("msg", context: "");
        var output = Render(entry);
        // Should NOT contain an empty bracket context
        output.Should().NotMatchRegex(@"\[\]");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Exception rendering
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithException_ExceptionAppearsInOutput()
    {
        var ex = new InvalidOperationException("test error");
        var entry = MakeEntry("msg", ex: ex);
        var output = Render(entry);
        output.Should().Contain("InvalidOperationException");
        output.Should().Contain("test error");
    }

    [Fact]
    public void Render_NoException_NoExceptionInOutput()
    {
        var entry = MakeEntry("clean message");
        var output = Render(entry);
        output.Should().NotContain("Exception");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. Pure-text message (no properties)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_PureTextMessage_OutputContainsMessage()
    {
        var entry = MakeEntry("Pure text message with no placeholders");
        var output = Render(entry);
        output.Should().Contain("Pure text message with no placeholders");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. Escaped braces in rendered message
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_EscapedBraces_RenderedAsLiteralBraces()
    {
        var entry = MakeEntry("{{literal}}");
        var output = Render(entry);
        output.Should().Contain("{literal}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. LoggerName rendering
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithLoggerName_LoggerNameAppearsInBrackets()
    {
        var entry = MakeEntry("msg"); // loggerName = "Test"
        var output = Render(entry);
        output.Should().Contain("[Test]");
    }

    [Fact]
    public void Render_EmptyLoggerName_NoLoggerNameBrackets()
    {
        var entry = new LogEntry(
            loggerName: "",
            loggerNameBytes: default,
            timestamp: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            logLevel: LogLevel.Info,
            message: "msg",
            properties: [],
            context: "",
            contextBytes: default,
            scope: "",
            messageTemplate: LogParser.EmptyMessageTemplate);
        entry.ParseMessage();
        var output = Render(entry);
        output.Should().NotMatchRegex(@"\[\]");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. Render with TextOutputIncludeConfig
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithConfigExcludeLoggerName_LoggerNameNotInOutput()
    {
        var entry = MakeEntry("msg"); // loggerName = "Test"
        var writer = WriterPool.Get<LogTextWriter>();
        try
        {
            writer.Render(entry, new TextOutputIncludeConfig { IncludeLoggerName = false });
            var output = writer.ToString().TrimEnd('\r', '\n');
            output.Should().NotContain("[Test]");
            output.Should().Contain("msg");
        }
        finally { writer.Return(); }
    }

    [Fact]
    public void Render_WithConfigExcludeTimestamp_TimestampNotInOutput()
    {
        var entry = MakeEntry("msg");
        var writer = WriterPool.Get<LogTextWriter>();
        try
        {
            writer.Render(entry, new TextOutputIncludeConfig { IncludeTimestamp = false });
            var output = writer.ToString().TrimEnd('\r', '\n');
            output.Should().NotContain("2026");
        }
        finally { writer.Return(); }
    }

    [Fact]
    public void Render_WithConfigExcludeLevel_LevelNotInOutput()
    {
        var entry = MakeEntry("msg");
        var writer = WriterPool.Get<LogTextWriter>();
        try
        {
            writer.Render(entry, new TextOutputIncludeConfig { IncludeLevel = false });
            var output = writer.ToString().TrimEnd('\r', '\n');
            output.Should().NotContain("[INF]");
        }
        finally { writer.Return(); }
    }

    [Fact]
    public void Render_WithConfigExcludeContext_ContextNotInOutput()
    {
        var entry = MakeEntry("msg", context: "MyService");
        var writer = WriterPool.Get<LogTextWriter>();
        try
        {
            writer.Render(entry, new TextOutputIncludeConfig { IncludeContext = false });
            var output = writer.ToString().TrimEnd('\r', '\n');
            output.Should().NotContain("[MyService]");
        }
        finally { writer.Return(); }
    }
}
