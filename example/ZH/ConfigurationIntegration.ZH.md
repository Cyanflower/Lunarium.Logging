# ConfigurationIntegration — appsettings.json 集成

> 完整代码版本：[RawCSharp/ConfigurationIntegration.ZH.cs](RawCSharp/ConfigurationIntegration.ZH.cs)
> 需引用 NuGet 包：`Lunarium.Logging.Configuration`
> 与 Host/DI 集成时还需：`Lunarium.Logging.Hosting`

---

## 第一节：appsettings.json 完整结构

配置节点默认为 `"LunariumLogging"`（可通过 `sectionKey` 参数修改）。

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

> Sink 名称（`"console"`、`"main"` 等）对应字典的键，构建后作为 `sinkName` 注册到 `LoggerManager`，可用于运行时 `UpdateSinkConfig(loggerName, sinkName, ...)` 热更新。

---

## 第二节：GlobalConfig 字段对照表

所有字段均可空，`null` 表示使用主库默认值。

| JSON 字段 | 类型 | 可选值 | 默认值 |
|-----------|------|--------|--------|
| `TimeZone` | string | `"Local"` / `"Utc"` / `"Asia/Shanghai"` 等 IANA 时区 ID | 本地时间 |
| `TextTimestampMode` | string | `"Unix"` / `"UnixMs"` / `"ISO8601"` / `"Custom"` | `Custom` |
| `TextCustomTimestampFormat` | string | 仅 `TextTimestampMode=Custom` 时生效 | `"yyyy-MM-dd HH:mm:ss.fff"` |
| `JsonTimestampMode` | string | `"Unix"` / `"UnixMs"` / `"ISO8601"` / `"Custom"` | `ISO8601` |
| `JsonCustomTimestampFormat` | string | 仅 `JsonTimestampMode=Custom` 时生效 | — |
| `EnableUnsafeRelaxedJsonEscaping` | bool | `true` = 中文/Emoji 不转义（推荐） | `true` |
| `WriteIndentedJson` | bool | `true` = 缩进；`false` = 紧凑单行 | `false` |
| `EnableAutoDestructuring` | bool | `true` = 集合类型自动解构为 JSON | `false` |

> ⚠️ `GlobalConfig` 不支持热更新（一次性全局设计）。

---

## 第三节：BuildLunariumLogger — 按名称构建单个 Logger

```csharp
ILogger? logger = configuration.BuildLunariumLogger(
    loggerName:     "Runtime",
    sectionKey:     "LunariumLogging",   // 默认值，可省略
    watchForChanges: false);

if (logger is null)
    // 配置中找不到指定 LoggerName
```

---

## 第四节：BuildLunariumLoggers — 构建全部 Logger

```csharp
IReadOnlyList<ILogger> loggers = configuration.BuildLunariumLoggers(
    sectionKey:     "LunariumLogging",
    watchForChanges: true);   // 启用热更新
```

---

## 第五节：热更新机制

```
watchForChanges: true
       ↓
ChangeToken.OnChange(config.GetReloadToken(), ...)
       ↓
LoggerManager.UpdateAllLoggerConfig(newLoggerConfigs)
```

**支持热更新的字段（修改配置文件保存后立即生效）：**
- `FilterConfig.LogMinLevel` / `LogMaxLevel`
- `FilterConfig.ContextFilterIncludes` / `ContextFilterExcludes`
- `Sink.Enabled`

**不支持热更新（需重启）：**
- `GlobalConfig`（时区、时间戳格式等）
- `LogFilePath`（需重建 FileTarget）
- `LoggerName`

---

## 第六节：与 Hosting 包结合使用

从 `appsettings.json` 读取配置并注册为 MEL Provider，一步完成。

```csharp
// WebApplication 写法
builder.Logging.ClearProviders();
builder.Logging.UseLunariumLog(
    configuration: builder.Configuration,
    loggerName: "Runtime",
    sectionKey: "LunariumLogging",    // 默认值
    watchForChanges: true);

// 或 IHostBuilder 写法
Host.CreateDefaultBuilder()
    .UseLunariumLog(
        configuration: context.Configuration,
        loggerName: "Runtime",
        watchForChanges: true)
    .Build()
    .Run();
```
