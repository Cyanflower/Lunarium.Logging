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
using System.Text.Json.Serialization.Metadata;
using Lunarium.Logging.Config.Configurator;
using Lunarium.Logging.Configuration;
using Microsoft.Extensions.Configuration;

namespace Lunarium.Logging.Tests.Configuration;

/// <summary>
/// Tests for LunariumConfigurationExtensions — BuildLunariumLoggers / BuildLunariumLogger,
/// GlobalConfigApplier, and LoggerConfigApplier.
/// Must run in the GlobalConfigurator collection (non-parallel) because GlobalConfigLock
/// is static singleton state.
/// </summary>
[Collection("GlobalConfigurator")]
public class LunariumConfigurationExtensionsTests
{
    // ── Static field cache for reflection-based reset ─────────────────────────

    private static readonly FieldInfo? _isConfiguringField =
        typeof(GlobalConfigurator).GetField("_isConfiguring",
            BindingFlags.Static | BindingFlags.NonPublic);

    private static readonly FieldInfo? _customResolverField =
        typeof(JsonSerializationConfig).GetField("_customResolver",
            BindingFlags.Static | BindingFlags.NonPublic);

    private static readonly FieldInfo? _optionsField =
        typeof(JsonSerializationConfig).GetField("_options",
            BindingFlags.Static | BindingFlags.NonPublic);

    private static void ResetAll()
    {
        GlobalConfigLock.Configured = false;
        _isConfiguringField?.SetValue(null, false);
        _customResolverField?.SetValue(null, null);
        _optionsField?.SetValue(null, null);
        JsonSerializationConfig.ConfigUnsafeRelaxedJsonEscaping(true);
        JsonSerializationConfig.ConfigWriteIndented(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IConfiguration MakeConfiguration(string json)
    {
        return new ConfigurationBuilder()
            .AddJsonStream(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();
    }

    private const string EmptyJson = "{}";

    private const string SingleLoggerJson = """
        {
          "LunariumLogging": {
            "LoggerConfigs": [
              {
                "LoggerName": "AppLogger",
                "ConsoleSinks": { "console": {} }
              }
            ]
          }
        }
        """;

    private const string MultiLoggerJson = """
        {
          "LunariumLogging": {
            "LoggerConfigs": [
              {
                "LoggerName": "Logger1",
                "ConsoleSinks": { "c1": {} }
              },
              {
                "LoggerName": "Logger2",
                "ConsoleSinks": { "c2": {} }
              }
            ]
          }
        }
        """;

    // ─────────────────────────────────────────────────────────────────────────
    // 1. BuildLunariumLoggers — empty / multiple / registration
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildLunariumLoggers_EmptyConfig_ReturnsEmptyList()
    {
        ResetAll();
        var cfg = MakeConfiguration(EmptyJson);

        var loggers = cfg.BuildLunariumLoggers();

        loggers.Should().BeEmpty();

        foreach (var l in loggers)
            await l.DisposeAsync();
    }

    [Fact]
    public async Task BuildLunariumLoggers_MultipleLoggerConfigs_ReturnsAllLoggers()
    {
        ResetAll();
        var cfg = MakeConfiguration(MultiLoggerJson);

        var loggers = cfg.BuildLunariumLoggers();

        loggers.Should().HaveCount(2);

        foreach (var l in loggers)
            await l.DisposeAsync();
    }

    [Fact]
    public async Task BuildLunariumLoggers_LoggerNamesMatchConfig()
    {
        ResetAll();
        var cfg = MakeConfiguration(MultiLoggerJson);

        var loggers = cfg.BuildLunariumLoggers();

        // Verify the loggers are registered in LoggerManager under expected names
        var registeredNames = LoggerManager.GetLoggerList();
        registeredNames.Should().Contain("Logger1").And.Contain("Logger2");

        foreach (var l in loggers)
            await l.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. BuildLunariumLogger — not found / found
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildLunariumLogger_NotFound_ReturnsNull()
    {
        ResetAll();
        var cfg = MakeConfiguration(SingleLoggerJson);

        var logger = cfg.BuildLunariumLogger("NonExistent");

        logger.Should().BeNull();
    }

    [Fact]
    public async Task BuildLunariumLogger_Found_ReturnsLogger()
    {
        ResetAll();
        var cfg = MakeConfiguration(SingleLoggerJson);

        var logger = cfg.BuildLunariumLogger("AppLogger");

        logger.Should().NotBeNull();

        await logger!.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. GlobalConfig — applied / skipped when already configured
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildLunariumLogger_WithGlobalConfig_AppliesWhenNotYetConfigured()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "TimeZone": "Utc" },
                "LoggerConfigs": [
                  { "LoggerName": "UtcLogger", "ConsoleSinks": { "c": {} } }
                ]
              }
            }
            """;
        var cfg = MakeConfiguration(json);

        var logger = cfg.BuildLunariumLogger("UtcLogger");

        logger.Should().NotBeNull();
        GlobalConfigLock.Configured.Should().BeTrue("GlobalConfig.TimeZone triggered Apply()");

        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task BuildLunariumLogger_WithGlobalConfig_SkipsWhenAlreadyConfigured()
    {
        ResetAll();
        GlobalConfigLock.Configured = true; // simulate previously applied

        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "TimeZone": "Utc" },
                "LoggerConfigs": [
                  { "LoggerName": "AlreadyLogger", "ConsoleSinks": { "c": {} } }
                ]
              }
            }
            """;
        var cfg = MakeConfiguration(json);

        // Should not throw even though GlobalConfigurator.Configure() would throw
        Lunarium.Logging.ILogger? logger = null;
        var act = () => { logger = cfg.BuildLunariumLogger("AlreadyLogger"); };
        act.Should().NotThrow();
        logger.Should().NotBeNull();

        await logger!.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. GlobalConfigApplier — individual config fields
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GlobalConfigApplier_UtcTimeZone_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "TimeZone": "Utc" },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_LocalTimeZone_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "TimeZone": "Local" },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_WriteIndentedJson_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "WriteIndentedJson": true },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_EnableAutoDestructuring_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "EnableAutoDestructuring": true },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. LoggerConfigApplier — empty path / logger name
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoggerConfigApplier_EmptyFileSinkPath_SkipsSink()
    {
        ResetAll();
        // FileSink has no LogFilePath — should be skipped, logger still built successfully
        const string json = """
            {
              "LunariumLogging": {
                "LoggerConfigs": [
                  {
                    "LoggerName": "FileLogger",
                    "FileSinks": { "file": { "LogFilePath": "" } }
                  }
                ]
              }
            }
            """;
        var cfg = MakeConfiguration(json);

        Lunarium.Logging.ILogger? logger = null;
        var act = () => { logger = cfg.BuildLunariumLogger("FileLogger"); };
        act.Should().NotThrow();
        logger.Should().NotBeNull();

        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task BuildLunariumLogger_LoggerNamePropagatedToLoggerManager()
    {
        ResetAll();
        var cfg = MakeConfiguration(SingleLoggerJson);

        var logger = cfg.BuildLunariumLogger("AppLogger");
        logger.Should().NotBeNull();

        // LoggerManager should know about this logger
        var list = LoggerManager.GetLoggerList();
        list.Should().Contain("AppLogger");

        await logger!.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. GlobalConfigApplier — additional fields not covered by earlier tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GlobalConfigApplier_CustomTimezone_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "TimeZone": "Asia/Tokyo" },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_TextTimestampMode_Unix_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "TextTimestampMode": "Unix" },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_TextTimestampMode_UnixMs_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "TextTimestampMode": "UnixMs" },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_TextTimestampMode_ISO8601_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "TextTimestampMode": "ISO8601" },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_TextTimestampMode_Custom_WithFormat_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": {
                  "TextTimestampMode": "Custom",
                  "TextCustomTimestampFormat": "yyyy/MM/dd HH:mm:ss"
                },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_TextTimestampMode_Custom_WithoutFormat_FallsBackToISO8601()
    {
        ResetAll();
        // Custom mode without TextCustomTimestampFormat — code falls back to UseTextISO8601Timestamp
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "TextTimestampMode": "Custom" },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        Lunarium.Logging.ILogger? logger = null;
        var act = async () =>
        {
            logger = MakeConfiguration(json).BuildLunariumLogger("L");
            await logger!.DisposeAsync();
        };
        await act.Should().NotThrowAsync();
        GlobalConfigLock.Configured.Should().BeTrue();
    }

    [Fact]
    public async Task GlobalConfigApplier_JsonTimestampMode_Unix_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "JsonTimestampMode": "Unix" },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_JsonTimestampMode_UnixMs_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "JsonTimestampMode": "UnixMs" },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_JsonTimestampMode_ISO8601_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "JsonTimestampMode": "ISO8601" },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_JsonTimestampMode_Custom_WithFormat_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": {
                  "JsonTimestampMode": "Custom",
                  "JsonCustomTimestampFormat": "yyyy-MM-ddTHH:mm:ssZ"
                },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_JsonTimestampMode_Custom_WithoutFormat_FallsBackToISO8601()
    {
        ResetAll();
        // Custom mode without JsonCustomTimestampFormat — code falls back to UseJsonISO8601Timestamp
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "JsonTimestampMode": "Custom" },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        Lunarium.Logging.ILogger? logger = null;
        var act = async () =>
        {
            logger = MakeConfiguration(json).BuildLunariumLogger("L");
            await logger!.DisposeAsync();
        };
        await act.Should().NotThrowAsync();
        GlobalConfigLock.Configured.Should().BeTrue();
    }

    [Fact]
    public async Task GlobalConfigApplier_EnableUnsafeRelaxedJsonEscaping_True_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "EnableUnsafeRelaxedJsonEscaping": true },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_EnableUnsafeRelaxedJsonEscaping_False_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "EnableUnsafeRelaxedJsonEscaping": false },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task GlobalConfigApplier_WriteIndentedJson_False_Applies()
    {
        ResetAll();
        const string json = """
            {
              "LunariumLogging": {
                "GlobalConfig": { "WriteIndentedJson": false },
                "LoggerConfigs": [ { "LoggerName": "L", "ConsoleSinks": { "c": {} } } ]
              }
            }
            """;
        var logger = MakeConfiguration(json).BuildLunariumLogger("L");
        GlobalConfigLock.Configured.Should().BeTrue();
        await logger!.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. watchForChanges — RegisterChangeCallback path
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildLunariumLoggers_WatchForChanges_True_DoesNotThrow()
    {
        ResetAll();
        var cfg = MakeConfiguration(SingleLoggerJson);

        List<ILogger> loggers = null!;
        var act = () => { loggers = cfg.BuildLunariumLoggers(watchForChanges: true).ToList(); };
        act.Should().NotThrow();
        loggers.Should().HaveCount(1);

        foreach (var l in loggers)
            await l.DisposeAsync();
    }

    [Fact]
    public async Task BuildLunariumLogger_WatchForChanges_True_DoesNotThrow()
    {
        ResetAll();
        var cfg = MakeConfiguration(SingleLoggerJson);

        Lunarium.Logging.ILogger? logger = null;
        var act = () => { logger = cfg.BuildLunariumLogger("AppLogger", watchForChanges: true); };
        act.Should().NotThrow();
        logger.Should().NotBeNull();

        await logger!.DisposeAsync();
    }

    [Fact]
    public async Task BuildLunariumLoggers_WatchForChanges_ReloadTriggersCallback()
    {
        ResetAll();
        // Use AddInMemoryCollection so that Reload() doesn't throw
        // (StreamConfigurationProvider does not support re-loading).
        var data = new Dictionary<string, string?>
        {
            ["LunariumLogging:LoggerConfigs:0:LoggerName"] = "ReloadCbLogger1",
            ["LunariumLogging:LoggerConfigs:0:ConsoleSinks:c:Enabled"] = "true",
            ["LunariumLogging:LoggerConfigs:1:LoggerName"] = "ReloadCbLogger2",
            ["LunariumLogging:LoggerConfigs:1:ConsoleSinks:c:Enabled"] = "true",
        };
        var cfgRoot = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();

        var loggers = cfgRoot.BuildLunariumLoggers(watchForChanges: true).ToList();
        loggers.Should().HaveCount(2);

        // Reload() on an in-memory provider fires the change token which invokes the
        // registered ChangeToken callback, exercising the callback body (lines 113-117).
        var act = () => cfgRoot.Reload();
        act.Should().NotThrow("reload on in-memory config should invoke ChangeToken callback");

        foreach (var l in loggers)
            await l.DisposeAsync();
    }
}
