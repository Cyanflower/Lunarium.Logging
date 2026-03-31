# appsettings.json

> Full runnable example: [ConfigurationIntegration.EN.cs](https://github.com/Cyanflower/Lunarium.Logging/blob/main/example/RawCSharp/EN/ConfigurationIntegration.EN.cs)  
> Required package: `Lunarium.Logging.Configuration`

```xml
<PackageReference Include="Lunarium.Logging.Configuration" Version="*" />
```

## appsettings.json Structure

The root key defaults to `"LunariumLogging"`.

```json
{
  "LunariumLogging": {
    "GlobalConfig": {
      "TimeZone": "Asia/Shanghai",
      "TextTimestampMode": "Custom",
      "TextCustomTimestampFormat": "yyyy-MM-dd HH:mm:ss.fff",
      "JsonTimestampMode": "ISO8601",
      "EnableUnsafeRelaxedJsonEscaping": true
    },
    "LoggerConfigs": [
      {
        "LoggerName": "Runtime",
        "ConsoleSinks": {
          "console": {
            "Enabled": true,
            "IsColor": true,
            "FilterConfig": { "LogMinLevel": "Info" }
          }
        },
        "FileSinks": {
          "main": {
            "LogFilePath": "Logs/app.log",
            "MaxFileSizeMB": 10,
            "MaxFile": 5,
            "FilterConfig": { "LogMinLevel": "Info" }
          },
          "errors": {
            "LogFilePath": "Logs/error.log",
            "RotateOnNewDay": true,
            "MaxFile": 30,
            "FilterConfig": { "LogMinLevel": "Error" }
          }
        }
      }
    ]
  }
}
```

## Build Loggers from IConfiguration

```csharp
IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

// Build a single logger by name
ILogger? logger = config.BuildLunariumLogger("Runtime");

// Build all loggers defined in the config
IReadOnlyList<ILogger> loggers = config.BuildLunariumLoggers();
```

## Hot Reload

Filter changes apply at runtime without restarting. `GlobalConfig` changes are ignored after the first `Build()`.

```csharp
ILogger? logger = config.BuildLunariumLogger(
    loggerName:     "Runtime",
    watchForChanges: true);   // monitor IConfiguration for changes
```

When `watchForChanges: true`, any change to the configuration section triggers `LoggerManager.UpdateAllLoggerConfig()` automatically.

## With Generic Host

```csharp
builder.Logging.UseLunariumLog(
    configuration:   builder.Configuration,
    loggerName:      "Runtime",
    sectionKey:      "LunariumLogging",    // default
    watchForChanges: true);
```
