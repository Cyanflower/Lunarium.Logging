# 性能分析报告

> Benchmark Report 出自 Lunarium.Logging.Benchmarks (基于 BenchmarkDotNet)  
> 本篇分析出自 Claude Sonnet 4.6 (基于 Baseline-2026-03-30 Markdown 报告)  
> 测试环境：Intel Core i7-8750H @ 2.20GHz (Coffee Lake), 6C/12T, Fedora Linux 42(6.18.8-100.fc42.x86_64), .NET 10.0.2, X64 RyuJIT AVX2  
> 跨平台差异见 [PlatformDifferences.md](PlatformDifferences.md)

---

## 概览

| 路径 | 耗时 | 分配 | 说明 |
| :--- | :--- | :--- | :--- |
| Filter 过滤（缓存命中） | ~8–9 ns | 0 | 热路径，零分配 |
| Parser 模板解析（缓存命中） | 11–18 ns | 0 | 热路径，零分配 |
| LogWriter Text 渲染 | 329–526 ns | **32 B** | 含 WriterPool 周转 |
| LogWriter Color 渲染 | 438–626 ns | **32 B** | 含 ANSI 转义写入 |
| LogWriter JSON 渲染 | 507–773 ns | **64 B** | 含 RenderedMessage 字段 |
| Log() 调用方（无属性） | ~188 ns | 128 B | Channel 写入即返回，渲染异步 |
| Log() 调用方（五属性） | ~201 ns | 240 B | params 数组分配与值类型 Boxing 为主要开销 |
| WriterPool Get + Return | ~77 ns | 0 | 池化路径，零分配 |

> **关键设计**：`Log()` 调用方仅执行 Channel `TryWrite`，**耗时与渲染无关**；模板解析和输出格式化均发生在后台线程，不阻塞业务逻辑。

---

## 调用方吞吐：Logger 端到端

此处测量的是**业务线程侧**的 `Log()` 调用开销，包括 `LogEntry` 构造和 Channel `TryWrite`，不含渲染和 I/O。

### 单次调用（ns）

| 场景 | 耗时 | 分配 |
| :--- | :--- | :--- |
| 无属性，纯文本消息 | 188.3 ns | 128 B |
| 单属性（params object?[1] 分配） | 199.1 ns | 160 B |
| 三属性（params object?[3] 分配） | 190.4 ns | 200 B |
| 五属性（params object?[5] 分配） | 201.3 ns | 240 B |
| 通过 ForContext 包装器（LoggerWrapper 额外调用） | 185.4 ns | 160 B |

单次调用稳定在 **185–201 ns**，属性数量（0–5 个）对耗时影响极小（< 16 ns）。分配来源主要为 `LogEntry` 对象（固定部分）+ `params object?[]` 数组（每个属性 ~8 B boxing）。`ForContext` 包装器本身零额外分配（Context/ContextBytes 构造时预计算）。

### 批量调用分摊

| 场景 | 总耗时 | 每条分摊 |
| :--- | :--- | :--- |
| 批量 100 条 | 20,117 ns | **~201 ns/条** |

批量分摊值与单次调用一致，说明 Channel 写入无批量摊销效应——每条日志独立竞争。单线程理论吞吐上限约 **530 万次/秒**；典型多线程业务场景下远低于此值，Channel 不会成为瓶颈。

---

## 分配特性总览

| 模块 | 正常路径分配 | 说明 |
| :--- | :--- | :--- |
| Filter 过滤（缓存命中） | **0** | 完全零分配 |
| Parser 解析（缓存命中） | **0** | 完全零分配 |
| LogWriter Text/Color 渲染 | **32 B** | 固定，与属性数无关 |
| LogWriter JSON 渲染 | **64 B** | 固定，与属性数无关 |
| Log() 调用方（无属性） | **128 B** | LogEntry 固定部分 |
| Log() 调用方（每增加一个属性） | **+32 B** | params boxing |
| WriterPool Get+Return | **0** | 池化路径零分配 |
| Filter 缓存未命中 | 218 B | 极少触发 |
| Parser 缓存未命中 | 1,414 B | 极少触发 |
| JSON `{@Payload}` 回退路径 | 656 B | 建议改用 `IDestructurable`/`IDestructured` |

调用方侧的 128–240 B 分配（每次 `Log()` 调用）是当前 `params object?[]` API 的固有成本，主要来自数组分配和值类型 boxing。在 100 万次/秒的极端场景下约产生 120–240 MB/sec 的 Gen0 分配压力，.NET GC 通常可在不触发 Gen1/Gen2 的情况下处理此量级。

---

## 使用建议

### 常规日志（< 10 万次/秒）

- 所有路径均有充足余量，无需特别优化
- JSON 格式与 Text 格式在此频率下 GC 影响可忽略

### 高频日志（> 100 万次/秒）

- 优先使用无属性或少属性（≤ 3 个）调用，控制 `params` boxing 分配
- 使用 `LogEntryChannelTarget` 而非 `StringChannelTarget`，减少渲染和解码开销
- 解构复杂对象时实现 `IDestructurable` 或 `IDestructured`，避免 `JsonSerializer.Serialize` 回退路径（多出 ~592 B 分配）
- 确保 context 字符串为静态常量，避免 Filter 缓存频繁失效

### AOT 场景

- 注册 `JsonSerializerContext`（`GlobalConfigurator.UseJsonTypeInfoResolver()`），否则 `{@Object}` 解构将回退为 `ToString()`
- 实现 `IDestructurable`/`IDestructured` 的类型不受 AOT 限制

---

## 内部组件性能详述

---

## 热路径：Filter 过滤

过滤发生在后台线程的每条日志分发时，是频率最高的操作之一。

| 场景 | 耗时 | 分配 |
| :--- | :--- | :--- |
| 无规则，级别通过（缓存命中） | 8.663 ns | 0 |
| Include 规则，context 匹配通过（缓存命中） | 8.889 ns | 0 |
| Include 规则，context 不匹配被拒绝（缓存命中） | 8.921 ns | 0 |
| Exclude 规则，context 不在排除列表（缓存命中） | 8.359 ns | 0 |
| Exclude 规则，context 命中排除列表（缓存命中） | 8.734 ns | 0 |
| 缓存未命中（超出 2048 条唯一 context 后触发） | 237 ns | 218 B |

**缓存命中路径**（正常情况）恒定在 **~8–9 ns，零分配**，与规则类型和通过/拒绝结果无关，说明过滤逻辑的分支均被预测命中且常驻 L1 缓存。

**缓存未命中**（237 ns）仅在超出 2048 条唯一 context 后触发缓存清空重建，对于 context 为静态字符串（类名、模块名）的正常使用模式，此路径几乎不会发生。

> **注**: 如果常面临 2048 缓存溢出的情况，请考虑提交 Issue 反馈来协助项目更好的了解使用情景来预估缓存大小。

---

## 热路径：LogParser 模板解析

模板解析同样发生在后台线程，解析结果被缓存复用。

| 场景 | 耗时 | 分配 |
| :--- | :--- | :--- |
| 纯文本，无占位符（缓存命中） | 13.65 ns | 0 |
| 单属性模板（缓存命中） | 11.01 ns | 0 |
| 三属性模板（缓存命中） | 16.28 ns | 0 |
| 复杂模板：对齐 + 格式化 + 解构前缀（缓存命中） | 18.30 ns | 0 |
| 含转义 `{{ }}`（缓存命中） | 13.94 ns | 0 |
| 缓存未命中（超出 4096 条唯一模板后触发） | 1,281 ns | 1,414 B |

**缓存命中路径**恒定在 **11–18 ns，零分配**，模板复杂度（属性数量、对齐格式、解构前缀）对命中路径的开销影响极小。

**缓存未命中**（1,281 ns）发生在首次解析或缓存溢出后。正常服务中模板字符串是有限的静态集合，缓存命中率趋近 100%。

> **注**: 如果常面临 4096 缓存溢出的情况，请考虑提交 Issue 反馈来协助项目更好的了解使用情景来预估缓存大小。

---

## 渲染性能：LogWriter

渲染在后台线程执行，通过 `WriterPool` 对象池复用 Writer 实例，**每次渲染的固定分配仅 32 B（Text/Color）或 64 B（JSON）**。

### Text Writer（ns）

| 场景 | 耗时 | 分配 |
| :--- | :--- | :--- |
| 纯文本消息（无属性） | 328.91 ns | 32 B |
| 单属性 | 356.61 ns | 32 B |
| 四属性 | 446.02 ns | 32 B |
| 对齐 + 格式化（`{Count,8:D} {Percent:P1}`） | 526.32 ns | 32 B |
| Numeric/Formattable（int, DateTimeOffset, TimeSpan） | 465.06 ns | 32 B |

### Color Writer（ns）

| 场景 | 耗时 | 分配 |
| :--- | :--- | :--- |
| 单属性（含 ANSI 颜色转义） | 438.36 ns | 32 B |
| 四属性 | 605.66 ns | 32 B |
| 对齐 + 格式化 | 626.05 ns | 32 B |
| Numeric/Formattable | 591.34 ns | 32 B |

Color Writer 相比 Text Writer 多出约 **100–170 ns**，来自 ANSI 转义字节序列的写入开销，分配量相同。

### JSON Writer（ns）

| 场景 | 耗时 | 分配 |
| :--- | :--- | :--- |
| 单属性（含 RenderedMessage + Properties 字段） | 506.76 ns | 64 B |
| 四属性 | 759.57 ns | 64 B |
| Numeric/Formattable | 772.94 ns | 64 B |
| `{@Payload}` IDestructured（预序列化字节，零分配路径） | 634.73 ns | 64 B |
| `{@Payload}` 匿名类型（JsonSerializer.Serialize 回退路径） | 979.06 ns | **656 B** |

JSON Writer 固定分配 **64 B**，无论属性数量。`IDestructured` 接口（预序列化字节）是解构对象的推荐路径，比 `JsonSerializer.Serialize` 回退路径快 **35%** 且分配减少 **90%**。若需解构自定义类型，建议实现 `IDestructurable` 或 `IDestructured` 接口以保持固定分配特性。

### 对象池

| 场景 | 耗时 | 分配 |
| :--- | :--- | :--- |
| `WriterPool.Get<LogTextWriter>()` + `Return()`（池化路径） | 77.23 ns | 0 |
| `new LogTextWriter()`（直接分配，仅作对比） | 54.56 ns | 520 B |

池化路径（77 ns，0 分配）比直接分配（54 ns，520 B）略慢，但在高频场景下消除了 GC 压力。WriterPool 上限 128 个实例，容量超 32 KB 的 Writer 不回池（会触发底层数组归还）。

---

## ChannelTarget

`ChannelTarget` 系列用于将日志异步投递到外部消费者（网络、自定义处理器等）。

| 变体 | 耗时 | 分配 | 适用场景 |
| :--- | :--- | :--- | :--- |
| `LogEntryChannelTarget`（零编码，透传引用） | 50.30 ns | 0 | 消费者需完整 LogEntry 对象 |
| `ByteChannelTarget`（渲染 + byte[] 拷贝） | 992.23 ns | 136 B | 消费者直接写网络/文件字节流 |
| `StringChannelTarget`（渲染 + UTF-8→string 解码） | 1,065.78 ns | 208 B | 消费者需要 string 类型 |

`LogEntryChannelTarget` 的 50 ns 仅为 Channel 写入开销，渲染由消费者侧自行处理。`ByteChannelTarget` 与 `StringChannelTarget` 的 ~1 μs 包含了完整渲染，比单次 LogWriter 渲染（~330 ns）高出的部分来自 Channel 写入和字节拷贝/解码。若消费者直接处理字节流，`ByteChannelTarget` 比 `StringChannelTarget` 节省 ~74 ns 和 72 B。这种设计将极其耗时的字符串拼接和渲染逻辑（~300-800ns）彻底从业务调用线程（Caller Thread）卸载到了后台消费线程，从而最大化业务主流程的吞吐量。
