# Lunarium.Logging.IntegrationTests 代码索引

此索引专为 AI 和开发者提供，记录了各个集成测试文件所覆盖的 **目标类 (Target Class)** 和详细的 **测试场景 (Test Cases)**。
AI 在修改与物理 I/O 环境强相关的底层逻辑时，可通过搜索此文档快速定位受影响的测试用例进行验证。

## ConsoleSink/(控制台输出集成测试)
### ConsoleSinkTests.cs
- **目标类**: `ConsoleTarget`
- **主要测试场景**:
  - 输出流注入：不再拦截全局 `Console.Out`，改为通过 `internal` 构造函数向 `ConsoleTarget` 注入自定义 `MemoryStream` 以验证输出逻辑。
  - 输出流分发：验证 `Info` 级进入标准输出，`Error` 与 `Critical` 级进入标准错误的路由规则。
  - 格式输出保障：`Emit_WithJson_OutputsJsonFormat` 检测开启 JSON 开关时能否获得预期的键值结构内容；以及关闭颜色选项 (`IsColor = false`) 时是否能洁净剥离 ANSI 转义符直接打印裸文 (`Emit_WithIsColorFalse_OutputsPlainTextWithoutAnsi`)。
  - 流级与格式多重组合：`Emit_WithToJsonAndErrorLevel_OutputsJsonToErrorStream` 检测错误流时开启 JSON 并不干扰 JSON 的输出到正确的目标端点。
  - 格式独立验证：`Emit_ToJson_TrueAndFalseBehaveDifferently` 检查分别以 `toJson=true` 和 `toJson=false` 构造的两个 Target，各自产生 JSON 与纯文本输出，互不干扰（`ToJson` 现为 `init`-only，不支持运行时切换）。
  - 安全资源收尾：测试该类可以接连被 `Dispose` 调用多次而不导致进程或线程异常抛出故障。

## FileSink/(文件输出及轮转策略集成测试)
### FileSinkTests.cs
- **目标类**: `FileTarget`
- **主要测试场景**:
  - 基础读写及强制落盘：验证纯文本或 JSON (`Emit_WithToJsonTrue/False_...`) 能成功被写入并读取回系统；测试 `Error` 等级及以上是否会触发无视缓冲的及时立即触发落盘持久化。
  - 文件切分/轮转规则：测试设置到达指定的 `maxFileSizeMB` 大小限制后，下一次触发时旧文件被结转新文件顺利建立 (`Emit_WhenMaxFileSizeReached_RotatesFile`)；以及针对跨日检测时间触发的每日切分是否生效 (`CheckForRotation_RotatesOnNewDay`)。
  - 日志积余数量清理：验证基于日期的定期清理或基于大小产生的多个切分后的冗余积淀，皆能按照 `maxFile` 数量阀值对久远留存进行正确抹除 (`CleanupOldFiles_DeletesOldestFiles_WhenLimitReached`, `CleanupOldFiles_TimedRotation_WithMaxFile_DeletesOldestFiles`)。
  - 初始化及配置防御：当使用者试图给出无效或矛盾策略（如启用了最大保留，却没开启日期和大小任何一种切割选项）能够明确触发并抛错异常 (`Constructor_InvalidConfig_Throws`)。
  - 运行期恢复及容灾重连：极其核心的抗毁恢复测试 —— `EnsureFileExists_WhenFileDeletedExternally_RecreatesFileAndContinuesWriting`，它验证当运行时期的物理日志文件意外遭到外部系统的强制撕裂/粉碎/删除之后，应用服务仍能探知、断开损坏符柄并于下次立刻重建出新生文件继续承受重负录入，从而保障不会被底层 IO 失效拉垮致崩。
  - 重启文件重续复用：`FindLatestLogFileOrCreateNew_ReusesExistingUnderSizeFile` 与 `FindLatestLogFileOrCreateNew_CombinedMode_ReusesUnderSizeTodayFile` 检测当项目历经关停冷重启之后，不仅不会覆盖上一轮文件，还会在判定当日末尾日志未满指定分卷限额的前提下，重新拼接续流避免无端浪费切分碎片文件的特性。
