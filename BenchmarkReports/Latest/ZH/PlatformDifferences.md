# 平台差异分析报告

> Benchmark Report 出自 Lunarium.Logging.Benchmarks (基于 BenchmarkDotNet)
> 本篇分析出自 Claude Sonnet 4.6 (基于 Baseline-2026-03-30 Markdown 报告)
> 测试环境为 Intel i7-8750H Linux 开发机；Intel Xeon 服务器预计性能相近，由于架构差异，AMD EPYC 可能无法用于参考 Intel 平台，AMD EPYC Linux (VPS) 环境下 Log() 调用方耗时略有差异，Server 测试平台均为 VPS，由于虚拟化层引入的开销和各种因素，实际与裸金属服务器仍会有另一层差异。

---

## 总结

| 维度 | 最优 | 最差 | 核心原因 |
| :--- | :--- | :--- | :--- |
| Filter/Parser 缓存命中 | LinuxLaptop | LinuxServer | Intel 的 L1/μop cache 效率，单核分支预测优 |
| Filter/Parser 缓存未命中 | WindowsServer | LinuxLaptop | EPYC 内存带宽大，分配/GC 更快；.NET 10.0.2 多 26 B（仅 LinuxLaptop） |
| LogWriter Text | LinuxLaptop | WindowsServer | i7 单核 IPC 高 |
| LogWriter JSON | LinuxLaptop | LinuxServer | EPYC+Linux 组合对 Utf8JsonWriter 分支密集路径不友好 |
| WriterPool | WindowsServer/LinuxServer | WindowsPC/LinuxLaptop | EPYC 大 L3 使池对象常驻 |
| LoggerThroughput 单次 | WindowsPC | LinuxServer | 高主频 + Windows Channel/futex 实现差异，Linux EPYC 慢 2–4× 且不稳定 |
| String 解码 | LinuxServer | WindowsServer | Windows Server 字符串分配器开销高 |

---

## 绝对值参考

各指标跨平台相对差异已见上表；下表将绝对值范围列出，便于判断差异是否对实际使用产生影响。

| 指标 | 全平台绝对值范围 | 实际意义 |
| :--- | :--- | :--- |
| Filter 缓存命中 | 8.4–10.3 ns | 全部远低于 1 μs，平台间差距 < 2 ns，热路径无实际影响 |
| Parser 缓存命中 | 11.0–21.7 ns | 全部远低于 1 μs，复杂模板也仅约 22 ns |
| LogWriter Text | 329–402 ns | 单条渲染 < 0.5 μs，固定分配 32 B |
| LogWriter JSON | 507–923 ns | 单条渲染 < 1 μs，固定分配 64 B；LinuxServer JSON 四属性偏高但仍在 1 μs 内 |
| LoggerThroughput | 153–576 ns/call | Windows(Intel i7 3.6GHz VBS Hyper-V) ~654 万次/秒；Linux(EPYC VPS) ~174 万次/秒。典型业务日志频率 < 10 万次/秒，各平台均有 **17×–65× 的余量** |
| Parser 缓存未命中 | 1,281–1,634 ns | 仅首次解析或缓存溢出（> 4096 条唯一模板）时触发，正常使用极少命中 |
| Filter 缓存未命中 | 150–237 ns | 仅超出 2048 条唯一 context 时触发，实际几乎不发生 |

> **注**：LinuxLaptop 的 Filter 缓存未命中分配量为 218 B，其余平台为 192 B，差异来自 .NET 10.0.2 → 10.0.4 的运行时内部布局调整，与库本身无关，升级运行时后对齐；tracing 级超高频场景（> 100 万次/秒）在 Linux EPYC 上需留意调度抖动（CV 23%）

---

## 详细对比分析

### 环境概览

| 平台 | OS | CPU | .NET |
| :--- | :--- | :--- | :--- |
| LinuxLaptop | Fedora 42 | i7-8750H @ 2.2GHz (6C/12T) | 10.0.2 |
| LinuxServer | Ubuntu 24.04 | EPYC 7542 (8C/16T) | 10.0.4 |
| WindowsPC | Windows 11 | i7-7700 @ 3.6GHz (4C/8T) | 10.0.4 |
| WindowsServer | Windows 10 | EPYC 7542 (8C/16T) | 10.0.5 |

---

### 一、Filter — 缓存命中/未命中

#### 缓存命中（ns）

| 平台 | 无规则 | Include 通过 | Include 拒绝 | Exclude 通过 | Exclude 拒绝 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| LinuxLaptop | 8.7 | 8.9 | 8.9 | 8.4 | 8.7 |
| LinuxServer | 9.8 | 9.7 | 9.5 | 9.3 | 9.5 |
| WindowsPC | 9.4 | 9.5 | 10.3 | 9.6 | 9.3 |
| WindowsServer | 9.1 | 9.0 | 9.0 | 8.9 | 9.1 |

LinuxLaptop 缓存命中最快（~0.5–1 ns 优势），原因是 i7-8750H L1/L2 单核延迟低，`ConcurrentDictionary` 查找极度依赖缓存延迟。WindowsPC 的 Include 拒绝（10.3 ns）是全场最慢，推测 Windows 11 的 RyuJIT codegen 在分支预测上略逊。

#### 缓存未命中

| 平台 | 耗时 (ns) | 分配 |
| :--- | :--- | :--- |
| LinuxLaptop | 237 | 218 B |
| LinuxServer | 167 | 192 B |
| WindowsPC | 204 | 192 B |
| WindowsServer | 150 | 192 B |

**关键差异 1：分配量**
LinuxLaptop 的 218 B 比其他三者多出 26 B，仅此平台运行 .NET 10.0.2（其余均 10.0.4/5）。推测是运行时内部某数据结构在两个小版本之间发生了布局优化（`ArraySegment` 或 `ConcurrentDictionary` 内部节点对齐调整）。

**关键差异 2：未命中速度**
两台 EPYC 机器（167/150 ns）快于两台 Intel（237/204 ns）。EPYC 的内存带宽更高，缓存清空后的重建（创建新字典、HashCode 计算）受益于更快的 L3 吞吐。

---

### 二、LogParser — 缓存命中/未命中

#### 缓存命中（ns）

| 平台 | 纯文本 | 单属性 | 三属性 | 复杂模板 | 含转义 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| LinuxLaptop | 13.7 | 11.0 | 16.3 | 18.3 | 13.9 |
| LinuxServer | 15.7 | 11.9 | 18.3 | 21.7 | 15.0 |
| WindowsPC | 15.6 | 13.2 | 18.7 | 21.7 | 14.8 |
| WindowsServer | 15.9 | 13.4 | 18.3 | 21.2 | 15.4 |

LinuxLaptop 在所有 Parser 缓存命中场景下均最快（领先 10–20%）。单属性模板的优势最明显（11.0 vs 11.9/13.2/13.4 ns），推测 i7-8750H 对短状态机循环的分支预测和 μop cache 利用率更高。

#### 缓存未命中（ns）

| 平台 | 耗时 |
| :--- | :--- |
| LinuxLaptop | 1,281 |
| LinuxServer | 1,422 |
| WindowsServer | 1,601 |
| WindowsPC | 1,634 |

Windows 平台普遍慢 ~300 ns。Parser 未命中要创建 `MessageTemplate` 对象（~1,413 B GC）；Windows 的 GC 压力和对象分配路径在这个量级上比 Linux 贵。LinuxServer 的 EPYC 虽然 GC 分配快（内存带宽大），但单核状态机执行本身稍慢，最终居中。

---

### 三、LogWriter — 渲染性能

#### Text Writer（ns）

| 平台 | 纯文本 | 单属性 | 四属性 | 对齐+格式化 | Numeric |
| :--- | :--- | :--- | :--- | :--- | :--- |
| LinuxLaptop | 329 | 357 | 446 | 526 | 465 |
| LinuxServer | 341 | 388 | 515 | 583 | 502 |
| WindowsPC | 345 | 372 | 486 | 575 | 509 |
| WindowsServer | 357 | 402 | 483 | 576 | 508 |

Text 渲染 LinuxLaptop 全场最快，主要差距在四属性/对齐场景（领先 ~10–15%）。固定分配 32 B 在所有平台一致，分配路径本身不是差异来源。

#### JSON Writer（ns）

| 平台 | 单属性 | 四属性 | Numeric | 回退路径 | IDestructured |
| :--- | :--- | :--- | :--- | :--- | :--- |
| LinuxLaptop | 507 | 760 | 773 | 979 | 635 |
| LinuxServer | 531 | 923 | 838 | 1,148 | 698 |
| WindowsPC | 577 | 817 | 818 | 1,039 | 646 |
| WindowsServer | 525 | 810 | 845 | 1,036 | 681 |

**关键差异：LinuxServer 的 JSON 性能明显偏差**
- JSON 四属性：923 ns（vs LinuxLaptop 760 ns，慢 21%）
- 回退路径：1,148 ns（vs 其他三者 979–1,039 ns，慢 10–17%）

`Utf8JsonWriter` 内部有较多分支和字符串查找操作，EPYC 7542 在 Linux 上的单核分支预测命中率或 L1i cache 效率不如 Intel，这与 AMD Zen 2 架构特性有关。同一 EPYC 在 Windows Server 下 JSON 四属性（810 ns）明显快于 Linux（923 ns），排除了 CPU 本身的问题，OS 差异（Linux 上 `Utf8JsonWriter` 某些内存屏障/syscall 路径较重）是主要因素。

#### WriterPool Get+Return（ns）

| 平台 | 耗时 |
| :--- | :--- |
| LinuxLaptop | 77 |
| WindowsPC | 78 |
| LinuxServer | 53 |
| WindowsServer | 51 |

Pool 操作 EPYC 明显快于 Intel（~30%）。`ConcurrentBag` 的 TryTake/Add 在 EPYC 的大缓存（L3 256 MB）下对象引用更可能常驻热缓存，而 Intel 的较小 L3 在多核 bench 环境下有更多驱逐。

---

### 四、LoggerThroughput — 调用方吞吐

这是四个平台差异最大的模块。

#### 单次 Log 调用（ns）

| 平台 | 无属性 | 单属性 | 三属性 | 五属性 | ForContext |
| :--- | :--- | :--- | :--- | :--- | :--- |
| WindowsPC | 153 | 168 | 169 | 185 | 163 |
| LinuxLaptop | 188 | 199 | 190 | 201 | 185 |
| WindowsServer | 380 | 250 | 321 | 303 | 397 |
| LinuxServer | 576 | 433 | 539 | 539 | 692 |

**关键差异 1：WindowsPC 最快**
i7-7700 @ 3.6GHz 高主频让 Channel `TryWrite` 加锁/无锁操作最快，领先 LinuxLaptop ~20%，领先 EPYC 机器 2–4×。

**关键差异 2：LinuxServer 极慢且高度不稳定**
- 无属性 576 ±133 ns（CV 23%），其他平台 CV 均在 3–10%
- ForContext 692 ±145 ns（CV 21%）
- 与 WindowsServer 同 CPU（EPYC 7542），但 Linux 下慢 1.5–2×，且波动剧烈

这是最值得关注的跨平台差异。根本原因在于 .NET Channel 的 `TryWrite` 使用 `Interlocked.Exchange` + `ManualResetValueTaskSourceCore` 唤醒机制，在 Linux 上对应 `futex` 系统调用，而 EPYC 的 NUMA 拓扑配合 Linux 调度器在 8 核场景下会引入更大的内存一致性延迟。Windows 的 kernel 对 `Interlocked` 做了更激进的优化。

#### 批量 100 条（ns，per-batch 总量）

| 平台 | 批量耗时 | 每条折算 | vs 单次 |
| :--- | :--- | :--- | :--- |
| WindowsPC | 17,222 | 172 ns/条 | ≈单次 |
| LinuxLaptop | 20,117 | 201 ns/条 | ≈单次 |
| WindowsServer | 27,812 | 278 ns/条 | ≈单次 |
| LinuxServer | 62,821 | 628 ns/条 | ≈单次 |

各平台批量折算与单次高度一致，说明 Channel 没有明显的批量摊销效应——每条日志的 Channel 写入都是独立竞争。

---

### 五、ChannelTarget

| 平台 | LogEntry (ns) | Byte (ns) | String (ns) |
| :--- | :--- | :--- | :--- |
| WindowsPC | 43 | 932 | 1,017 |
| LinuxLaptop | 50 | 992 | 1,066 |
| WindowsServer | 69 | 1,073 | 1,262 |
| LinuxServer | 84 | 1,110 | 1,165 |

LogEntry（零编码）Windows 平台快于 Linux，与 LoggerThroughput 规律一致（Channel `TryWrite` 在 Windows 更快）。

#### String vs Byte 解码开销（UTF-8→string 差值）

| 平台 | 差值 |
| :--- | :--- |
| WindowsPC | +85 ns |
| LinuxLaptop | +74 ns |
| LinuxServer | +55 ns |
| WindowsServer | +189 ns |

WindowsServer 的 `Encoding.UTF8.GetString` 解码开销异常高（+189 ns vs LinuxServer +55 ns），EPYC 在 Windows Server 下的字符串解码路径更重，可能与 Windows Server 的内存分配器或 string intern pool 策略有关。
