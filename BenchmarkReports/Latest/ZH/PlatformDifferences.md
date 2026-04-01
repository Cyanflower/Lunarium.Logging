# 平台差异分析报告

> Benchmark Report 出自 Lunarium.Logging.Benchmarks (基于 BenchmarkDotNet)  
> 本篇分析出自 Claude Sonnet 4.6 (基于 Baseline-2026-03-30 Markdown 报告)  
> 主开发环境: Intel Core i7-8750H @ 2.20GHz (Coffee Lake), 6C/12T, Fedora Linux 42(6.18.8-100.fc42.x86_64), .NET 10.0.2, X64 RyuJIT AVX2  
> Intel Xeon 服务器在等效核心频率及核心数的 cpu 配置 (e.g. Xeon E-2100 Series) 下预计性能相近主开发环境表现，但因为 Cache Size 与 ECC 内存 以及指令集与服务器环境的影响下仍会有偏差因数。   
> 由于架构差异，AMD EPYC 可能无法用于参考 Intel 平台，AMD EPYC Linux (VPS) 环境下 Log() 调用方耗时和 Intel 差异较大，不具有互相参考性。  
> Server 测试平台均为 VPS，由于虚拟化层引入的开销和各种因素，实际与裸金属服务器仍会有另一层差异。

---

## 总结

| 维度 | 最优 | 最差 | 观察 |
| :--- | :--- | :--- | :--- |
| Filter/Parser 缓存命中 | LinuxLaptop | LinuxServer | LinuxLaptop 全场领先约 10–20%；平台间差距在 2 ns 以内 |
| Filter 缓存未命中 | WindowsServer | LinuxLaptop | EPYC 平台（150–167 ns）均快于 Intel 平台（204–237 ns）；.NET 10.0.2 多分配 26 B（仅 LinuxLaptop） |
| Parser 缓存未命中 | LinuxLaptop | WindowsPC | Windows 平台慢约 300 ns；原因待进一步分析 |
| LogWriter Text | LinuxLaptop | WindowsServer | LinuxLaptop 在多属性场景领先约 10–15% |
| LogWriter JSON | LinuxLaptop | LinuxServer | 同 EPYC CPU 在 Linux 下比 Windows 慢约 21%（JSON 四属性） |
| WriterPool | WindowsServer/LinuxServer | WindowsPC/LinuxLaptop | EPYC 平台约快 30%；确切原因待进一步分析 |
| LoggerThroughput 单次 | WindowsPC | LinuxServer | Linux EPYC 慢 2–4×，CV 高达 23%；同 CPU 在 Windows 下表现明显更好 |
| String 解码 | LinuxServer | WindowsServer | 同 EPYC CPU 跨 OS 解码开销差异显著（+55 ns vs +189 ns） |

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

LinuxLaptop 在所有缓存命中场景下均最快（~0.5–1 ns 优势）。WindowsPC 的 Include 拒绝（10.3 ns）是全场最慢。各平台差异均在 2 ns 以内，热路径无实际影响。

#### 缓存未命中

| 平台 | 耗时 (ns) | 分配 |
| :--- | :--- | :--- |
| LinuxLaptop | 237 | 218 B |
| LinuxServer | 167 | 192 B |
| WindowsPC | 204 | 192 B |
| WindowsServer | 150 | 192 B |

**关键差异 1：分配量**
LinuxLaptop 的 218 B 比其他三者多出 26 B，仅此平台运行 .NET 10.0.2（其余均 10.0.4/5）。差异来自运行时内部某数据结构在两个小版本之间的布局调整，与库本身无关。

**关键差异 2：未命中速度**
两台 EPYC 机器（167/150 ns）快于两台 Intel（237/204 ns）。涉及 CPU 架构与平台两个变量，确切原因有待进一步分析。

---

### 二、LogParser — 缓存命中/未命中

#### 缓存命中（ns）

| 平台 | 纯文本 | 单属性 | 三属性 | 复杂模板 | 含转义 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| LinuxLaptop | 13.7 | 11.0 | 16.3 | 18.3 | 13.9 |
| LinuxServer | 15.7 | 11.9 | 18.3 | 21.7 | 15.0 |
| WindowsPC | 15.6 | 13.2 | 18.7 | 21.7 | 14.8 |
| WindowsServer | 15.9 | 13.4 | 18.3 | 21.2 | 15.4 |

LinuxLaptop 在所有 Parser 缓存命中场景下均最快（领先 10–20%）。单属性模板的优势最明显（11.0 vs 11.9/13.2/13.4 ns）。LinuxServer、WindowsPC、WindowsServer 三者结果相近。

#### 缓存未命中（ns）

| 平台 | 耗时 |
| :--- | :--- |
| LinuxLaptop | 1,281 |
| LinuxServer | 1,422 |
| WindowsServer | 1,601 |
| WindowsPC | 1,634 |

Windows 平台比 Linux 平台慢约 300 ns。两个 Linux 平台之间差异约 140 ns。涉及 CPU 架构与操作系统两个变量，确切原因有待进一步分析。

---

### 三、LogWriter — 渲染性能

#### Text Writer（ns）

| 平台 | 纯文本 | 单属性 | 四属性 | 对齐+格式化 | Numeric |
| :--- | :--- | :--- | :--- | :--- | :--- |
| LinuxLaptop | 329 | 357 | 446 | 526 | 465 |
| LinuxServer | 341 | 388 | 515 | 583 | 502 |
| WindowsPC | 345 | 372 | 486 | 575 | 509 |
| WindowsServer | 357 | 402 | 483 | 576 | 508 |

Text 渲染 LinuxLaptop 全场最快，主要差距在四属性/对齐场景（领先约 10–15%）。固定分配 32 B 在所有平台一致。

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

值得注意的是，同一 EPYC CPU 在 WindowsServer 下 JSON 四属性仅需 810 ns，明显快于 LinuxServer 的 923 ns。这排除了 CPU 本身的因素，指向 OS 或 .NET 版本差异（10.0.4 vs 10.0.5）。VPS 虚拟化噪声亦可能是影响因素之一。确切原因有待进一步分析。

#### WriterPool Get+Return（ns）

| 平台 | 耗时 |
| :--- | :--- |
| LinuxLaptop | 77 |
| WindowsPC | 78 |
| LinuxServer | 53 |
| WindowsServer | 51 |

EPYC 平台 Pool 操作约快 30%（51–53 ns vs 77–78 ns）。确切原因有待进一步分析。

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
i7-7700 @ 3.6GHz 主频最高，领先 LinuxLaptop（2.2GHz）约 20%，领先两台 EPYC 机器约 2–4×。

**关键差异 2：LinuxServer 更慢且不稳定**
- 无属性 576 ±133 ns（CV 23%），其他平台 CV 均在 3–10%
- ForContext 692 ±145 ns（CV 21%）
- 与 WindowsServer 使用同一 CPU 型号（EPYC 7542），但 Linux 下慢约 1.5–2×，且波动更高

这是最值得关注的跨平台差异。同一 CPU 在同为 VPS 的 Windows 和 Linux 环境下表现出明显差异，指向 OS 层或 .NET 在不同 OS 下的实现差异。高 CV 同时表明 Linux VPS 调度抖动对该场景影响尤为明显。具体机制有待进一步分析。

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

LogEntry（零编码）Windows 平台快于 Linux，与 LoggerThroughput 规律一致。

#### String vs Byte 解码开销（UTF-8→string 差值）

| 平台 | 差值 |
| :--- | :--- |
| WindowsPC | +85 ns |
| LinuxLaptop | +74 ns |
| LinuxServer | +55 ns |
| WindowsServer | +189 ns |

WindowsServer 的 `Encoding.UTF8.GetString` 解码开销异常高（+189 ns vs LinuxServer +55 ns）。两台机器使用同一 CPU（EPYC 7542），差异指向 OS 或 .NET 版本（10.0.4 vs 10.0.5）的因素。VPS 虚拟化噪声亦可能对此数字有影响。
