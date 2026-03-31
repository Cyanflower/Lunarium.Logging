# Lunarium.Logging.Http.Tests 代码索引

此索引专为 AI 和开发者提供，记录了各测试文件所覆盖的 **目标类 (Target Class)** 和详细的 **测试场景 (Test Cases)**。
AI 在修改 `Lunarium.Logging.Http` 包代码时，可通过搜索此文档快速定位受影响的测试用例，无需全局扫描测试目录。

测试框架：**xUnit 2.9.3 + FluentAssertions 6.12.2**
同步策略：异步行为均使用 `TaskCompletionSource<T>` + `WaitAsync(TimeSpan)` 等待；HTTP 层由 `FakeHttpHandler`（`Helpers/FakeHttpHandler.cs`）拦截。

---

## Helpers/（测试辅助类）

### FakeHttpHandler.cs
- **类型**: 测试辅助 `HttpMessageHandler`，不直接测试任何生产代码
- **功能**:
  - `Requests`：捕获所有传入的 `HttpRequestMessage`
  - 构造函数接受 `Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>` 委托，可灵活控制响应
  - 静态工厂方法：
    - `HangUntilCancelled()`：永远阻塞直到取消，用于测试 Dispose 超时路径
    - `FailOnceThenSucceed()`：首次返回 500，第二次返回 200，用于重试测试
    - `AlwaysThrow()`：每次调用抛 `HttpRequestException`

### EntryFactory.cs
- **类型**: 测试辅助工厂，不直接测试任何生产代码
- **功能**:
  - `Make(message, level, loggerName, context, props, ex, scope, timestamp)`：创建并解析单个 `LogEntry`
  - `MakeBatch(count, message)`：批量创建 `List<LogEntry>`

---

## Serializer/（序列化器测试）

### JsonArraySerializerTests.cs
- **目标类**: `JsonArraySerializer`
- **主要测试场景**:
  - **基础结构（2个）**：空批次返回 `[]`；多条目时数组长度与输入一致。
  - **必填字段（2个）**：`timestamp`/`level`/`logger`/`message` 均存在；`logger` 字段值等于 LoggerName。
  - **可选字段（6个）**：context 为空时字段省略；非空时写入；无异常时 exception 省略；有异常时 exception 含消息；scope 为空时省略；非空时写入。
  - **级别映射（Theory，5个）**：`Debug`→`"Debug"`，`Info`→`"Information"`，`Warning`→`"Warning"`，`Error`→`"Error"`，`Critical`→`"Critical"`（Info 对齐 MEL 约定输出 Information）。
  - **消息渲染（2个）**：无 token 的纯文本原样输出；含 `{Name}` token 时渲染为 `"Hello World"`。
  - **属性类型（9个）**：null→JSON null；bool→JSON boolean；int/long/uint/ulong（Theory）→JSON number；有限 double→JSON number；无穷大 double→JSON string；无穷大 float→JSON string；Guid→字符串可解析回原值；DateTimeOffset→含日期的 ISO 8601 字符串。
  - **Stringify `{$Value}`（1个）**：int 42 写为字符串 `"42"` 而非数字。
  - **Destructure `{@Object}`（3个）**：`IDestructured` 返回字节直接嵌入；`IDestructurable` 调用 `Destructure(DestructureHelper)` 写入；普通对象走 `JsonSerializer` 回退。
  - **Content-Type（1个）**：`application/json; charset=utf-8`。

### ClefSerializerTests.cs
- **目标类**: `ClefSerializer`
- **主要测试场景**:
  - **NDJSON 格式（2个）**：空批次输出空内容；多条目按 `\n` 分隔，每行为独立合法 JSON。
  - **`@t` 时间戳（1个）**：写入 `@t` 字段，值为含日期的 ISO 8601 字符串。
  - **`@l` 级别（5个）**：`Info` 时 `@l` 字段省略（CLEF 约定）；`Debug`/`Warning`/`Error` 写入对应字符串（Theory，3个）；`Critical` 映射为 `"Fatal"`。
  - **`@mt` 消息模板（1个）**：写入原始模板字符串而非渲染结果（`"Hello {Name}"` 而非 `"Hello World"`）。
  - **`@x` 异常（2个）**：有异常时写入 `@x` 含消息；无异常时省略 `@x`。
  - **附加结构化字段（4个）**：`LoggerName` 写入 `LoggerName` 字段；context 非空时写入 `Context` 字段；context 为空时省略；scope 非空时写入 `Scope` 字段。
  - **Properties 平铺到顶级（3个）**：属性无嵌套 `properties` 对象，直接出现在顶级；`{$Count}` Stringify 写为字符串；`{@Data}` IDestructured 嵌入为对象。
  - **Content-Type（1个）**：`application/vnd.serilog.clef`。

### LokiSerializerTests.cs
- **目标类**: `LokiSerializer`
- **主要测试场景**:
  - **根结构（1个）**：输出包含 `streams` 数组。
  - **stream labels（3个）**：所有构造时注入的 labels 出现在 `stream` 对象中；空 labels 产生空 `stream` 对象；支持 `IReadOnlyDictionary<string,string>` 构造函数。
  - **values（2个）**：`values` 数组长度等于输入条目数；每个 value 是恰好两个元素的数组（`[timestamp, logLine]`）。
  - **时间戳（3个）**：时间戳字段为字符串类型；值为正整数纳秒字符串；同毫秒内不同 tick 偏移（+500µs）的时间戳纳秒值不同（亚毫秒精度验证）。
  - **log line（2个）**：第二元素为合法 JSON；该 JSON 含 `timestamp` 字段且 `level` 字段正确。
  - **Content-Type（1个）**：`application/json`。

### DelegateHttpLogSerializerTests.cs
- **目标类**: `DelegateHttpLogSerializer`
- **主要测试场景**:
  - 委托被调用且接收到相同的 entries 引用（`BeSameAs`）。
  - 委托返回的 `HttpContent` 直接透传给调用方。
  - 构造时传入自定义 ContentType 可通过 `ContentType` 属性读取。
  - 不传 ContentType 时默认为 `"application/json"`。

---

## Target/（HttpTarget 生命周期与行为测试）

### HttpTargetTests.cs
- **目标类**: `HttpTarget`
- **主要测试场景**:
  - **批量触发（2个）**：写入条目数达到 `batchSize` 时立即触发 flush；单条目 + 短 `flushInterval` 时时间触发在 batchSize 达到前 flush。
  - **请求内容（3个）**：请求为 POST 且 URI 与 endpoint 一致；Content-Type 与序列化器一致（以 CLEF 为例）；自定义 headers 正确附加到请求。
  - **重试（2个）**：首次 500 重试第二次 200，共调用 2 次（使用 `batchSize=1` + 单条 entry 避免 CI 时序竞争）；两次均失败则丢弃条目，不会发起第三次调用。
  - **溢出（2个）**：Channel 满时 `Emit` 不抛异常且继续正常消费；flush 成功后 `_overflowWarned` 重置，后续溢出仍可正常继续（通过两轮写入+等待验证）。
  - **Dispose 生命周期（5个）**：
    - `Dispose` 等待剩余条目全部发送完才返回（批次未满时的 drain 验证）
    - 多次 `Dispose` 幂等不抛异常
    - `Dispose` 后继续 `Emit` 静默丢弃，不抛异常，不发请求
    - `Dispose` 不调用注入的 `HttpClient.Dispose()`（谁创建谁管理）
  - **Dispose 超时（1个）**：后台任务永远阻塞时，`Dispose` 在 `disposeTimeout` 内返回，不会永久挂起。
  - **错误处理（3个）**：非 2xx 状态码（包括重试后仍失败）不崩溃后台任务；`HttpClient` 抛 `HttpRequestException` 不崩溃后台任务；序列化器抛异常不崩溃后台任务，`Dispose` 正常完成。
  - **构造验证（2个）**：非 http/https scheme 抛 `ArgumentException`；header 名含 CR/LF 抛 `ArgumentException`。

---

## Config/（配置对象测试）

### HttpSinkConfigTests.cs
- **目标类**: `HttpSinkConfig`
- **主要测试场景**:
  - **默认值（6个）**：`BatchSize=100`、`FlushInterval=5s`、`DisposeTimeout=5s`、`ChannelCapacity=1000`、`RequestTimeout=30s`、`Enabled=true`。
  - **CreateTarget（2个）**：返回 `HttpTarget` 实例；非 http/https scheme 时抛 `ArgumentException`。

---

## Extensions/（扩展方法集成冒烟测试）

### HttpLoggerBuilderExtensionsTests.cs
- **目标类**: `HttpLoggerBuilderExtensions`（`AddHttpSink` / `AddSeqSink` / `AddLokiSink`）
- **测试策略**: 构建真实 Logger → 写入两条日志 → `FakeHttpHandler` TCS 等待，验证请求确实发出。
  `batchSize=2 + flushInterval=3s`：正常情况下 BatchSize 先触发；Logger 内部 Channel 引入异步延迟时，3s 计时器保底（小于 WaitTimeout=5s），避免随机超时失败。BatchSize 批量合并行为由 `HttpTargetTests.BatchSize_Reached_TriggersFlush` 专项覆盖。
- **主要测试场景**:
  - **AddHttpSink（string overload）（2个）**：emit 两条后请求发出且 body 非空；Content-Type 为 `application/json`。
  - **AddHttpSink（HttpSinkConfig overload）（1个）**：传入配置对象时请求发出。
  - **AddSeqSink（3个）**：Content-Type 为 `application/vnd.serilog.clef`；传入 apiKey 时 `X-Seq-ApiKey` header 存在且值正确；不传 apiKey 时 header 不存在。
  - **AddLokiSink（2个）**：Content-Type 为 `application/json`；传入 labels 时 Loki stream 对象含对应 label 键值。
