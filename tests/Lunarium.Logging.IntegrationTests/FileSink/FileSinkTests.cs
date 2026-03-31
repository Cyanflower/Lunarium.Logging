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

using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Lunarium.Logging.Models;
using Lunarium.Logging.Parser;
using Lunarium.Logging.Target;
using Xunit;

namespace Lunarium.Logging.IntegrationTests.FileTargetTests;

public class FileTargetTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _logBaseFilePath;

    public FileTargetTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LunariumLoggerTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _logBaseFilePath = Path.Combine(_tempDir, "app.log");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch { /* Best effort */ }
        }
    }

    // Windows 共享模型要求：读取仍被 FileTarget 持有的文件时，
    // reader 的 FileShare 必须包含 Write，以允许已存在的写句柄继续持有。
    // Linux 无此限制，但显式共享在两个平台上均能正常工作。
    private static string ReadShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static LogEntry MakeEntry(string message, LogLevel level = LogLevel.Info)
    {
        return new LogEntry(
            loggerName: "FileScope",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("FileScope"),
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
    public void Emit_BasicWrite_SuccessfullyWritesToFile()
    {
        var target = new FileTarget(_logBaseFilePath);
        var entry = MakeEntry("Hello basic write");

        target.Emit(entry);
        target.Dispose();

        File.Exists(_logBaseFilePath).Should().BeTrue();
        var content = File.ReadAllText(_logBaseFilePath);
        content.Should().Contain("Hello basic write");
    }

    [Fact]
    public void Emit_WhenErrorLevel_FlushesImmediately()
    {
        var target = new FileTarget(_logBaseFilePath);
        var entry = MakeEntry("Error flush now!", LogLevel.Error);

        target.Emit(entry);
        // Error level forces an immediate flush

        File.Exists(_logBaseFilePath).Should().BeTrue();
        var content = ReadShared(_logBaseFilePath);
        content.Should().Contain("Error flush now!");
        
        target.Dispose();
    }

    [Fact]
    public void Emit_WhenMaxFileSizeReached_RotatesFile()
    {
        // Set max file size to 100 bytes. Convert back to MB for constructor:
        double maxFileSizeMB = 100.0 / (1024 * 1024);
        var target = new FileTarget(_logBaseFilePath, maxFileSizeMB: maxFileSizeMB, maxFile: 0);

        // Write a lot of small entries to exceed 100 bytes
        // Use LogLevel.Error to ensure immediate flush so `_currentFileSize` updates!
        for (int i = 0; i < 5; i++)
        {
            target.Emit(MakeEntry($"Line {i:D3} to bypass limit we write some data padding this out.", LogLevel.Error));
        }
        
        // Wait 1 second to ensure the next rotation gets a new filename 
        // because FileTarget timestamp resolution is by second (yyyy-MM-dd-HH-mm-ss).
        System.Threading.Thread.Sleep(1050);

        // Write one more to trigger the rotation check and split to a new file
        target.Emit(MakeEntry("Line that triggers rotation", LogLevel.Error));

        target.Dispose(); // Ensure flushed

        // Check the directory contents. Should have more than 1 file named something like app-yyyy-mm-dd-hh-mm-ss.log
        var files = Directory.GetFiles(_tempDir, "app-*.log");
        files.Length.Should().BeGreaterThan(1); // It rotated
    }

    [Fact]
    public void CheckForRotation_RotatesOnNewDay()
    {
        using var target = new FileTarget(_logBaseFilePath, rotateOnNewDay: true);

        // Write day 1 (Fake the entry timestamp to be yesterday)
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        var entry1 = new LogEntry(
            loggerName: "FileScope",
            loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("FileScope"),
            timestamp: yesterday,
            logLevel: LogLevel.Error,
            message: "Day 1",
            properties: [],
            context: "",
            contextBytes: default,
            scope: "",
            messageTemplate: LogParser.ParseMessage("Day 1"),
            exception: null
        );
        target.Emit(entry1);

        // Write day 2 — Should trigger rotation because entry2 has UTC Now
        var entry2 = MakeEntry("Day 2", LogLevel.Error);
        target.Emit(entry2);

        // There should be a file for yesterday, and one for "Day 2" (today). 
        var files = Directory.GetFiles(_tempDir, "app-*.log");
        files.Length.Should().Be(2); 
    }

    [Fact]
    public void CleanupOldFiles_DeletesOldestFiles_WhenLimitReached()
    {
        double maxFileSizeMB = 100.0 / (1024 * 1024);
        using var target = new FileTarget(_logBaseFilePath, maxFileSizeMB: maxFileSizeMB, maxFile: 2);

        // Write enough to trigger rotation multiple times
        for (int i = 0; i < 40; i++)
        {
            target.Emit(MakeEntry($"Line {i:D3}", LogLevel.Error));
            // Put a tiny sleep to ensure file name timestamp changes (if fast enough, they might collide within 1s)
            Thread.Sleep(50);
        }

        var files = Directory.GetFiles(_tempDir, "app-*.log");
        // Because maxFile = 2, we should have exactly 2 files left in the directory
        files.Length.Should().BeLessOrEqualTo(2); 
    }

    [Fact]
    public void Constructor_InvalidConfig_Throws()
    {
        // When maxFile > 0, we MUST have either rotateOnNewDay OR maxFileSize > 0.
        Action act = () => new FileTarget(_logBaseFilePath, maxFileSizeMB: 0, rotateOnNewDay: false, maxFile: 5);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Emit_WithToJsonTrue_WritesJsonFormat()
    {
        var target = new FileTarget(_logBaseFilePath) { ToJson = true };
        var entry = MakeEntry("json write test", LogLevel.Error); // Error for immediate flush
        target.Emit(entry);
        target.Dispose();

        var content = File.ReadAllText(_logBaseFilePath);
        content.Should().Contain("\"Level\":");
        content.Should().Contain("\"OriginalMessage\":");
    }

    [Fact]
    public void Emit_WithToJsonFalse_WritesTextFormat()
    {
        var target = new FileTarget(_logBaseFilePath) { ToJson = false };
        var entry = MakeEntry("text write test", LogLevel.Error);
        target.Emit(entry);
        target.Dispose();

        var content = File.ReadAllText(_logBaseFilePath);
        content.Should().Contain("text write test");
        content.Should().NotContain("\"Level\":");
        (content.Contains("[INF]") || content.Contains("[ERR]")).Should().BeTrue();
    }

    [Fact]
    public void Emit_AfterDispose_DoesNotThrow()
    {
        var target = new FileTarget(_logBaseFilePath);
        target.Dispose();
        var entry = MakeEntry("after dispose");
        Action act = () => target.Emit(entry);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureFileExists_WhenFileDeletedExternally_RecreatesFileAndContinuesWriting()
    {
        var target = new FileTarget(_logBaseFilePath);

        // Error level forces immediate flush before we delete
        target.Emit(MakeEntry("before delete", LogLevel.Error));
        File.Exists(_logBaseFilePath).Should().BeTrue();

        // Delete the file externally while the target still holds the writer
        File.Delete(_logBaseFilePath);

        // This emit triggers EnsureFileExists: detects file missing → CloseCurrentWriter → OpenFile
        // Its own content went to the old writer's buffer before the reopen, so it ends up in
        // the deleted inode and is effectively lost — this is the expected behavior.
        target.Emit(MakeEntry("trigger reopen", LogLevel.Error));

        // File should be recreated at this point
        File.Exists(_logBaseFilePath).Should().BeTrue();

        // This third entry goes entirely through the new writer → must appear in the file
        target.Emit(MakeEntry("written to new file", LogLevel.Error));

        ReadShared(_logBaseFilePath).Should().Contain("written to new file");

        target.Dispose();
    }

    [Fact]
    public void FindLatestLogFileOrCreateNew_ReusesExistingUnderSizeFile()
    {
        // Large limit — a single tiny entry will not fill it
        double maxFileSizeMB = 10;
        var target1 = new FileTarget(_logBaseFilePath, maxFileSizeMB: maxFileSizeMB);
        target1.Emit(MakeEntry("first write", LogLevel.Error));
        target1.Dispose();

        var filesAfterFirst = Directory.GetFiles(_tempDir, "app-*.log");
        filesAfterFirst.Should().HaveCount(1);

        // Second target should reuse the existing file (still under size limit)
        var target2 = new FileTarget(_logBaseFilePath, maxFileSizeMB: maxFileSizeMB);
        target2.Emit(MakeEntry("second write", LogLevel.Error));
        target2.Dispose();

        var filesAfterSecond = Directory.GetFiles(_tempDir, "app-*.log");
        filesAfterSecond.Should().HaveCount(1); // reused, not new file
        var content = File.ReadAllText(filesAfterSecond[0]);
        content.Should().Contain("first write");
        content.Should().Contain("second write");
    }

    [Fact]
    public void FindLatestLogFileOrCreateNew_CombinedMode_ReusesUnderSizeTodayFile()
    {
        // Combined mode: both sized and timed rotation — covers the todayPrefix filter branch
        double maxFileSizeMB = 10;
        var target1 = new FileTarget(_logBaseFilePath, maxFileSizeMB: maxFileSizeMB, rotateOnNewDay: true);
        target1.Emit(MakeEntry("combined first", LogLevel.Error));
        target1.Dispose();

        var filesAfterFirst = Directory.GetFiles(_tempDir, "app-*.log");
        filesAfterFirst.Should().HaveCount(1);

        // Second target in combined mode: applies todayPrefix filter, finds today's file under limit → reuse
        var target2 = new FileTarget(_logBaseFilePath, maxFileSizeMB: maxFileSizeMB, rotateOnNewDay: true);
        target2.Emit(MakeEntry("combined second", LogLevel.Error));
        target2.Dispose();

        var filesAfterSecond = Directory.GetFiles(_tempDir, "app-*.log");
        filesAfterSecond.Should().HaveCount(1);
        var content = File.ReadAllText(filesAfterSecond[0]);
        content.Should().Contain("combined first");
        content.Should().Contain("combined second");
    }

    [Fact]
    public void CleanupOldFiles_TimedRotation_WithMaxFile_DeletesOldestFiles()
    {
        // Timed-only rotation + maxFile → CleanupOldFiles uses date-only regex pattern
        using var target = new FileTarget(_logBaseFilePath, rotateOnNewDay: true, maxFile: 2);

        var baseTime = DateTimeOffset.UtcNow.AddDays(-5);

        // Send 3 entries each with a different UTC date → triggers rotation twice
        for (int i = 0; i < 3; i++)
        {
            var ts = baseTime.AddDays(i);
            var entry = new LogEntry(
                loggerName: "FileScope",
                loggerNameBytes: System.Text.Encoding.UTF8.GetBytes("FileScope"),
                timestamp: ts,
                logLevel: LogLevel.Error,
                message: $"day {i}",
                properties: [],
                context: "",
                contextBytes: default,
                scope: "",
                messageTemplate: LogParser.ParseMessage($"day {i}"));
            target.Emit(entry);
        }

        // With maxFile = 2, oldest file should be cleaned up
        var files = Directory.GetFiles(_tempDir, "app-????-??-??.log");
        files.Length.Should().BeLessOrEqualTo(2);
    }
}
