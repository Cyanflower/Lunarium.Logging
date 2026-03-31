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

using System.Text;
using FluentAssertions;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Target;
using Xunit;

namespace Lunarium.Logging.Tests.Target;

/// <summary>
/// Tests for ConsoleTarget — using the internal (Stream, Stream) constructor
/// to inject MemoryStreams and avoid writing to the real console.
/// </summary>
public class ConsoleTargetTests
{
    private static LogEntry MakeEntry(string msg = "test message", LogLevel level = LogLevel.Info) =>
        new LogEntry(
            loggerName: "ConsoleTest",
            loggerNameBytes: Encoding.UTF8.GetBytes("ConsoleTest"),
            timestamp: DateTimeOffset.UtcNow,
            logLevel: level,
            message: msg,
            properties: [],
            context: "",
            contextBytes: default,
            scope: "",
            messageTemplate: LogParser.ParseMessage(msg));

    // ─────────────────────────────────────────────────────────────────────────
    // 1. UpdateTextOutputIncludeConfig
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ConsoleTarget_UpdateTextOutputIncludeConfig_UpdatesValue()
    {
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        var target = new ConsoleTarget(stdout, stderr);

        target.UpdateTextOutputIncludeConfig(new TextOutputIncludeConfig { IncludeLoggerName = false });

        target.GetTextOutputIncludeConfig().IncludeLoggerName.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Emit routes Info → stdout, Error → stderr
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ConsoleTarget_Emit_InfoLevel_WritesToStdout()
    {
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        var target = new ConsoleTarget(stdout, stderr, toJson: false, isColor: false);

        target.Emit(MakeEntry("hello info", LogLevel.Info));

        stdout.Length.Should().BeGreaterThan(0, "Info goes to stdout");
        stderr.Length.Should().Be(0, "stderr should be empty for Info");
    }

    [Fact]
    public void ConsoleTarget_Emit_ErrorLevel_WritesToStderr()
    {
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        var target = new ConsoleTarget(stdout, stderr, toJson: false, isColor: false);

        target.Emit(MakeEntry("an error", LogLevel.Error));

        stderr.Length.Should().BeGreaterThan(0, "Error goes to stderr");
        stdout.Length.Should().Be(0, "stdout should be empty for Error");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. JSON output
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ConsoleTarget_Emit_WithToJsonTrue_ProducesJsonOutput()
    {
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        var target = new ConsoleTarget(stdout, stderr, toJson: true, isColor: false);

        target.Emit(MakeEntry("json message", LogLevel.Info));

        var output = Encoding.UTF8.GetString(stdout.ToArray());
        output.Should().Contain("\"Level\"", "JSON output should include Level field");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Plain-text output (isColor=false)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ConsoleTarget_Emit_WithIsColorFalse_ProducesPlainText()
    {
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        var target = new ConsoleTarget(stdout, stderr, toJson: false, isColor: false);

        target.Emit(MakeEntry("plain text", LogLevel.Info));

        var output = Encoding.UTF8.GetString(stdout.ToArray());
        output.Should().Contain("[INF]", "plain-text output should contain level abbreviation");
        output.Should().NotContain("\x1b[", "plain-text should not contain ANSI escape sequences");
    }
}
