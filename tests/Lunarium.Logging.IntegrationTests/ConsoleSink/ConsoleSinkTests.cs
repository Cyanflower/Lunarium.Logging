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
using System.Text.RegularExpressions;
using FluentAssertions;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Target;
using Xunit;

namespace Lunarium.Logging.IntegrationTests.ConsoleSinkTests;

// Global constraint for console hijacking.
[CollectionDefinition("ConsoleTests", DisableParallelization = true)]
public class ConsoleTestsCollectionDef { }

[Collection("ConsoleTests")]
public class ConsoleSinkTests : IDisposable
{
    private readonly MemoryStream _stdoutStream;
    private readonly MemoryStream _stderrStream;
    private readonly ConsoleTarget _target;

    public ConsoleSinkTests()
    {
        _stdoutStream = new MemoryStream();
        _stderrStream = new MemoryStream();
        _target = new ConsoleTarget(_stdoutStream, _stderrStream);
    }

    public void Dispose()
    {
        _stdoutStream.Dispose();
        _stderrStream.Dispose();
        _target.Dispose();
    }

    private string GetStdout() => Encoding.UTF8.GetString(_stdoutStream.ToArray());
    private string GetStderr() => Encoding.UTF8.GetString(_stderrStream.ToArray());

    private static LogEntry MakeEntry(LogLevel level, string message)
    {
        return new LogEntry(
            loggerName: "ConsoleScope",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("ConsoleScope"),
            timestamp: DateTimeOffset.UtcNow,
            logLevel: level,
            message: message,
            properties: [],
            context: "",
            contextBytes: default,
            scope: "",
            messageTemplate: LogParser.ParseMessage(message),
            exception: null
        );
    }

    [Fact]
    public void Emit_InfoLevel_ToConsoleOut()
    {
        var entry = MakeEntry(LogLevel.Info, "Hello out");
        _target.Emit(entry);

        GetStdout().Should().Contain("Hello out");
        GetStderr().Should().BeEmpty();
    }

    [Fact]
    public void Emit_ErrorLevel_ToConsoleError()
    {
        var entry = MakeEntry(LogLevel.Error, "Hello error");
        _target.Emit(entry);

        GetStderr().Should().Contain("Hello error");
        GetStdout().Should().BeEmpty();
    }

    [Fact]
    public void Emit_FatalLevel_ToConsoleError()
    {
        var entry = MakeEntry(LogLevel.Critical, "Hello fatal");
        _target.Emit(entry);

        GetStderr().Should().Contain("Hello fatal");
    }

    [Fact]
    public void Emit_WithJson_OutputsJsonFormat()
    {
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        var target = new ConsoleTarget(stdout, stderr, toJson: true, isColor: null);
        var entry = MakeEntry(LogLevel.Debug, "Hello json");
        target.Emit(entry);

        var output = Encoding.UTF8.GetString(stdout.ToArray());
        output.Should().Contain("\"Level\":\"Debug\"");
        output.Should().Contain("\"OriginalMessage\":\"Hello json\"");
    }

    [Fact]
    public void Emit_WithIsColorFalse_OutputsPlainTextWithoutAnsi()
    {
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        var target = new ConsoleTarget(stdout, stderr) { IsColor = false };
        var entry = MakeEntry(LogLevel.Info, "plain text test");
        target.Emit(entry);

        var output = Encoding.UTF8.GetString(stdout.ToArray());
        output.Should().Contain("plain text test");
        output.Should().NotContain("\x1b[");
    }

    [Fact]
    public void Emit_WithToJsonAndErrorLevel_OutputsJsonToErrorStream()
    {
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        var target = new ConsoleTarget(stdout, stderr) { ToJson = true };
        var entry = MakeEntry(LogLevel.Error, "json error message");
        target.Emit(entry);

        Encoding.UTF8.GetString(stderr.ToArray()).Should().Contain("\"Level\":\"Error\"");
        Encoding.UTF8.GetString(stdout.ToArray()).Should().BeEmpty();
    }

    [Fact]
    public void Emit_ToJson_TrueAndFalseBehaveDifferently()
    {
        using var stdout1 = new MemoryStream();
        using var stdout2 = new MemoryStream();
        var jsonTarget = new ConsoleTarget(stdout1, new MemoryStream(), toJson: true, isColor: null);
        var plainTarget = new ConsoleTarget(stdout2, new MemoryStream(), toJson: false, isColor: null);

        jsonTarget.Emit(MakeEntry(LogLevel.Info, "json message"));
        plainTarget.Emit(MakeEntry(LogLevel.Info, "plain message"));

        var jsonOutput = Encoding.UTF8.GetString(stdout1.ToArray());
        var plainOutput = Encoding.UTF8.GetString(stdout2.ToArray());

        jsonOutput.Should().Contain("\"Level\"");       // JSON format
        plainOutput.Should().Contain("plain message");  // plain text format
        plainOutput.Should().NotContain("\"Level\"");
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes_DoesNotThrow()
    {
        var target = new ConsoleTarget();
        Action act = () => { target.Dispose(); target.Dispose(); };
        act.Should().NotThrow();
    }
}
