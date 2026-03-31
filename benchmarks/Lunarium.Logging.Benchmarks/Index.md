# Lunarium.Logging.Benchmarks 代码索引

此索引专为 AI 和开发者提供，记录了各个 Benchmark 文件所覆盖的 **目标类 (Target Class)** 和详细的 **测试场景 (Benchmark Cases)**。
修改核心库热路径时，可通过搜索此文档快速定位受影响的 Benchmark，评估性能回归风险。

**运行方式**：
```bash
# 交互式菜单选择
dotnet run -c Release --project benchmarks/Lunarium.Logging.Benchmarks

# 按类名过滤运行
dotnet run -c Release --project benchmarks/Lunarium.Logging.Benchmarks -- --filter "*LogParser*"
```

> ⚠️ 必须以 Release 模式运行，Debug 模式结果无意义。

---

## 公共基础设施

### BenchmarkHelper.cs
- **目标类**: `NullTarget`（`ILogTarget` 空实现）、`GlobalConfigurator`
- **用途**:
  - `NullTarget.Emit()` 为空方法，用于在所有吞吐量测试中隔离 I/O 噪声，只测量 Channel 写入与管道分发开销。
  - `BenchmarkHelper.EnsureGlobalConfig()` 内部调用 `GlobalConfigurator.ApplyDefaultIfNotConfigured()`，确保全局时区、时间戳格式等静态配置在进程首次 Benchmark 前完成初始化（进程级单次执行，所有 Benchmark 类共享）。

---

## LogParserBenchmarks.cs

- **目标类**: `LogParser`（`Lunarium.Logging.Parser`，internal）
- **测量目标**: `LogParser.ParseMessage(string)` 的吞吐量
- **架构背景**: 解析器内置 4096 条 `ConcurrentDictionary` 缓存；缓存满时全量清空（非 LRU 淘汰）。
- **主要 Benchmark 场景**:
  - `PlainText_CacheHit`：纯文本模板（无占位符），走 `IndexOfAny` 快速路径后命中缓存，代表最快情形的基准线。
  - `SingleProperty_CacheHit`：单属性 `"User {Name} logged in"`，最常见模板形态的缓存命中开销。
  - `ThreeProperty_CacheHit`：三属性模板，验证 Token 数量增加后缓存命中的影响（理论上命中后开销与属性数量无关）。
  - `ComplexTemplate_CacheHit`：含对齐 `{Count,8:D}`、格式化 `{Percent:P1}`、解构前缀 `{@Worker}` 的复合模板缓存命中，验证复杂性不影响缓存命中路径。
  - `EscapedBraces_CacheHit`：含 `{{ }}` 转义的模板缓存命中，验证转义分支不增加命中后的开销。
  - `CacheMiss_Approx`：使用 6000 个预生成唯一字符串池循环调用（超出缓存上限 4096 后触发清空重建），近似模拟持续缓存未命中场景，测量状态机解析 + 字典写入的完整开销。

---

## LogWriterBenchmarks.cs

- **目标类**: `LogTextWriter`、`LogColorTextWriter`、`LogJsonWriter`（均为 `Lunarium.Logging.Writer`，internal）、`WriterPool`（internal static）
- **测量目标**: `LogWriter.Render(LogEntry)` 渲染管线吞吐量 + `WriterPool` 对象池收益
- **架构背景**: 三种 Writer 均通过 `WriterPool.Get<T>()` 取出、`writer.Return()` 归还；内部使用 `BufferWriter`（自定义 `IBufferWriter<byte>` 实现）构建 UTF-8 输出，写入 `Stream.Null` 排除 I/O 干扰。`GlobalSetup` 中预构建含解析完毕模板的 `LogEntry`，排除解析器开销干扰。
- **主要 Benchmark 场景**:

  **LogTextWriter（纯文本格式）**
  - `Text_PlainText`（基准线）：无属性消息渲染，代表最小开销路径。
  - `Text_SingleProperty`：单属性渲染，测量 `AppendFormat` 与属性值查找的基础开销。
  - `Text_MultiProperty`：四属性渲染，观察属性数量线性增长对渲染时间的影响。
  - `Text_AlignmentAndFormat`：含对齐 `{Count,8:D}` 与格式化 `{Percent:P1}`，验证 `IUtf8SpanFormattable` 快路径 + 手动对齐实现的开销。
  - `Text_Numeric`：数值/时间类型（`int`、`DateTimeOffset`、`TimeSpan`）的 `IUtf8SpanFormattable` 零分配路径验证。

  **LogColorTextWriter（带 ANSI 颜色的文本格式）**
  - `Color_SingleProperty`：单属性渲染，相比 `Text_SingleProperty` 测量 ANSI 转义码插入的额外开销。
  - `Color_MultiProperty`：四属性渲染，多属性场景下颜色代码拼接的综合开销。
  - `Color_AlignmentAndFormat`：验证带颜色输出时，手动对齐逻辑与转义码插入的组合性能。
  - `Color_Numeric`：验证数值类型在带颜色路径下的 `IUtf8SpanFormattable` 零分配渲染。

  **LogJsonWriter（JSON 格式）**
  - `Json_SingleProperty`：单属性 JSON 渲染，基于 `Utf8JsonWriter` 测量核心序列化开销。
  - `Json_MultiProperty`：四属性 JSON 渲染，验证 `Utf8JsonWriter` 的多属性拼接效率。
  - `Json_Numeric`：数值/时间类型的 `Utf8JsonWriter` 直接写入路径（`WriteNumberValue` / `WriteStringValue(Span<byte>)`）验证。
  - `Json_ComplexObject`：复杂对象解构（`{@Payload}`）测试，验证 `IDestructurable` 接口与 `Utf8JsonWriter.WriteRawValue()` 集成路径。

  **WriterPool 对象池收益**
  - `Pool_GetAndReturn`：`WriterPool.Get<LogTextWriter>()` + `WriterPool.Return()` 的往返开销（池化路径），测量 `ConcurrentBag.TryTake` 与 `Add` 的代价。
  - `Alloc_NewWriter`：`new LogTextWriter()` 直接分配（不归还），通过 `[MemoryDiagnoser]` 的 `Allocated` 列量化池化节省的堆分配。

  > **注**：`Json_ComplexObject` 和 `Json_Destructure_IDestructured` 使用相同的 `{@Payload}` 模板，属性值分别为匿名类型（走 `JsonSerializer.Serialize` 回退路径）和 `IDestructured` 实现（静态单例，预序列化字节，走 `WriteRawValue` 零分配路径），两者对比可直接量化自定义解构接口的性能收益。

---

## ChannelTargetBenchmarks.cs

- **目标类**: `LogEntryChannelTarget`、`ByteChannelTarget`、`StringChannelTarget`（均为 `Lunarium.Logging.Target`）
- **测量目标**: `ChannelTarget<T>.Emit(LogEntry)` 的完整开销（包含渲染 + 编码 + Channel TryWrite）
- **架构背景**: 三个变体的核心差异在 `Transform()` 实现：`LogEntryChannelTarget` 直接返回传入引用（零编码）；`ByteChannelTarget` 渲染后调用 `GetWrittenBytes()` / `ToArray()`（一次 byte[] 拷贝）；`StringChannelTarget` 渲染后调用 `ToString()` / `Encoding.UTF8.GetString`（UTF-8→string 解码）。所有变体使用无界 Channel，`TryWrite` 始终成功，排除背压干扰。`GlobalCleanup` 中 `Dispose` 各 Target 完成 Channel，不等待排空。
- **主要 Benchmark 场景**:
  - `Emit_LogEntry`（基准线）：`LogEntryChannelTarget` 传递 `LogEntry` 引用，无渲染无编码，测量最小基线开销。
  - `Emit_ByteArray`：`ByteChannelTarget` 渲染 + `ToArray()` byte[] 拷贝，通过 `[MemoryDiagnoser]` 的 `Allocated` 列量化 byte[] 分配成本，相比 `Emit_String` 跳过 UTF-16 字符串解码。
  - `Emit_String`：`StringChannelTarget` 渲染 + `Encoding.UTF8.GetString` UTF-8→string 解码，产生额外 string 堆分配，与 `Emit_ByteArray` 对比可量化 UTF-16 解码的开销。

---

## FilterBenchmarks.cs

- **目标类**: `LoggerFilter`（`Lunarium.Logging.Filter`，internal）
- **测量目标**: `LoggerFilter.ShouldEmit(LogEntry, FilterConfig)` 的吞吐量
- **架构背景**: 每个 `LoggerFilter` 实例持有独立的 2048 条 `ConcurrentDictionary` 上下文缓存（实例级，非静态）；缓存满时全量清空。每个 Benchmark 方法使用独立的 `LoggerFilter` 实例，缓存状态互不干扰。`GlobalSetup` 中预热缓存，确保缓存命中测试直接走字典读路径。
- **主要 Benchmark 场景**:

  **缓存命中路径**
  - `NoRules_Pass_CacheHit`（基准线）：无 Include/Exclude 规则，仅做级别检查后命中缓存，代表最快过滤路径。
  - `WithIncludes_Pass_CacheHit`：配置 5 条 Include 前缀规则，context 匹配通过的缓存命中开销。
  - `WithIncludes_Reject_CacheHit`：context 不匹配 Include 列表被拒绝，缓存命中路径（命中结果为 false）。
  - `WithExcludes_Pass_CacheHit`：配置 3 条 Exclude 前缀规则，context 不在排除列表通过的缓存命中。
  - `WithExcludes_Reject_CacheHit`：context 匹配 Exclude 列表被拒绝，缓存命中路径。

  **缓存未命中（近似）**
  - `NoRules_CacheMiss_Approx`：使用 3000 个预生成唯一 context 字符串池（超出缓存上限 2048 后触发清空），近似模拟缓存未命中场景，测量前缀匹配计算 + 字典写入开销。`LogEntry` 已在 `static` 构造器中预生成，排除构造开销干扰。

---

## LoggerThroughputBenchmarks.cs

- **目标类**: `Logger`（internal，通过 `ILogger` 接口操作）、`LoggerBuilder`、`LoggerWrapper`（`ForContext` 返回值）
- **测量目标**: 调用方线程的 `Log()` 调用开销（Channel TryWrite 吞吐量）
- **架构背景**: `Logger.Log()` 是同步方法，内部构造 `LogEntry` 后调用 `Channel<LogEntry>.Writer.TryWrite()` 即返回；实际的过滤/解析/渲染/输出在后台 `ProcessQueueAsync` 中异步执行。本 Benchmark 测量的是调用方线程感知到的开销，与后台处理速度无关。所有测试使用 `NullTarget`，`GlobalCleanup` 中通过 `IAsyncDisposable.DisposeAsync()` 等待队列排空后释放资源。
- **主要 Benchmark 场景**:
  - `Log_PlainMessage`（基准线）：无属性纯文本消息，`params object?[]` 传入空数组，测量 `LogEntry` 构造 + `TryWrite` 的最小开销。
  - `Log_OneProperty`：单属性调用，`params object?[1]` 隐式数组分配，通过 `[MemoryDiagnoser]` 观察该分配量。
  - `Log_ThreeProperties`：三属性调用，`params object?[3]` 数组分配，与单属性对比验证参数数量对 `Allocated` 的影响。
  - `Log_FiveProperties`：五属性调用，参数数组分配量最大的常见场景。
  - `Log_ViaForContext`：通过 `ForContext` 返回的 `LoggerWrapper` 调用，测量 `LoggerWrapper` 相比直接调用 `Logger` 的额外间接层开销（预期极小）。
  - `Log_Batch100`：循环调用 100 次，分摊开销后反映批量写入的平均每条成本，更接近高吞吐生产场景。
