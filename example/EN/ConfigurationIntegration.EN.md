# ConfigurationIntegration — appsettings.json Integration

> Full code version: [RawCSharp/ConfigurationIntegration.EN.cs](RawCSharp/ConfigurationIntegration.EN.cs)
> Required NuGet package: `Lunarium.Logging.Configuration`
> For Host/DI integration, also reference: `Lunarium.Logging.Hosting`

---

## Section 1: Complete appsettings.json Structure

The configuration section key defaults to `"LunariumLogging"` (can be changed via the `sectionKey` parameter).

```json
{
  "LunariumLogging": {
    "GlobalConfig": {
      "TimeZone": "Asia/Shanghai",
      "TextTimestampMode": "Custom",
      "TextCustomTimestampFormat": "yyyy-MM-dd HH:mm:ss.fff zzz",
      "JsonTimestampMode": "ISO8601",
      "EnableUnsafeRelaxedJsonEscaping": true,
      "WriteIndentedJson": false,
      "EnableAutoDestructuring": true
    },
    "LoggerConfigs": [
      {
        "LoggerName": "Runtime",
        "ConsoleSinks": {
          "console": {
            "Enabled": true,
            "ToJson": false,
            "IsColor": true,
            "FilterConfig": {
              "LogMinLevel": "Info"
            }
          }
        },
        "FileSinks": {
          "main": {
            "Enabled": true,
            "LogFilePath": "Logs/Runtime.log",
            "MaxFileSizeMB": 10,
            "MaxFile": 5,
            "RotateOnNewDay": false,
            "FilterConfig": {
              "LogMinLevel": "Info",
              "ContextFilterExcludes": ["Runtime.Proxy"]
            }
          },
          "error": {
            "Enabled": true,
            "LogFilePath": "Logs/Error.log",
            "MaxFileSizeMB": 5,
            "MaxFile": 30,
            "RotateOnNewDay": true,
            "FilterConfig": {
              "LogMinLevel": "Error"
            }
          }
        }
      }
    ]
  }
}
```

> Sink names (`"console"`, `"main"`, etc.) correspond to dictionary keys. After building, they are registered in `LoggerManager` as `sinkName` values, enabling runtime hot-updates via `UpdateSinkConfig(loggerName, sinkName, ...)`.

---

## Section 2: GlobalConfig Field Reference

All fields are nullable; `null` means use the library's default value.

| JSON field | Type | Allowed values | Default |
|------------|------|----------------|---------|
| `TimeZone` | string | `"Local"` / `"Utc"` / `"Asia/Shanghai"` or any IANA time zone ID | Local time |
| `TextTimestampMode` | string | `"Unix"` / `"UnixMs"` / `"ISO8601"` / `"Custom"` | `Custom` |
| `TextCustomTimestampFormat` | string | Only effective when `TextTimestampMode=Custom` | `"yyyy-MM-dd HH:mm:ss.fff"` |
| `JsonTimestampMode` | string | `"Unix"` / `"UnixMs"` / `"ISO8601"` / `"Custom"` | `ISO8601` |
| `JsonCustomTimestampFormat` | string | Only effective when `JsonTimestampMode=Custom` | — |
| `EnableUnsafeRelaxedJsonEscaping` | bool | `true` = CJK/Emoji not escaped (recommended) | `true` |
| `WriteIndentedJson` | bool | `true` = pretty-print; `false` = compact single line | `false` |
| `EnableAutoDestructuring` | bool | `true` = collections auto-destructured as JSON | `false` |

> ⚠️ `GlobalConfig` does NOT support hot-reload (one-time global design).

---

## Section 3: BuildLunariumLogger — Build a Single Logger by Name

```csharp
ILogger? logger = configuration.BuildLunariumLogger(
    loggerName:      "Runtime",
    sectionKey:      "LunariumLogging",   // default value; can be omitted
    watchForChanges: false);

if (logger is null)
    // No Logger configuration found with the specified LoggerName
```

---

## Section 4: BuildLunariumLoggers — Build All Loggers

```csharp
IReadOnlyList<ILogger> loggers = configuration.BuildLunariumLoggers(
    sectionKey:      "LunariumLogging",
    watchForChanges: true);   // enable hot-reload
```

---

## Section 5: Hot-Reload Mechanism

```
watchForChanges: true
       ↓
ChangeToken.OnChange(config.GetReloadToken(), ...)
       ↓
LoggerManager.UpdateAllLoggerConfig(newLoggerConfigs)
```

**Fields that support hot-reload (take effect immediately after saving the config file):**
- `FilterConfig.LogMinLevel` / `LogMaxLevel`
- `FilterConfig.ContextFilterIncludes` / `ContextFilterExcludes`
- `Sink.Enabled`

**Fields that do NOT support hot-reload (require restart):**
- `GlobalConfig` (time zone, timestamp format, etc.)
- `LogFilePath` (requires rebuilding FileTarget)
- `LoggerName`

---

## Section 6: Combined Usage with the Hosting Package

Read configuration from `appsettings.json` and register as a MEL Provider in one step.

```csharp
// WebApplication style
builder.Logging.ClearProviders();
builder.Logging.UseLunariumLog(
    configuration: builder.Configuration,
    loggerName: "Runtime",
    sectionKey: "LunariumLogging",    // default value
    watchForChanges: true);

// Or IHostBuilder style
Host.CreateDefaultBuilder()
    .UseLunariumLog(
        configuration: context.Configuration,
        loggerName: "Runtime",
        watchForChanges: true)
    .Build()
    .Run();
```
