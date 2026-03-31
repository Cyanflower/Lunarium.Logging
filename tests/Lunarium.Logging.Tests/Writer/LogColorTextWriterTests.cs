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

using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Writer;

namespace Lunarium.Logging.Tests.Writer;

/// <summary>
/// Tests for LogColorTextWriter.
///
/// Strategy: render a LogEntry, strip ANSI escape codes from the result,
/// and assert on the visible content. Also assert that specific ANSI codes
/// appear at the right positions for color verification.
/// </summary>
public class LogColorTextWriterTests
{
    private const string AnsiReset = "\x1b[0m";
    private const string AnsiPrefix = "\x1b[";

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
        entry.ParseMessage();
        return entry;
    }

    private static string Render(LogEntry entry)
    {
        var writer = WriterPool.Get<LogColorTextWriter>();
        try
        {
            writer.Render(entry);
            return writer.ToString().TrimEnd('\r', '\n');
        }
        finally
        {
            writer.Return();
        }
    }

    /// <summary>Strip all ANSI escape sequences to get visible text only.</summary>
    private static string StripAnsi(string s)
    {
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < s.Length)
        {
            if (s[i] == '\x1b' && i + 1 < s.Length && s[i + 1] == '[')
            {
                i += 2;
                while (i < s.Length && s[i] != 'm') i++;
                i++; // skip 'm'
            }
            else
            {
                sb.Append(s[i++]);
            }
        }
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Output contains ANSI codes (not plain text)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_Output_ContainsAnsiEscapeCodes()
    {
        var entry = MakeEntry("Hello");
        var raw = Render(entry);
        raw.Should().Contain(AnsiPrefix);
        raw.Should().Contain(AnsiReset);
    }

    [Fact]
    public void Render_StrippedOutput_ContainsMessage()
    {
        var entry = MakeEntry("Hello world");
        var stripped = StripAnsi(Render(entry));
        stripped.Should().Contain("Hello world");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Level abbreviations visible in stripped output
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LogLevel.Debug, "DBG")]
    [InlineData(LogLevel.Info, "INF")]
    [InlineData(LogLevel.Warning, "WRN")]
    [InlineData(LogLevel.Error, "ERR")]
    [InlineData(LogLevel.Critical, "CRT")]
    public void Render_LevelAbbreviation_PresentInStrippedOutput(LogLevel level, string abbrev)
    {
        var entry = MakeEntry("msg", level: level);
        var stripped = StripAnsi(Render(entry));
        stripped.Should().Contain(abbrev);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Different log levels use different ANSI color codes
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_DebugLevel_UsesDarkGrayCode()
    {
        var entry = MakeEntry("msg", level: LogLevel.Debug);
        Render(entry).Should().Contain("\x1b[90m"); // DarkGray = 90
    }

    [Fact]
    public void Render_InfoLevel_UsesGreenCode()
    {
        var entry = MakeEntry("msg", level: LogLevel.Info);
        Render(entry).Should().Contain("\x1b[92m"); // Green = 92
    }

    [Fact]
    public void Render_WarningLevel_UsesYellowCode()
    {
        var entry = MakeEntry("msg", level: LogLevel.Warning);
        Render(entry).Should().Contain("\x1b[93m"); // Yellow = 93
    }

    [Fact]
    public void Render_ErrorLevel_UsesRedCode()
    {
        var entry = MakeEntry("msg", level: LogLevel.Error);
        Render(entry).Should().Contain("\x1b[91m"); // Red = 91
    }

    [Fact]
    public void Render_CriticalLevel_UsesWhiteOnDarkRedCode()
    {
        var entry = MakeEntry("msg", level: LogLevel.Critical);
        // White fg (97) + DarkRed bg (41)
        Render(entry).Should().Contain("\x1b[97;41m");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Context / LoggerName
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithLoggerName_LoggerNameVisibleInStrippedOutput()
    {
        var entry = MakeEntry("msg"); // loggerName = "Test"
        StripAnsi(Render(entry)).Should().Contain("Test");
    }

    [Fact]
    public void Render_WithContext_ContextVisibleInStrippedOutput()
    {
        var entry = MakeEntry("msg", context: "MyService");
        StripAnsi(Render(entry)).Should().Contain("MyService");
    }

    [Fact]
    public void Render_WithContext_UsesCyanCode()
    {
        var entry = MakeEntry("msg", context: "Svc");
        Render(entry).Should().Contain("\x1b[96m"); // Cyan = 96
    }

    [Fact]
    public void Render_EmptyContext_EmptyLoggerName_NoCyanCode()
    {
        // Neither context nor logger name rendered → Cyan should not appear
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
        Render(entry).Should().NotContain("\x1b[96m");
    }

    [Fact]
    public void Render_EmptyContext_LoggerNameUsesCyanCode()
    {
        // Empty context but non-empty logger name — Cyan appears for logger name
        var entry = MakeEntry("msg", context: "");
        Render(entry).Should().Contain("\x1b[96m");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Property substitution in RenderedMessage
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_StringProperty_SubstitutedInMessage()
    {
        var entry = MakeEntry("Hello {Name}", props: ["Alice"]);
        StripAnsi(Render(entry)).Should().Contain("Alice");
    }

    [Fact]
    public void Render_IntProperty_SubstitutedInMessage()
    {
        var entry = MakeEntry("Count={N}", props: [42]);
        StripAnsi(Render(entry)).Should().Contain("42");
    }

    [Fact]
    public void Render_NoProperties_MessageKeptAsIs()
    {
        var entry = MakeEntry("Hello {Name}", props: []);
        StripAnsi(Render(entry)).Should().Contain("{Name}");
    }

    [Fact]
    public void Render_NullProperty_RendersNullText()
    {
        var entry = MakeEntry("V={V}", props: [null]);
        StripAnsi(Render(entry)).Should().Contain("null");
    }

    [Fact]
    public void Render_MissingProperty_RendersRawPlaceholder()
    {
        // Template has 2 props but only 1 value provided
        var entry = MakeEntry("{A} {B}", props: ["x"]);
        var stripped = StripAnsi(Render(entry));
        stripped.Should().Contain("x");
        stripped.Should().Contain("{B}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Value color dispatch
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_StringProperty_UsesMagentaColor()
    {
        var entry = MakeEntry("{V}", props: ["hello"]);
        Render(entry).Should().Contain("\x1b[95m"); // Magenta = 95
    }

    [Fact]
    public void Render_IntProperty_UsesYellowColor()
    {
        var entry = MakeEntry("{V}", props: [99]);
        Render(entry).Should().Contain("\x1b[93m"); // Yellow = 93
    }

    [Fact]
    public void Render_BoolProperty_UsesBlueColor()
    {
        var entry = MakeEntry("{V}", props: [true]);
        Render(entry).Should().Contain("\x1b[94m"); // Blue = 94
    }

    [Fact]
    public void Render_NullProperty_UsesDarkBlueColor()
    {
        var entry = MakeEntry("{V}", props: [null]);
        Render(entry).Should().Contain("\x1b[34m"); // DarkBlue = 34
    }

    [Fact]
    public void Render_ObjectProperty_UsesGrayColor()
    {
        // A custom object falls into the "default" branch → Gray (37)
        var entry = MakeEntry("{V}", props: [new object()]);
        Render(entry).Should().Contain("\x1b[37m"); // Gray = 37
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Destructuring (@)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_DestructurePrefix_OutputsJson()
    {
        var entry = MakeEntry("{@Obj}", props: [new { X = 1 }]);
        StripAnsi(Render(entry)).Should().Contain("\"X\"");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. Alignment
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_MultipleProperties_AllSubstituted()
    {
        var entry = MakeEntry("{A} and {B}", props: ["hello", "world"]);
        var stripped = StripAnsi(Render(entry));
        stripped.Should().Contain("hello");
        stripped.Should().Contain("world");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. Exception
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithException_ExceptionTextPresentInStrippedOutput()
    {
        var ex = new InvalidOperationException("boom");
        var entry = MakeEntry("msg", ex: ex);
        StripAnsi(Render(entry)).Should().Contain("InvalidOperationException");
    }

    [Fact]
    public void Render_WithException_UsesRedColor()
    {
        var ex = new Exception("err");
        var entry = MakeEntry("msg", ex: ex);
        // Exception block uses Red = 91
        Render(entry).Should().Contain("\x1b[91m");
    }

    [Fact]
    public void Render_NoException_NoExtraNewline()
    {
        var entry = MakeEntry("no-exception");
        var raw = Render(entry);
        // No \n from exception block — output ends with reset/bracket not a newline
        raw.Should().NotContain("\nerr");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. Pool round-trip
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Return_ThenGet_WriterCanRenderAgain()
    {
        var entry1 = MakeEntry("First");
        var entry2 = MakeEntry("Second");

        var writer = WriterPool.Get<LogColorTextWriter>();
        writer.Render(entry1);
        writer.Return();

        var writer2 = WriterPool.Get<LogColorTextWriter>();
        writer2.Render(entry2);
        var result = writer2.ToString();
        writer2.Return();

        StripAnsi(result).Should().Contain("Second");
        StripAnsi(result).Should().NotContain("First");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. Timestamp visible in stripped output
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_Timestamp_PresentInStrippedOutput()
    {
        var entry = MakeEntry("msg");
        var stripped = StripAnsi(Render(entry));
        // Timestamp "2026" or similar must appear
        stripped.Should().MatchRegex(@"\d{4}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12. Timestamp uses green ANSI color
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_Timestamp_UsesGreenColor()
    {
        var entry = MakeEntry("msg");
        // Green = 92 applied to timestamp
        Render(entry).Should().Contain("\x1b[92m");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 13. SetValueColor — StringColor branch (char / DateTime / DateTimeOffset /
    //     Guid / TimeSpan / Uri) → Magenta = 95
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_CharProperty_UsesMagentaColor()
    {
        var entry = MakeEntry("{V}", props: ['a']);
        Render(entry).Should().Contain("\x1b[95m");
    }

    [Fact]
    public void Render_DateTimeProperty_UsesMagentaColor()
    {
        var entry = MakeEntry("{V}", props: [DateTime.Now]);
        Render(entry).Should().Contain("\x1b[95m");
    }

    [Fact]
    public void Render_DateTimeOffsetProperty_UsesMagentaColor()
    {
        var entry = MakeEntry("{V}", props: [DateTimeOffset.Now]);
        Render(entry).Should().Contain("\x1b[95m");
    }

    [Fact]
    public void Render_GuidProperty_UsesMagentaColor()
    {
        var entry = MakeEntry("{V}", props: [Guid.NewGuid()]);
        Render(entry).Should().Contain("\x1b[95m");
    }

    [Fact]
    public void Render_TimeSpanProperty_UsesMagentaColor()
    {
        var entry = MakeEntry("{V}", props: [TimeSpan.FromSeconds(1)]);
        Render(entry).Should().Contain("\x1b[95m");
    }

    [Fact]
    public void Render_UriProperty_UsesMagentaColor()
    {
        var entry = MakeEntry("{V}", props: [new Uri("https://example.com")]);
        Render(entry).Should().Contain("\x1b[95m");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 14. SetValueColor — NumberColor branch (long / double / decimal) → Yellow = 93
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_LongProperty_UsesYellowColor()
    {
        var entry = MakeEntry("{V}", props: [1L]);
        Render(entry).Should().Contain("\x1b[93m");
    }

    [Fact]
    public void Render_DoubleProperty_UsesYellowColor()
    {
        var entry = MakeEntry("{V}", props: [1.0]);
        Render(entry).Should().Contain("\x1b[93m");
    }

    [Fact]
    public void Render_DecimalProperty_UsesYellowColor()
    {
        var entry = MakeEntry("{V}", props: [1.0m]);
        Render(entry).Should().Contain("\x1b[93m");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 15. BuildFormatStringByHeap — format string > 96 chars
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_LongFormatString_FallsBackToHeapAlloc_DoesNotThrow()
    {
        // format part is 97 chars → triggers BuildFormatStringByHeap
        var longFormat = new string('0', 97);
        var entry = MakeEntry($"{{V:{longFormat}}}", props: [42.0]);
        var raw = Render(entry);
        StripAnsi(raw).Should().NotBeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 16. WriteLevel — unknown LogLevel falls back to "UNK" + White/Yellow
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_UnknownLogLevel_ShowsUnkAbbreviation()
    {
        var entry = MakeEntry("msg", level: (LogLevel)999);
        StripAnsi(Render(entry)).Should().Contain("UNK");
    }

    [Fact]
    public void Render_UnknownLogLevel_UsesWhiteOnYellowColor()
    {
        var entry = MakeEntry("msg", level: (LogLevel)999);
        // White fg (97) + Yellow bg (103)
        Render(entry).Should().Contain("\x1b[97;103m");
    }
}
