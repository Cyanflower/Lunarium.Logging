# Lunarium.Logging.Tests 代码索引

此索引专为 AI 和开发者提供，记录了各个测试文件所覆盖的 **目标类 (Target Class)** 和详细的 **测试场景 (Test Cases)**。
AI 在修改核心库的代码时，可通过搜索此文档快速定位受影响的测试类和方法，避免全局扫描。

---

## Config/（配置相关测试）

### GlobalConfiguratorTests.cs
- **目标类**: `GlobalConfigurator`, `GlobalConfigLock`, `LogTimestampConfig`, `TimestampFormatConfig`, `DestructuringConfig`, `JsonSerializationConfig`
- **主要测试场景**:
  - **双重配置防御（第1节，5个测试）**：
    - `Configure_WhenAlreadyConfigured_Throws`：已完成配置后再次调用 `Configure()` 应抛 `InvalidOperationException` 且消息含 `already`。
    - `ApplyConfiguration_WhenNotConfiguring_Throws`：未处于配置流程中直接调用 `ApplyConfiguration()` 应抛 `InvalidOperationException` 且消息含 `No configuration in progress`。
    - `AddConfigOperation_WhenNotConfiguring_Throws`：未处于配置流程中调用 `AddConfigOperation()` 应抛 `InvalidOperationException`。
    - `Configure_WhenNotYetConfigured_ReturnsBuilder`：未配置过时 `Configure()` 正常返回非 null 的 Builder 实例。
    - `Configure_WhileInProgress_Throws`：在调用 `Configure()` 之后、`Apply()` 之前再次调用 `Configure()` 应抛 `InvalidOperationException` 且消息含 `progress`。
  - **Apply() 锁定配置（第2节，1个测试）**：
    - `Apply_SetsConfiguredToTrue`：调用 `Apply()` 后 `GlobalConfigLock.Configured` 变为 `true`。
  - **时区配置（第3节，4个测试）**：
    - `UseUtcTimeZone_SetsTimestampToUtc`：配置 UTC 后时间戳偏移为 `TimeSpan.Zero`。
    - `UseLocalTimeZone_SetsTimestampToLocal`：配置本地时区后时间戳偏移与系统本地一致。
    - `UseCustomTimezone_SetsTimestampToGivenZone`：传入 `Asia/Tokyo` 后时间戳偏移与该时区匹配。
    - `UseCustomTimezone_NullArg_Throws`：传入 `null` 时立即抛 `ArgumentNullException`。
  - **JSON 时间戳格式（第4节，5个测试）**：
    - 分别验证 `UseJsonUnixTimestamp`、`UseJsonUnixMsTimestamp`、`UseJsonISO8601Timestamp` 设置后 `TimestampFormatConfig.JsonMode` 的枚举值正确。
    - `UseJsonCustomTimestamp_SetsJsonModeToCustomAndStoresFormat`：自定义格式字符串被正确存入 `JsonCustomFormat`。
    - `UseJsonCustomTimestamp_NullOrWhiteSpace_Throws`：空白字符串立即抛 `ArgumentException`。
  - **文本时间戳格式（第5节，5个测试）**：同上，针对 `TextMode` 的四种模式及空白字符串防御。
  - **自动解构（第6节，1个测试）**：
    - `EnableAutoDestructuring_SetsAutoDestructureCollectionsTrue`：调用后 `DestructuringConfig.AutoDestructureCollections` 为 `true`。
  - **JSON 序列化选项（第7节，4个测试）**：
    - `PreserveChineseCharacters_ConfiguresNonEscapedChinese`：调用后 `JsonSerializationConfig.Options` 非 null。
    - `EscapeChineseCharacters_CanBeChained`：链式调用不抛异常。
    - `UseIndentedJson_SetsWriteIndentedTrue` / `UseCompactJson_SetsWriteIndentedFalse`：验证 `Options.WriteIndented` 的开关控制。
  - **UseJsonTypeInfoResolver（第8节，4个测试）**：
    - 两个 null 参数防御测试（`IJsonTypeInfoResolver` 和 `JsonSerializerContext` 重载各一）。
    - 两个注册验证测试：传入的 Resolver 或 Context 出现在 `Options.TypeInfoResolverChain` 中。
  - **链式多设置（第10节，1个测试）**：
    - `Configure_MultipleSettings_AllApplied`：在一次链式调用中设置 UTC、UnixTimestamp、ISO8601、AutoDestructuring、CompactJson，验证所有配置均生效。
  - **ApplyDefaultIfNotConfigured（第11节，2个测试）**：
    - `ApplyDefaultIfNotConfigured_WhenNotConfigured_SetsDefaults`：未配置时调用后 `Configured` 为 `true`，`JsonMode` 为 `ISO8601`，`TextMode` 为 `Custom`。
    - `ApplyDefaultIfNotConfigured_WhenAlreadyConfigured_IsNoop`：已配置时调用不覆盖已有设置（Unix 模式保持不变）。

### FilterConfigTests.cs
- **目标类**: `FilterConfig`
- **主要测试场景**:
  - **默认值验证（第1节，1个测试）**：`LogMinLevel` 为 `Info`、`LogMaxLevel` 为 `Critical`、`ContextFilterIncludes/Excludes` 为 `null`、`IgnoreFilterCase` 为 `false`、`TextOutputIncludeConfig` 为 `null`。
  - **IgnoreFilterCase 驱动 ComparisonType（第2节，2个测试）**：`true` → `OrdinalIgnoreCase`；`false` → `Ordinal`。
  - **自定义级别范围（第3节，1个测试）**：设置 `Error`~`Critical` 范围后读取正确。
  - **TextOutputIncludeConfig 标志（第4节，1个测试）**：设置 `IncludeLoggerName=false` 后可正确读取。

---

## Core/（核心日志派发与工具）

### InternalLoggerTests.cs
- **目标类**: `InternalLogger`（位于 `Lunarium.Logging.InternalLoggerUtils` 命名空间）
- **主要测试场景**:
  - `InternalLogger_AllOverloads_WriteToFileAndDoNotCrash`：调用全部四个重载（`Error(string)`、`Error(Exception)`、`Error(Exception, string)`、`Error(string, Exception)`）后，验证当日的内部日志文件（`LunariumLogger-internal-yyyyMMdd.log`）存在且包含各条目内容，整个过程不抛任何异常。

### LogUtilsTests.cs（路径：`Core/LogUtilsTests.cs`）
- **目标类**: `LogUtils`、`LogTimestampConfig`、`TimestampFormatConfig`
- **主要测试场景**:
  - `GetLogSystemTimestamp_ReturnsCurrentTimeWithCorrectOffset`：切换为 UTC 模式后，返回的时间戳偏移为 `TimeSpan.Zero`。
  - `GetLogSystemFormattedTimestamp_Fallback_ReturnsIso8601`：将 `TextMode` 强制设为无效枚举值（`(TextTimestampMode)999`），验证返回值非空（ISO8601 回退防线）。

### LoggerCoverageGapTests.cs
- **目标类**: `Logger`（异步队列消费路径的异常隔离覆盖）
- **主要测试场景**:
  - `Logger_SinkEmitThrows_LoggerContinuesAndDoesNotCrash`（第1节）：某个 Sink 的 `Emit()` 抛出异常时，后续日志仍能被其他 Sink 正常接收，`ProcessQueueAsync` 内循环不中断。
  - `Logger_DisposeAsync_SinkDisposeThrows_DoesNotPropagate`（第2节）：`DisposeAsync` 期间某 Sink 的 `Dispose()` 抛出异常，不向上传播。
  - `Logger_DisposeAsync_WithNormalSink_DoesNotThrow`（第3节）：正常 Sink 场景下 `DisposeAsync` 不抛任何异常。

### LoggerConcurrencyTests.cs
- **目标类**: `Logger`（多线程安全性）
- **主要测试场景**:
  - `Log_ConcurrentCalls_NoException`：1000 个 `Task.Run` 并发调用 `Info()` 全程无异常抛出。
  - `Log_ConcurrentCalls_AllMessagesReceived`：500 个任务并发后，通过 Channel 读取验证所有消息 ID 均被接收，无丢失。

### LoggerCoreTests.cs
- **目标类**: `Logger`、`LoggerBuilder`、`FileTarget`
- **主要测试场景**:
  - **基础派发（第1节）**：`Logger.Log()` 调用后消息在 Channel 中可读取，且内容正确。
  - **Dispose 后静默丢弃（第2节）**：`DisposeAsync()` 后调用 `Log()` 不抛异常，消息被静默丢弃，不出现在 Channel 中。
  - **DisposeAsync 无异常（第3节）**：正常流程的 `DisposeAsync` 不抛出。
  - **FileTarget 重复路径保护（第4节）**：对同一路径创建两个 `FileTarget` 时，第二次构造抛 `InvalidOperationException` 且消息含路径字符串。
  - **多 Sink 广播（第5节）**：同一条日志同时到达两个独立的 Channel Sink。
  - **LoggerBuilder 流式链（第6节）**：`LoggerName()` 和 `AddSink()` 均返回同一个 Builder 实例（`BeSameAs`）。
  - **多次 Build（第7节）**：`Build()` 可多次调用，每次返回独立的 Logger 实例，各自能正确派发日志。
  - **FileTarget 路径复用（第8节）**：Dispose 后同路径可成功重新创建 `FileTarget`，不抛异常。
  - **LoggerBuilder null 防御（第9节，1个测试）**：`AddSink((ISinkConfig)null)` 立即抛 `ArgumentNullException`，参数名为 `sinkConfig`。
  - **ProcessQueueAsync 内层 catch（第10节，1个测试）**：第一个 sink 的 `Emit()` 抛出异常，logger 不崩溃，第二个 sink 仍正常收到消息。
  - **DisposeAsync sink Dispose 异常（第11节，1个测试）**：`ILogTarget.Dispose()` 抛出时，`DisposeAsync` 静默吞掉异常并正常完成。

### LoggerManagerTests.cs（新增）
- **目标类**: `LoggerManager`（公开静态 API）、`Logger.UpdateSinkConfig`、`Logger.UpdateLoggerConfig`
- **主要测试场景**:
  - **GetLoggerList（第1节，1个测试）**：`Build()` 后 logger 名称出现在 `GetLoggerList()` 中。
  - **UpdateSinkConfig（第2-4节，3个测试）**：logger 存在时更新过滤器（Debug 热路径 → Error 专用，验证 Debug 被拦截、Error 通过）；logger 不存在时不抛异常；sink 名称不存在时不抛异常。
  - **UpdateLoggerConfig（第5-7节，3个测试）**：logger 存在时通过 `LoggerConfig` 更新 sink 过滤器；logger 不存在时不抛异常；sink 名称不在配置中时 sink 被禁用并释放（channel writer 被 `TryComplete()`）。
  - **UpdateAllLoggerConfig（第8节，1个测试）**：批量更新两个 logger，各自过滤器均生效。
  - **UpdateLoggerConfig FileSinks 分支（第9节，1个测试）**：`LoggerConfig.FileSinks` 中的 sink 名称匹配时进入 FileSinks 分支（而非 ConsoleSinks），过滤器正确更新。

---

## Extensions/（Microsoft.Extensions.Logging 桥接适配）

### MicrosoftLoggingBridgeTests.cs
- **目标类**: `LunariumLoggerProvider`、`LunariumMsLoggerAdapter`、`LunariumLoggerConversionExtensions`、`LunariumLoggerExtensions`
- **主要测试场景**:
  - **IsEnabled 始终为 true（第1节）**：对 MEL 的所有 `LogLevel` 枚举值（含 `None`）调用 `IsEnabled` 均返回 `true`，过滤委托给 Sink 层。
  - **日志级别映射（第2节，6个 Theory）**：`Trace/Debug` → `Debug`；`Information` → `Info`；`Warning` → `Warning`；`Error` → `Error`；`Critical` → `Critical`。
  - **消息内容透传（第3节）**：消息字符串正确转发至底层 `ILogger.Log()`。
  - **异常透传（第4节）**：通过 MEL 传入的 `Exception` 对象原样转发。
  - **CreateLogger 返回适配器（第5节）**：返回值非 null 且实现 MEL 的 `ILogger` 接口。
  - **EventId 处理（第6节，3个测试）**：有名称的 `EventId` 追加到 `scope`；仅有 ID（非零）时 ID 追加到 `scope`；`EventId(0)` 无名称时不追加。
  - **BeginScope 返回 Disposable（第7节）**：返回非 null 的 `IDisposable`，`Dispose()` 不抛异常。
  - **ToMicrosoftLogger 扩展（第8节，2个测试）**：返回非 null 的 MEL `ILogger`；`IsEnabled` 和 `BeginScope` 正常工作。
  - **Provider.Dispose 不抛出（第9节）**。
  - **SetScopeProvider 不抛出（第10节）**。
  - **IsEnabled(None) 返回 true（第11节）**：`MsLogLevel.None` 级别同样返回 `true`（同第1节，独立用例）。
  - **AddLunariumLogger 扩展（第12节，1个测试）**：调用 `AddLunariumLogger(lunariumLogger)` 后返回同一 `ILoggingBuilder` 实例（支持链式调用），且 `builder.Services` 被访问（内部调用 `ClearProviders` 和 `AddProvider`）。
  - **ConvertLogLevel 默认分支（第13节，1个测试）**：传入未定义的 `(MsLogLevel)999` 时，`ConvertLogLevel` 的 `default` 分支被触发，映射为 `LmLogLevel.Info`。
  - **ForEachScope 回调（第14节，1个测试）**：使用真实 `LoggerExternalScopeProvider` 推送 scope 后调用 `Log()`，验证 `scope` 参数包含推送的 scope 字符串（覆盖 `LunariumMsLoggerAdapter.Log` 中 `ForEachScope` 回调体）。

### UseLunariumLogExtensionTests.cs
- **目标类**: `LunariumLoggerExtensions.UseLunariumLog(ILoggingBuilder)`、`LunariumHostBuilderExtensions.UseLunariumLog(IHostBuilder)`
- **主要测试场景**:
  - **ILoggingBuilder.UseLunariumLog（第13节，3个测试）**：
    - `UseLunariumLog_ILoggingBuilder_NullConfigureSinks_ThrowsArgumentNullException`：`configureSinks` 为 null 时抛 `ArgumentNullException`，参数名为 `configureSinks`。
    - `UseLunariumLog_ILoggingBuilder_ReturnsSameBuilder_AndInvokesConfigureSinks`：返回同一 `ILoggingBuilder` 实例（支持链式调用）；`configureSinks` 委托被实际调用。
    - `UseLunariumLog_ILoggingBuilder_RegistersLoggerAsSingleton`：`ILogger` 以 `ServiceLifetime.Singleton` 注册至 `IServiceCollection`，`ImplementationInstance` 为 `ILogger` 实例。
  - **IHostBuilder.UseLunariumLog（第14节，2个测试）**：
    - `UseLunariumLog_IHostBuilder_NullConfigureSinks_ThrowsArgumentNullException`：`configureSinks` 为 null 时抛 `ArgumentNullException`，参数名为 `configureSinks`。
    - `UseLunariumLog_IHostBuilder_CallsConfigureServices_ReturnsSameBuilder`：`IHostBuilder.ConfigureServices` 被调用一次（代理委托注册），返回同一 builder 实例。
  - **configureGlobal 参数路径（第15节，2个测试）**：
    - `UseLunariumLog_WithConfigureGlobal_AppliesGlobalConfig`：重置 `GlobalConfigLock` 后传入非 null `configureGlobal`（调用 `UseLocalTimeZone()`），验证方法正常返回且 `Configured=true`。
    - `UseLunariumLog_WithConfigureGlobalThatFails_LogsErrorButReturns`：`Configured=true` 时传入非 null `configureGlobal`，内部 `GlobalConfigurator.Configure()` 抛异常被捕获；验证方法不向外抛且返回同一 builder。
  - **ILoggingBuilder.UseLunariumLog(IConfiguration, loggerName)（第16节，4个测试）**：
    - `UseLunariumLog_ILoggingBuilder_NullConfiguration_ThrowsArgumentNullException`：`configuration` 为 null 时抛 `ArgumentNullException`，参数名为 `configuration`。
    - `UseLunariumLog_ILoggingBuilder_EmptyLoggerName_ThrowsArgumentException`：loggerName 为空字符串时抛 `ArgumentException`。
    - `UseLunariumLog_ILoggingBuilder_LoggerNotFound_ThrowsInvalidOperationException`：logger 名称不存在时抛 `InvalidOperationException`，消息含 logger 名称。
    - `UseLunariumLog_ILoggingBuilder_ValidConfig_RegistersSingletonAndReturnsBuilder`：找到 logger 时，以 Singleton 注册至 DI，返回同一 builder。
  - **IHostBuilder.UseLunariumLog(IConfiguration, loggerName)（第17节，3个测试）**：
    - `UseLunariumLog_IHostBuilder_NullConfiguration_ThrowsArgumentNullException`：`configuration` 为 null 时抛 `ArgumentNullException`，参数名为 `configuration`。
    - `UseLunariumLog_IHostBuilder_EmptyLoggerName_ThrowsArgumentException`：loggerName 为空字符串时抛 `ArgumentException`。
    - `UseLunariumLog_IHostBuilder_ValidConfig_CallsConfigureServicesAndReturnsSameBuilder`：`IHostBuilder.ConfigureServices` 被调用一次，返回同一 builder 实例。

---

## Filter/（上下文黑白名单过滤）

### LoggerFilterTests.cs
- **目标类**: `LoggerFilter`
- **主要测试场景**:
  - **级别过滤（第1节，4个测试）**：低于 `MinLevel` 返回 `false`；高于 `MaxLevel` 返回 `false`；边界值（恰好等于 Min 或 Max）返回 `true`。
  - **无过滤规则（第2节，3个 Theory）**：无 Include/Exclude 时任意上下文（含空字符串）均通过。
  - **Include 规则（第3节，5个测试）**：前缀匹配返回 `true`；精确匹配返回 `true`；不匹配返回 `false`；空上下文返回 `false`；多个 Include 时任一匹配即通过。
  - **Exclude 规则（第4节，2个测试）**：前缀匹配时返回 `false`；不匹配时返回 `true`。
  - **Include + Exclude 组合（第5节，3个测试）**：仅匹配 Include 时通过；同时匹配两者时被 Exclude 阻断；不匹配 Include 时直接拒绝。
  - **大小写敏感性（第6节，3个测试）**：`IgnoreFilterCase=true` 时不区分大小写；`false` 时区分大小写（匹配/不匹配各一）。
  - **缓存一致性（第7节，1个测试）**：同一上下文调用两次结果相同（第二次命中 LRU 缓存）。
  - **缓存驱逐（第8节，1个测试）**：填入超过 2048 条目触发 `Clear()` 后，新的查找仍能返回正确结果。
  - **GetFilterConfig 与 UpdateConfig（第9节，2个测试）**：
    - `LoggerFilter_GetFilterConfig_ReturnsConstructorConfig`：`GetFilterConfig()` 返回构造时传入的同一实例，字段值正确。
    - `LoggerFilter_UpdateConfig_ChangesFilterBehavior`：`UpdateConfig()` 替换 `_config`；调用后 `GetFilterConfig()` 返回新配置；对未曾缓存的 Context 的下一次查找反映新规则。

---

## Integration/（端到端集成与 ChannelTarget 变体）

### LoggerIntegrationTests.cs
- **说明**: 文件包含两个测试类。`LoggerIntegrationTests` 使用 `[Collection("Integration")]` 共享 `LoggerFixture`（`ICollectionFixture`），所有测试顺序执行。`ChannelTargetDirectTests` 为独立测试类，直接测试各 ChannelTarget 变体。`LoggerFixture.ConcreteLogger` 的声明类型为 `ILogger`（接口），运行时类型为 `Logger`（通过 `LoggerBuilder.Build()` 创建）。
- **目标类**: `Logger`、`LoggerBuilder`、`LoggerFilter`（端到端）；`LogEntryChannelTarget`、`DelegateChannelTarget<T>`、`StringChannelTarget`（直接测试）

#### LoggerIntegrationTests（9 个测试）:
  - `Log_Info_AppearsInGeneralChannel`：Info 日志出现在通用 Channel，且包含消息内容和 ID。
  - `Log_Debug_NotInErrorOnlyChannel`：Debug 日志不进入 Error-only Channel（等待 300ms 后确认）。
  - `Log_Error_AppearsInErrorOnlyChannel`：Error 日志正确进入 Error-only Channel。
  - `Log_WithServiceContext_AppearsInServiceChannel`：`ForContext("Service.Auth")` 后的日志进入 Service-only Channel。
  - `Log_WithOtherContext_NotInServiceChannel`：`ForContext("Database.Query")` 后的日志不进入 Service-only Channel。
  - `ForContext_Chained_ContextPathInOutput`：`ForContext("Service").ForContext("Payment")` 输出包含 `[Service.Payment]`。
  - `Log_WithException_ExceptionInOutput`：`Error(ex, message)` 输出包含异常类型名 `InvalidOperationException`。
  - `Log_MultipleProperties_AllRenderedCorrectly`：多个模板属性均被正确渲染。
  - `Log_Warning_AppearsWithWrnAbbreviation`：Warning 日志输出包含 `[WRN]`。

#### ChannelTargetDirectTests（9 个测试）:
  - **LogEntryChannelTarget（3个）**：
    - `LogEntryChannelTarget_Emit_TransparentlyPassesLogEntry`：`Emit()` 后 Channel 中可读取 `LogEntry`，level 和 message 正确。
    - `LogEntryChannelTarget_Emit_PreservesAllFields`：LoggerName、Context、Timestamp 所有字段完整保留。
    - `LogEntryChannelTarget_Dispose_CompletesChannel`：`Dispose()` 后 Channel 的 `Completion` 任务完成。
  - **DelegateChannelTarget\<T\>（3个）**：
    - `DelegateChannelTarget_Emit_AppliesTransformDelegate`：Lambda 将 `LogLevel` 转换为 `int` 正确执行。
    - `DelegateChannelTarget_Emit_StringTransform_ProducesExpectedOutput`：字符串转换（ToUpperInvariant）正确执行。
    - `DelegateChannelTarget_Dispose_CompletesChannel`：同上，`Dispose()` 完成 Channel。
  - **StringChannelTarget（3个）**：
    - `StringChannelTarget_WithIsColorTrue_ProducesAnsiOutput`：`isColor:true` 时输出含 ANSI 转义码 `\x1b[`。
    - `StringChannelTarget_WithToJsonTrue_ProducesJsonOutput`：`ToJson:true` 时输出含 `"Level"` 字段。
    - `StringChannelTarget_Dispose_CompletesChannel`：`Dispose()` 后 Channel 完成。

---

## Internal/（底层缓冲区实现）

### BufferWriterTests.cs
- **目标类**: `BufferWriter`（位于 `Lunarium.Logging.Internal` 命名空间）
- **主要测试场景**:
  - **基础写入与索引（7个测试）**：`Remove(2,2)` 移除中间字节；`Remove` 末尾等价于 `RemoveLast`；`RemoveLast(2)` 正确收缩长度；追加非 ASCII 字符（`€`/`中`）UTF-8 正确编码；`GetSpan` 超容量触发扩容；`this[index]` 按索引返回字节；`WrittenSpan` 返回正确切片。
  - **Advance（第2节，3个测试）**：负数 count 抛 `ArgumentOutOfRangeException`；超出可用字节抛 `InvalidOperationException`；有效 count 正确更新 `Length` 和内容。
  - **GetMemory（第3节，2个测试）**：带 sizeHint 时触发扩容，返回 Memory 长度满足需求；缓冲区满时自动扩容，返回非空 Memory。
  - **Append(object?)（第4节，2个测试）**：null 为 no-op，`Length` 保持 0；非 null 调用 `ToString()` 写入内容。
  - **Append(ReadOnlySpan\<byte\>)（第5节，2个测试）**：空 span 为 no-op；非空字节 span 正确写入。
  - **AppendLine（第6节，1个测试）**：追加 `Environment.NewLine` 字节。
  - **AppendFormat(string, object?)（第7节，1个测试）**：`{0:D4}` + 7 输出 `"0007"`。
  - **AppendFormat(string, string?)（第8节，7个测试）**：`{0}` 直接路径写入值；null 值写入空；`{0,6}` 右对齐补空格；`{0,-6}` 左对齐补空格；内容超宽无补空格；含格式说明符 `{0:X}` 降级至 `string.Format`；短格式 `{0}` 走快速路径。
  - **AppendFormattable\<T\>（第9节，3个测试）**：int 零分配格式化；double 带 `"F2"` 格式；`DateTimeOffset` 以 `"O"` 格式输出含年份。
  - **Rewind（第10节，4个测试）**：有效 index 截断内容；负数抛 `ArgumentOutOfRangeException`；超出已写入量抛 `ArgumentOutOfRangeException`；Rewind(0) 清空内容。
  - **FlushTo(Stream)（第11节，2个测试）**：已写字节全部写入 `MemoryStream`；空缓冲区时 Stream 长度为 0。
  - **Reset（第12节，2个测试）**：清空 `Length` 和内容；保留原有 `Capacity` 不变。
  - **Dispose（第13节，1个测试）**：调用不抛任何异常。
  - **EnsureCapacity 边界（第14节，3个测试）**：初始容量 0 时首次写入触发扩容；`Append((string?)null)` 为 no-op；`AppendSpan(ReadOnlySpan<char>.Empty)` 为 no-op。

### DestructureHelperTests.cs（新增）
- **目标类**: `DestructureHelper`（位于 `Lunarium.Logging` 命名空间）
- **说明**: `DestructureHelper` 是高性能自定义解构的辅助类，包装 `Utf8JsonWriter`，供实现 `IDestructurable` 接口的对象使用。测试通过 `internal` 构造函数直接实例化，使用 `bufferWriterIsMainWriter=false, jsonWriterIsMainWriter=false` 将 `Utf8JsonWriter` 绑定至 `BufferWriter`。
- **主要测试场景（~40个测试）**:
  - **对象/数组结构（4个测试）**：`WriteStartObject`/`WriteEndObject`、`WriteStartArray`/`WriteEndArray` 成对调用后 `TryFlush()` 为 true。
  - **WritePropertyName（3个测试）**：`string`、`ReadOnlySpan<char>`、`JsonEncodedText` 三种重载均正常写入。
  - **WriteStringValue（6个测试）**：`string`、`ReadOnlySpan<char>`、`JsonEncodedText`、`DateTime`、`DateTimeOffset`、`Guid` 六种重载。
  - **WriteNumberValue（7个测试）**：`int`、`long`、`uint`、`ulong`、`float`、`double`、`decimal` 七种重载。
  - **WriteBooleanValue（1个测试）**：写入 `true` 后输出含 `true`。
  - **WriteNullValue（1个测试）**：写入后输出含 `null`。
  - **组合写入方法（WriteString/WriteNumber/WriteBoolean/WriteNull）（4个测试）**：各一键写键值对方法正确输出对应 JSON 字段。
  - **WriteRawValue（1个测试）**：写入合法 JSON 片段后直接出现在输出中。
  - **TryFlush（3个测试）**：正常完整 JSON 后返回 true 且 `WrittenSpan` 非空；深度未归零（未调用 `WriteEndObject`）时返回 false；调用两次时第二次返回 false（`Flush` 无内容）。
  - **WrittenSpan（1个测试）**：`TryFlush` 后 `WrittenSpan` 长度 > 0。
  - **Dispose（2个测试）**：`resetBufferWriter=true` 后 BufferWriter 被清空；`resetJsonWriter=true` 后 Utf8JsonWriter 被重置（再次 `TryFlush` 返回 false）。
  - **复杂嵌套 JSON 集成测试（1个测试）**：对象内含数组，数组内含嵌套对象，整体可被 `JsonDocument.Parse()` 解析。

---

## Models/（配置结构与模型）

### LogConfigTests.cs
- **目标类**: `Sink`、`ConsoleSinkConfig`、`FileSinkConfig`、`LoggerFilter`（间接，通过 `Sink.Emit` 路径）
- **主要测试场景**:
  - **Sink 构造与解构（第1节，6个测试）**：带/不带 `FilterConfig` 的构造；2-out 和 3-out `Deconstruct` 重载均正确拆解 `Target`、`Configuration`、`LoggerFilter`；显式 `name` 参数被存储；空 `name` 自动生成 GUID 格式字符串。
  - **ConsoleSinkConfig（第2节，3个测试）**：默认 `FilterConfig` 为 null；设置后正确；实现 `ISinkConfig` 接口。
  - **FileSinkConfig（第3节，3个测试）**：验证 `MaxFileSizeMB`、`RotateOnNewDay`、`MaxFile`、`FilterConfig` 的默认值及完整赋值；实现 `ISinkConfig` 接口。
  - **ISinkConfig.CreateTarget() — ToJson 路径（第4节，3个测试）**：`ConsoleSinkConfig { ToJson=true/false/null }` 通过 `CreateTarget()` 构造 `ConsoleTarget`，验证 `ToJson` 值（null 默认为 false）。
  - **ISinkConfig.CreateTarget() — IsColor 路径 + FilterConfig 存储（第5节，6个测试）**：`ConsoleSinkConfig { IsColor=false/true/null }` 通过 `CreateTarget()` 传入 ConsoleTarget；Target 不实现 `ITextTarget` 时 Sink 构造不抛异常；`FilterConfig.TextOutputIncludeConfig` 非 null 时存储在 `Sink.Configuration` 中；`TextOutputIncludeConfig=null` 时 Target 属性不变。
  - **FileSinkConfig.CreateTarget() — ToJson/IsColor 路径（第5b节，2个测试）**：`FileSinkConfig { ToJson=true }` 通过 `CreateTarget()` 构造 `FileTarget`，验证 `ToJson=true`；`IsColor=true` 同理。
  - **Sink.Emit 过滤（第6节，2个测试）**：过滤通过时调用 `Target.Emit()` 一次；过滤拦截时 `Target.Emit()` 不被调用（使用 NSubstitute 验证）。
  - **第二构造函数与 UpdateSinkConfig（第7节，2个测试）**：
    - `Sink_Ctor_WithNameOnly_UsesDefaultFilter`：`new Sink(target, "my-named-sink")` 使用第二构造函数；`Name` 正确存储，`LoggerFilter` 非 null，默认 `LogMinLevel` 为 `Info`。
    - `Sink_UpdateSinkConfig_ChangesFilterResult`：构造时使用 `LogMinLevel=Warning` 配置，Debug 级别被拦截；`UpdateSinkConfig()` 嵌套新 `FilterConfig` 后 Debug 级别通过（NSubstitute 验证调用次数）。
  - **默认构造（第8节，2个测试）**：
    - `FileSinkConfig_DefaultConstruction_LogFilePathIsEmpty`：`new FileSinkConfig()` 时 `LogFilePath` 为空字符串（移除 `required` 关键字后的回归验证）。
    - `LoggerConfig_DefaultConstruction_LoggerNameIsEmpty`：`new LoggerConfig()` 时 `LoggerName` 为空字符串，`ConsoleSinks`/`FileSinks` 为空字典。
  - **Sink Enabled 开关（第9节，3个测试）**：`DisableSink()` 时 `Emit()` 不调用 `Target.Emit()`；`DisableSink()` 后 `Enabled=false`；`EnableSink()` 后 `Enabled=true`。
  - **Sink.UpdateSinkConfig 边界（第10-13节，4个测试）**：null `FilterConfig` 重置为默认过滤器（`Info` 最低级别）；`Enabled=false` 禁用 Sink；ITextTarget + null `TextOutput` 重置为 `new TextOutputIncludeConfig()`（IncludeTimestamp=true）；ITextTarget + 非 null `TextOutput` 更新目标配置（IncludeTimestamp/Level=false 生效）。
  - **Sink.Dispose（第14节，1个测试）**：`Dispose()` 调用 `Target.Dispose()` 一次（NSubstitute 验证）。
  - **第二构造函数 null name（第15节，1个测试）**：`new Sink(target, (string?)null)` 时 `Name` 为 GUID 格式字符串。

---

## Parser/（消息模板解析器）

### LogParserTests.cs
- **目标类**: `LogParser`、`MessageTemplate`、`TextToken`、`PropertyToken`
- **说明**: `Tokens()` 辅助方法访问 `MessageTemplate.MessageTemplateTokens`（`internal readonly IReadOnlyList<MessageTemplateTokens>`）。
- **主要测试场景**:
  - **null/空字符串（第1节，2个测试）**：返回 `LogParser.EmptyMessageTemplate` 单例（`BeSameAs`）。
  - **纯文本（第2节，4个测试）**：无占位符时生成单个 `TextToken`；末尾悬挂 `{`、末尾 `}`、中间孤立 `}` 均视作纯文本。
  - **转义序列（第3节，3个测试）**：`{{` `}}` 转义为单个花括号；`{{name}}` 不解析为属性而是文本 `{name}`。
  - **合法属性名（第4节，7个 Theory）**：字母/下划线/数字/点号组合均生成 `PropertyToken`，`Destructuring` 为 `Default`。
  - **非法属性名回退（第5节，8个 Theory）**：空属性、前导数字、末尾点、双点、格式后跟冒号等均回退为 `TextToken`，文本内容与原始模板一致。
  - **解构前缀（第6节，4个测试）**：`@` → `Destructure`；`$` → `Stringify`；`{@}` / `{$}` 无名称时回退为 `TextToken`。
  - **对齐（第7节，5个测试）**：正/负整数对齐值正确；非数字、含空格对齐回退为 `TextToken`。
  - **格式说明符（第8节，4个测试）**：`Format` 字段正确捕获；对齐与格式同时存在；含前导/尾随空格的格式回退为 `TextToken`。
  - **混合模板（第9节，4个测试）**：文本+属性+文本的 Token 顺序；多属性全部捕获；相邻属性无文本分隔；未闭合 `{` 与前置文本合并为单个 `TextToken`。
  - **缓存复用（第10节，1个测试）**：同一模板字符串两次调用返回相同对象（`BeSameAs`）。
  - **RawText 字段（第11节，2个测试）**：`PropertyToken.RawText.Text` 包含含花括号的完整原始片段，含解构前缀 `@`。
  - **单字符属性回归（第12节，4个测试）**：`{V:F2}`、`{V,10}`、`{V,10:F2}`、`{V}` 四种组合均正确解析，不产生旧 bug 中的混合值。

---

## Target/（数据转发目标）

### ChannelTargetTests.cs
- **目标类**: `ByteChannelTarget`、`StringChannelTarget`
- **主要测试场景**:
  - `ByteChannelTarget_ShouldWriteEncodedBytes`：`Emit()` 后 Channel 中的 `byte[]` 解码为 UTF-8 后包含日志消息。
  - `ByteChannelTarget_WithJson_ShouldWriteJsonBytes`：`ToJson=true` 时字节内容含 `"OriginalMessage":"..."` 字段。
  - `StringChannelTarget_ToJson_CanBeSet`：`ToJson` 属性在对象初始化时设为 `true` 后读取正确值。
  - `StringChannelTarget_ImplementsITextTarget`：`StringChannelTarget` 实现 `ITextTarget` 接口。
  - `ByteChannelTarget_ImplementsITextTarget`：`ByteChannelTarget` 实现 `ITextTarget` 接口。
  - `StringChannelTarget_TextOutputIncludeConfig_CanBeSet`：`TextOutputIncludeConfig` 在对象初始化时设置后读取正确值。
  - `StringChannelTarget_UpdateTextOutputIncludeConfig_UpdatesValue`：`UpdateTextOutputIncludeConfig()` 后 `GetTextOutputIncludeConfig()` 返回新值。
  - `ByteChannelTarget_UpdateTextOutputIncludeConfig_UpdatesValue`：同上，对 `ByteChannelTarget`。
  - `ByteChannelTarget_GetTextOutputIncludeConfig_ReturnsDefaultValues`：未传 config 时，所有四个包含字段均为 `true`。
  - `ByteChannelTarget_WithIsColor_ProducesAnsiOutput`：`isColor=true` 时 Channel 中的字节包含 ANSI 转义码 `\x1b[`。

### ConsoleTargetTests.cs（新建）
- **目标类**: `ConsoleTarget`
- **说明**: 使用 `internal ConsoleTarget(Stream stdout, Stream stderr, ...)` 构造函数注入 `MemoryStream`，覆盖 Emit 路由和内部构造函数。
- **主要测试场景**:
  - `ConsoleTarget_UpdateTextOutputIncludeConfig_UpdatesValue`：调用后 `GetTextOutputIncludeConfig().IncludeLoggerName` 为 `false`。
  - `ConsoleTarget_Emit_InfoLevel_WritesToStdout`：Info 日志写入 stdout，stderr 为空。
  - `ConsoleTarget_Emit_ErrorLevel_WritesToStderr`：Error 日志写入 stderr，stdout 为空。
  - `ConsoleTarget_Emit_WithToJsonTrue_ProducesJsonOutput`：`toJson=true` 时 stdout 包含 `"Level"` 字段。
  - `ConsoleTarget_Emit_WithIsColorFalse_ProducesPlainText`：`isColor=false` 时输出含 `[INF]`，不含 ANSI 转义码。

---

## Wrapper/（ForContext 装饰器）

### LoggerWrapperTests.cs
- **目标类**: `LoggerWrapper`
- **说明**: `GetContext()`/`GetContextSpan()` 已从 `ILogger` 移至 `IContextProvider`（internal interface），由 `LoggerWrapper` 实现。测试中对 `ILogger` mock 不再设置 `GetContext()` 返回值；`GetContext()`/`GetContextSpan()` 通过 `LoggerWrapper` 具体类型变量调用。
- **主要测试场景**:
  - **空/null 上下文（第1节，2个测试）**：传入空字符串或 `null` 的 context 参数时，实际转发的 context 为 Wrapper 自身的上下文名。
  - **上下文拼接（第2节，1个测试）**：传入非空 context 时，与 Wrapper 自身名称以 `.` 拼接后转发（`Outer.Inner`）。
  - **嵌套 Wrapper（第3节，2个测试）**：两层和三层嵌套时 `GetContext()` 返回正确的点分路径（`A.B`、`A.B.C`）。
  - **异常透传（第4节，1个测试）**：传入的 `Exception` 对象原样转发至底层 `Log()`。
  - **属性透传（第5节，1个测试）**：`propertyValues` 数组（含 `"Alice"`）原样转发，长度和内容均正确。
  - **GetContextSpan（第6节，2个测试）**：`GetContextSpan()` 解码后与 `GetContext()` 字符串一致；三层嵌套时 Span 解码为完整点分路径（`A.B`）。
  - **DisposeAsync 不传播（第7节，1个测试）**：调用 `DisposeAsync()` 后底层 `ILogger.DisposeAsync()` 不被调用（Wrapper 屏蔽销毁传播）。

---

## Writer/（输出渲染引擎）

### LogColorTextWriterTests.cs
- **目标类**: `LogColorTextWriter`、`WriterPool`
- **主要测试场景**:
  - **ANSI 码输出（第1节，2个测试）**：输出含 `\x1b[` 前缀和 `\x1b[0m` 重置码；去除 ANSI 后包含消息文本。
  - **级别缩写（第2节，5个 Theory）**：去除 ANSI 后 DBG/INF/WRN/ERR/CRT 均出现。
  - **级别颜色码（第3节，5个测试）**：Debug=`\x1b[90m`；Info=`\x1b[92m`；Warning=`\x1b[93m`；Error=`\x1b[91m`；Critical=`\x1b[97;41m`。
  - **上下文 / LoggerName（第4节，5个测试）**：LoggerName 在去 ANSI 后可见；有上下文时可见且使用 Cyan(`\x1b[96m`)；有 LoggerName 但无 Context 时 Cyan 仍出现（LoggerName 同色）；LoggerName 和 Context 均为空时不出现 Cyan 码。
  - **属性替换（第5节，5个测试）**：字符串/整数属性被替换；无属性时保留 `{Name}` 原文；null 渲染为 `null`；属性数量少于占位符时剩余占位符保留原文。
  - **值颜色分派（第6节，5个测试）**：String→Magenta(`95`)；Int→Yellow(`93`)；Bool→Blue(`94`)；null→DarkBlue(`34`)；object→Gray(`37`)。
  - **解构 @（第7节，1个测试）**：去除 ANSI 后输出含 JSON 字段名 `"X"`。
  - **多属性（第8节，1个测试）**：`{A}` 和 `{B}` 均被替换。
  - **异常（第9节，3个测试）**：去除 ANSI 后包含异常类型名；使用 Red(`91`)；无异常时不含额外换行。
  - **池化复用（第10节，1个测试）**：Return 后 Get 得到的 Writer 渲染新条目不残留旧内容。
  - **时间戳（第11节，2个测试）**：去除 ANSI 后含年份数字；时间戳使用 Green(`92`)。
  - **StringColor 分支（第13节，6个测试）**：char/DateTime/DateTimeOffset/Guid/TimeSpan/Uri 均使用 Magenta(`95`)。
  - **NumberColor 分支（第14节，3个测试）**：long/double/decimal 均使用 Yellow(`93`)。
  - **超长格式串（第15节，1个测试）**：格式部分超 96 字符触发堆分配路径，不抛异常且有输出。
  - **未知 LogLevel（第16节，2个测试）**：去除 ANSI 后含 `UNK`；颜色为 WhiteOnYellow(`\x1b[97;103m`)。

### LogJsonWriterTests.cs
- **目标类**: `LogJsonWriter`、`WriterPool`
- **主要测试场景**:
  - **JSON 有效性（第1节，2个测试）**：输出可被 `JsonDocument.Parse()` 解析；含 `Timestamp` 字段。
  - **Level 字段（第2节，5个 Theory）**：字符串名（Debug/Information/Warning/Error/Critical）和整型值（0~4）均正确。
  - **Context 字段（第3节，2个测试）**：有上下文时 `Context` 字段存在；空上下文时 `Context` 字段不存在。
  - **OriginalMessage 字段（第4节，1个测试）**：含原始模板占位符 `{Name}`，不替换。
  - **RenderedMessage 字段（第5节，4个测试）**：属性被替换；无属性时保留占位符；多属性全替换；null 渲染为字面量 `null`。
  - **Propertys 字段（第6节，11个测试）**：String/Int/Bool/Long/null/NaN/Infinity/负无穷/Guid/无属性空对象/decimal 各类型的序列化行为。
  - **解构 @ （第7节，1个测试）**：`Propertys.Obj` 为嵌套 JSON 对象，含 `Name` 和 `Age` 字段。
  - **Exception 字段（第8节，2个测试）**：有异常时 `Exception` 字段存在且含类型名和消息；无异常时字段不存在。
  - **特殊字符转义（第9节，5个测试）**：`"`、`\`、`\n` 正确转义；中文字符直接输出（不转义）；Emoji 处理为代理对。
  - **格式说明符（第10节，2个测试）**：字符串属性在 RenderedMessage 中正确替换；decimal 在 Propertys 中作为数字值。
  - **多属性（第11节，1个测试）**：Propertys 对象含所有键。
  - **LoggerName 字段（第12节，1个测试）**：`LoggerName` JSON 字段值与构造时传入的 `loggerName` 一致。

### LogTextWriterTests.cs
- **目标类**: `LogTextWriter`、`WriterPool`
- **主要测试场景**:
  - **级别缩写（第1节，5个 Theory）**：`[DBG]`/`[INF]`/`[WRN]`/`[ERR]`/`[CRT]` 均出现在输出中。
  - **属性替换（第2节，4个测试）**：单/多属性替换；占位符多于属性时多余的保留原文；null 渲染为 `null`。
  - **对齐（第3节，2个测试）**：右对齐（`{Val,6}` → `    42`）；左对齐（`{Val,-6}` → `42    `）。
  - **格式说明符（第4节，1个测试）**：`{PI:F2}` 渲染为 `3.14`。
  - **解构 @（第5节，1个测试）**：输出含 JSON 内容（`Alice`、`30`）。
  - **上下文（第6节，2个测试）**：有上下文时输出含 `[MyService]`；空上下文时输出不含 `[]`。
  - **异常（第7节，2个测试）**：有异常时输出含类型名和消息；无异常时不含 `Exception` 字样。
  - **纯文本消息（第8节，1个测试）**：无占位符时消息原文出现。
  - **转义花括号（第9节，1个测试）**：`{{literal}}` 渲染为 `{literal}`。
  - **LoggerName 渲染（第10节，2个测试）**：非空 LoggerName 输出 `[LoggerName]`；空 LoggerName bytes 时不输出多余括号。
  - **Render(config) 重载（第11节，4个测试）**：`IncludeLoggerName=false` 时不输出 LoggerName；`IncludeTimestamp=false` 时不含年份；`IncludeLevel=false` 时不含 `[INF]`；`IncludeContext=false` 时不含指定 Context。

### LogWriterTests.cs
- **目标类**: `LogTextWriter`、`LogColorTextWriter`、`LogJsonWriter`、`LogWriter`（基类）、`StringChannelTarget`
- **主要测试场景**:
  - **LogTextWriter 时间戳模式（第1节，4个测试）**：Unix/UnixMs/ISO8601/Custom 四种模式的输出格式用正则验证。
  - **LogTextWriter 所有级别（第2节，5个 Theory）**：所有级别缩写正确出现。
  - **LogTextWriter 上下文（第3节，2个测试）**：有/无上下文的输出格式。
  - **LogTextWriter 属性渲染路径（第4节，9个测试）**：null/缺失/多余/格式/对齐/对齐+格式/超长格式/解构/集合自动解构 各路径。
  - **LogTextWriter 异常（第4节末）**：含 `Exception` 时输出包含异常信息。
  - **LogColorTextWriter 各级别 ANSI 码（第5节，5个 Theory + 7个测试）**：各级别含 `\x1b[`；上下文可见；null/格式/超长格式/值类型颜色/Guid/DateTime/解构/异常各路径。
  - **LogJsonWriter 结构（第6节，14个测试）**：JSON 有效性；所有字段（含 Context、Exception）存在/不存在的条件；JSON 时间戳四种模式；ToJsonValue 各类型（int/bool/string/NaN/Infinity/Guid/long/解构/缺省属性 null/特殊字符/所有 Level 字符串）。
  - **LogWriter 基类池逻辑（第7节，3个测试）**：小缓冲区 `TryReset` 返回 `true`；大缓冲区（>100字节限制）返回 `false`；`Dispose` 两次不抛异常。
  - **StringChannelTarget 分发选择（第8节，3个测试）**：`ToJson=true` 输出 JSON 格式；`isColor=true` 输出 ANSI 格式；默认输出纯文本 `[INF]`。

### WriterCoverageGapTests.cs
- **目标类**: `LogJsonWriter`、`LogColorTextWriter`、`LogTextWriter`、`LogWriter`（基类）
- **主要测试场景**:
  - **A节 — LogJsonWriter JSON 字符串内容转义（8个测试）**：
    - `\t` → `\\t`；`\r` → `\\r`；`\b` → `\\b`；`\f` → `\\f`。
    - 控制字符 `\x01` → `\\u0001`（十六进制转义）。
    - Emoji `😀`（U+1F600）通过 `Utf8JsonWriter` 输出为代理对转义 `\\uD83D\\uDE00`。
    - 中文字符（`你好`）在 `UnsafeRelaxedJsonEscaping` 模式下直接输出，不转义。
  - **B节 — LogJsonWriter ToJsonValue 类型覆盖（17个测试）**：
    - 整数族：short/byte/uint/ulong/ushort/sbyte/decimal 各渲染为 JSON 数字。
    - float 特殊值：NaN → `"NaN"`；+∞ → `"Infinity"`；−∞ → `"-Infinity"`；正常 float 渲染为数字。
    - char → 带引号字符串；DateTime → ISO 8601 字符串含年份；TimeSpan → `"02:00:00"` 形式；Uri → URL 字符串；未知类型 → `ToString()` 字符串回退。
  - **C节 — LogJsonWriter 无属性值时的模板输出（1个测试）**：模板含属性 Token 但 properties 数组为空，`RenderedMessage` 含原始 `{N}` 文本。
  - **D节 — LogColorTextWriter 无属性值时的模板输出（1个测试）**：同上，`{N}` 保留在输出中。
  - **E节 — LogColorTextWriter SetValueColor 剩余类型（6个测试）**：char/DateTime/DateTimeOffset/TimeSpan/Uri 均有颜色输出；匿名类型走默认颜色分支，输出含 `\x1b[`。
  - **F节 — LogWriter.GetBufferCapacity（1个测试）**：返回值 ≥ 0。
  - **G节 — IsCommonCollectionType 边界（3个测试）**：AutoDestructure 启用时，null 值输出 `null`；string 类型不被当作集合解构（不含 JSON 对象 `{"`）；int[] 数组正确解构输出元素。
  - **H节 — LogJsonWriter Token 多于属性时的 null 填充（1个测试）**：模板含两个属性 Token 但只提供一个值，第二个 Token 在 `Propertys` 中输出为 `null`。
  - **I节 — LogWriter.WriteAligned（4个测试，经 LogTextWriter 渲染触发）**：`{N,5}` + 42 → `"   42"`（右对齐）；`{N,-5}` + 42 → `"42   "`（左对齐）；`{N,2}` + 12345 → `"12345"`（内容超宽，无补空格）；`{N,40}` + 1 触发 39 个空格（> `SpacePool.Length=32`，走 `GetSpan/Advance` 路径）。
  - **J节 — LogJsonWriter.WriteAligned（3个测试，经 RenderedMessage 渲染触发）**：与 I节 相同的右对齐、左对齐、超宽不补空格场景，验证 `_scratchWriter` 路径正确执行。
  - **K节 — IDestructured 路径（4个测试）**：`LogTextWriter`/`LogColorTextWriter` 渲染 `{@V}` + `IDestructured` 实现时，`Destructured()` 返回的字节直接出现在输出；`LogJsonWriter` 渲染时字节通过 `WriteRawValue` 写入 `Propertys`，输出含预期 JSON 字段。
  - **L节 — IDestructurable no-crash（2个测试）**：`LogTextWriter` 和 `LogColorTextWriter` 渲染 `{@V}` + `IDestructurable` 实现时不抛异常（已知 `_bufferWriter` 被 Reset、jsonWriter 仍绑定 `Stream.Null` 的行为缺陷，无输出但无崩溃）。
  - **M节 — LogJsonWriter IDestructurable（2个测试）**：`WritePropertyValue` 中 `IDestructurable` 分支被执行后因 `WriteRawValue` 接收空字节抛 `JsonException`（已知路径缺陷，测试验证代码分支可达）；`IDestructured` 分支在 `Propertys` 中正确输出预期 JSON 字段。
  - **N节 — LogJsonWriter 正常 double 值（1个测试）**：`WriteJsonValue` 中 `case double d:` 的正常值路径（`json.WriteNumberValue(d)`）；`3.5` 渲染为 JSON 数字。
  - **O节 — LogColorTextWriter WriteAligned（1个测试）**：`{N,5}` + int 42 触发 `RenderPropertyToken` 中 `IUtf8SpanFormattable` + `Alignment.HasValue=true` 分支（`WriteAligned`）；原始输出含 `"   42"`（3个空格 + 42）。
  - **P节 — LogJsonWriter IDestructurable TryFlush=false 路径（1个测试）**：`Destructure` 只写 `StartObject` 不写 `EndObject`，使 `TryFlush()` 返回 false；`WriteRawValue` 被跳过，`destructureHelper.Dispose(true, true)` 仍被执行（lines 322-323）；`_jsonWriter` 随后因属性名无值抛 `InvalidOperationException`。

### WriterPoolTests.cs
- **目标类**: `WriterPool`
- **主要测试场景**:
  - `GetReturnGet_SameObject_IsReused`：Return 后再 Get，得到的是同一个对象实例（`BeSameAs`）。
  - `Return_WriterIsReset_ToStringIsEmpty`：Return 并 Get 后，`ToString()` 为空字符串（缓冲区已清除）。
  - `Return_OversizedWriter_NotReused`：将 `MaxBufferCapacity` 设为 1，排空池后，Return 的 Writer 因 `TryReset(1)` 失败不入池，下次 Get 得到新实例（`NotBeSameAs`）。
  - `Get_WhenPoolEmpty_ReturnsNewInstance`：取出超过 `PoolMaxSize`（100）个对象耗尽池后，再 Get 仍能返回非 null 的新实例。
  - `WriterPool_Return_WhenPoolFull_DoesNotThrow`：Get 128 个对象后全部 Return（填满池至上限），再 Get+Return 第 129 个，验证溢出路径（`DisposeAndReturnArrayBuffer`）不抛异常。

---

## 根目录（接口与扩展方法）

### ILoggerDefaultMethodTests.cs
- **目标类**: `ILogger`（默认接口方法，DIM）
- **说明**: 使用内部 `CapturingLogger` 具体实现（非 NSubstitute mock）捕获 `Log()` 调用，确保 DIM 能通过 `ILogger` 接口类型引用正确触发。`GetContext()`/`GetContextSpan()` 已从 `ILogger` 移至 `IContextProvider`，`CapturingLogger` 不再实现这两个方法。
- **主要测试场景（17个测试）**:
  - `Debug_ForwardsToLog`：级别为 `Debug`，消息正确，无异常。
  - `Info_ForwardsToLog`：级别为 `Info`，消息正确。
  - `Warning_ForwardsToLog`：级别为 `Warning`，消息正确。
  - `Error_MessageOnly_ForwardsToLog`：级别 `Error`，消息正确，`Ex` 为 null。
  - `Error_ExceptionOnly_ForwardsToLog`：级别 `Error`，消息为空字符串，`Ex` 为传入的异常对象。
  - `Error_MessageAndException_ForwardsToLog`：级别 `Error`，消息和异常均正确转发（`Error(Exception, string, params)`重载）。
  - `Error_ExceptionThenMessage_ForwardsToLog`：`Error(Exception, string)`重载，消息和异常均正确。
  - `Critical_MessageOnly_ForwardsToLog`：级别 `Critical`，消息正确，`Ex` 为 null。
  - `Critical_ExceptionOnly_ForwardsToLog`：级别 `Critical`，消息为空，`Ex` 为传入异常。
  - `Critical_MessageAndException_ForwardsToLog`：`Critical(Exception, string, params)`重载，消息和异常均正确。
  - `Critical_ExceptionThenMessage_ForwardsToLog`：`Critical(Exception, string)`重载，消息和异常均正确。
  - `Debug_ExceptionOnly_ForwardsToLog`：`Debug(Exception)` 重载，级别 `Debug`，消息为空，`Ex` 为传入异常。
  - `Debug_ExceptionAndMessage_ForwardsToLog`：`Debug(Exception, string, params)` 重载，消息和异常均正确，`Props[0]` 为 42。
  - `Info_ExceptionOnly_ForwardsToLog`：`Info(Exception)` 重载，级别 `Info`，消息为空，`Ex` 为传入异常。
  - `Info_ExceptionAndMessage_ForwardsToLog`：`Info(Exception, string, params)` 重载，消息和异常均正确。
  - `Warning_ExceptionOnly_ForwardsToLog`：`Warning(Exception)` 重载，级别 `Warning`，消息为空，`Ex` 为传入异常。
  - `Warning_ExceptionAndMessage_ForwardsToLog`：`Warning(Exception, string, params)` 重载，消息和异常均正确，`Props[0]` 为 `"x-val"`。

### LoggerExtensionsTests.cs
- **目标类**: `LoggerExtensions`（`ForContext`/`ForContext<T>`）、`LoggerBuilderExtensions`（`AddConsoleSink`、`AddFileSink`、`AddStringChannelSink`、`AddLogEntryChannelSink`、`AddChannelSink<T>`、`AddSink(ISinkConfig)`、`AddTimedRotatingFileSink`、`AddSizedRotatingFileSink`、`AddRotatingFileSink`）
- **主要测试场景**:
  - **ForContext(string)（第1节，3个测试）**：返回 `LoggerWrapper` 类型；null logger 抛 `ArgumentNullException`；调用 `Info()` 后 context 正确转发给底层 mock。
  - **ForContext\<T\>（第2节，1个测试）**：上下文为 `typeof(T).FullName`，正确转发。
  - **AddFileSink（第3节，4个测试）**：无 config 重载返回同一 Builder（含 DisposeAsync）；带 `FilterConfig` 返回同一 Builder；Build 后写一条日志验证文件存在且内容正确；`filterConfig: null` 不抛异常。
  - **AddConsoleSink（第3b节，2个测试）**：无参和带 `FilterConfig` 重载均返回同一 Builder 实例。
  - **AddStringChannelSink（第4节，4个测试）**：基本重载返回 Builder 和非 null Reader；带 capacity 和 isColor 参数的重载；`toJson=true` 分支；带 `FilterConfig` 分支。
  - **AddSink(ISinkConfig)（第5节，4个测试）**：`ConsoleSinkConfig` 分发；`FileSinkConfig` 分发（临时路径，Build 后 DisposeAsync）；自定义 `ISinkConfig` 通过 `CreateTarget()` 注册（NSubstitute 验证调用次数）；`AddSinkByConfig` 扩展方法返回同一 Builder。
  - **AddTimedRotatingFileSink / AddSizedRotatingFileSink（第6节，2个测试）**：各返回同一 Builder，Build 后可正常 DisposeAsync。
  - **AddLogEntryChannelSink（第7节，3个测试）**：基本/带 capacity/带 FilterConfig 重载各返回 Builder 和非 null Reader。
  - **AddChannelSink\<T\>（第8节，2个测试）**：带 transform lambda 返回 Builder 和 Reader；带 capacity 参数正常。
  - **AddRotatingFileSink（第10节，1个测试）**：同时设置 `maxFileSizeMB` 和 `rotateOnNewDay` 两种策略，返回同一 Builder，Build 后可正常 DisposeAsync。
  - **AddUtf8ByteChannelSink（第11节，1个测试）**：`AddUtf8ByteChannelSink_ReturnsSameBuilderAndValidReader`：返回同一 Builder，`out ChannelReader<byte[]>` 非 null，Build 后 DisposeAsync 正常。

### LogUtilsTests.cs（路径：根目录 `LogUtilsTests.cs`）
- **目标类**: `LogUtils`、`TimestampFormatConfig`
- **说明**: 此文件与 `Core/LogUtilsTests.cs` 为两个独立文件，覆盖不同场景。本文件测试公共 API 在各 `TextTimestampMode` 下的格式化输出；`Core/` 下的文件测试 UTC 偏移和枚举回退防线。
- **主要测试场景（5个测试）**:
  - `GetLogSystemTimestamp_ReturnsDateTimeOffset`：返回值在测量前后的 Unix 秒范围内。
  - `GetLogSystemFormattedTimestamp_UnixMode_ReturnsLongString`：结果为纯数字字符串，可解析为正 long。
  - `GetLogSystemFormattedTimestamp_UnixMsMode_ReturnsLongerNumber`：结果为纯数字且 > 10¹²。
  - `GetLogSystemFormattedTimestamp_ISO8601Mode_ReturnsIsoString`：结果可被 `DateTimeOffset.TryParse()` 解析。
  - `GetLogSystemFormattedTimestamp_CustomMode_UsesCustomFormat`：格式 `yyyy/MM/dd` 产生匹配 `^\d{4}/\d{2}/\d{2}$` 的字符串。

---

## Configuration/（appsettings.json 集成）

### LunariumConfigurationExtensionsTests.cs
- **目标类**: `LunariumConfigurationExtensions`（`BuildLunariumLoggers`/`BuildLunariumLogger`）、`GlobalConfigApplier`、`LoggerConfigApplier`
- **说明**: 归属 `[Collection("GlobalConfigurator")]`（非并行），因为 `GlobalConfigLock` 是静态单例。使用 `ConfigurationBuilder + AddJsonStream` 构造内存配置。
- **主要测试场景（16个测试）**:
  - **BuildLunariumLoggers（第1节，3个测试）**：
    - `BuildLunariumLoggers_EmptyConfig_ReturnsEmptyList`：无 `LoggerConfigs` 时返回空列表，无异常。
    - `BuildLunariumLoggers_MultipleLoggerConfigs_ReturnsAllLoggers`：配置两个 logger 时返回 2 个实例。
    - `BuildLunariumLoggers_LoggerNamesMatchConfig`：`LoggerManager.GetLoggerList()` 包含配置中的所有 logger 名称。
  - **BuildLunariumLogger（第2节，2个测试）**：
    - `BuildLunariumLogger_NotFound_ReturnsNull`：logger 名称不存在时返回 null。
    - `BuildLunariumLogger_Found_ReturnsLogger`：找到时返回非 null 的 `ILogger` 实例。
  - **GlobalConfig 应用/跳过（第3节，2个测试）**：
    - `BuildLunariumLogger_WithGlobalConfig_AppliesWhenNotYetConfigured`：存在 `GlobalConfig.TimeZone=Utc` 时，`GlobalConfigLock.Configured` 变为 `true`。
    - `BuildLunariumLogger_WithGlobalConfig_SkipsWhenAlreadyConfigured`：预置 `Configured=true` 后调用不抛异常（一次性设计的 try-catch 路径）。
  - **GlobalConfigApplier 各字段（第4节，4个测试）**：
    - `GlobalConfigApplier_UtcTimeZone_Applies`：`TimeZone=Utc` → `Configured=true`，无异常。
    - `GlobalConfigApplier_LocalTimeZone_Applies`：`TimeZone=Local` → `Configured=true`，无异常。
    - `GlobalConfigApplier_WriteIndentedJson_Applies`：`WriteIndentedJson=true` → `Configured=true`，无异常。
    - `GlobalConfigApplier_EnableAutoDestructuring_Applies`：`EnableAutoDestructuring=true` → `Configured=true`，无异常。
  - **LoggerConfigApplier（第5节，2个测试）**：
    - `LoggerConfigApplier_EmptyFileSinkPath_SkipsSink`：`FileSinks` 中 `LogFilePath` 为空字符串时跳过该 Sink，logger 仍构建成功。
    - `BuildLunariumLogger_LoggerNamePropagatedToLoggerManager`：构建后 `LoggerManager.GetLoggerList()` 包含该 logger 名称（验证 `LoggerBuilder.Build()` 时自动调用 `RegisterLogger`）。