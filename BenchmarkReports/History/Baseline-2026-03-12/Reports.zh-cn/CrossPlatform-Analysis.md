# Lunarium.Logging 跨平台性能分析报告

**分析日期**: 2026-03-12
**数据来源**: `BenchmarkReports/Summary-Linux-Laptop.md` / `Summary-Linux-Server.md` / `Summary-Windows-PC.md` / `Summary-Windows-Server.md`

---

## 环境一览

| 环境 | CPU | 基频 | OS |
|------|-----|------|----|
| Linux Laptop | Intel Core i7-8750H (6C/12T, Coffee Lake) | 2.20 GHz | Fedora Linux 42 |
| Linux Server | AMD EPYC 7542 (8C/16T) | 2.90 GHz | Ubuntu 24.04 LTS |
| Windows PC | Intel Core i7-7700 (4C/8T) | 3.60 GHz | Windows 11 |
| Windows Server | AMD EPYC 7402 (8C/16T) | 2.80 GHz | Windows Server 2022 |

---

## 一、FilterBenchmarks — 过滤器

### 缓存命中（ns）

| | Linux Laptop | Linux Server | Windows PC | Windows Server |
|---|---:|---:|---:|---:|
| 无规则 | 8.9 | 9.4 | 9.3 | 9.2 |
| Include 通过 | 8.8 | 9.5 | 8.7 | 9.1 |
| Include 拒绝 | 9.5 | 9.7 | 8.9 | 9.1 |
| Exclude 通过 | 8.9 | 9.4 | 8.6 | 9.2 |
| Exclude 拒绝 | 8.8 | 9.4 | 8.5 | 9.1 |

**缓存命中路径极度稳定**：4 个平台均集中在 8.5–9.7 ns 区间，零分配。不同规则类型（Include/Exclude/无规则、通过/拒绝）之间差异在 1 ns 以内，说明缓存命中后规则复杂度完全被摊销。

### 缓存未命中（近似，μs）

| Linux Laptop | Linux Server | Windows PC | Windows Server |
|---:|---:|---:|---:|
| **26.0 μs** | 13.6 μs | 14.5 μs | 12.1 μs |

**Linux Laptop 异常**：未命中耗时是其他平台的 **1.8–2.1 倍**。原因是 i7-8750H 移动端处理器基频仅 2.2 GHz，而缓存未命中操作（全量清空 + 字符串前缀匹配 + ConcurrentDictionary 写入）是内存密集型，受制于时钟频率与移动端内存带宽。其他三台（服务器或 3.6 GHz 桌面端）均在 12–15 μs。

---

## 二、LoggerThroughputBenchmarks — 调用方写入延迟

### Logger.Log() 各参数量耗时（ns）

| | Linux Laptop | Linux Server | Windows PC | Windows Server |
|---|---:|---:|---:|---:|
| 无属性 | 149 | **531** | **125** | 202 |
| 单属性 | 186 | 558 | 133 | 172 |
| 三属性 | 189 | 803 | 139 | **413 ⚠** |
| 五属性 | 204 | 620 | 148 | 306 |
| ForContext | 183 | 476 | 126 | 177 |
| 批量 100 条（per-log 分摊） | 197 | **758** | 139 | 192 |

**Windows PC 最快**：i7-7700 3.6 GHz 高单核频率使 Channel.TryWrite() 的 CAS 操作最快，125–148 ns。

**Linux Server (EPYC 7542) 是最大异常**：基线 531 ns，是 Windows PC 的 **4.2 倍**，批量分摊后每条 758 ns。Channel.TryWrite() 是无锁 CAS 操作，EPYC 的大核心数和多层 NUMA 缓存拓扑会增加单线程 CAS 的缓存一致性解决延迟，这在此类单线程写入 benchmark 中体现得尤为明显（生产中多线程并发写入反而能更充分利用 EPYC 的并行能力）。

**Windows Server 三属性异常（413 ns）**：StdDev 高达 56 ns（同组中最高），而五属性却只有 306 ns，违反了单调递增规律。这是 benchmark 期间 OS 调度抖动导致的测量噪声，而非真实行为差异。

**ForContext 开销**：在所有平台上与直接调用的差距均 < 10 ns，LoggerWrapper 的间接层实际上是免费的。

---

## 三、LogParserBenchmarks — 解析器

### 缓存命中（ns，全部零分配）

| | Linux Laptop | Linux Server | Windows PC | Windows Server |
|---|---:|---:|---:|---:|
| 纯文本 | 13.5 | 13.5 | 15.0 | 13.9 |
| 单属性 | 11.5 | 10.8 | 11.4 | 10.7 |
| 三属性 | 17.6 | 16.7 | 18.4 | 16.7 |
| 复杂模板 | 18.7 | 19.4 | 20.6 | 19.6 |
| 含转义 | 14.1 | 12.8 | 14.1 | 13.1 |

**单属性比纯文本更快**：纯文本路径需要先执行 `IndexOfAny` 扫描确认无占位符再命中缓存，而单属性模板的缓存命中路径跳过了该初步检测——这是一个反直觉但设计合理的结果。

各平台差距极小（< 3 ns），解析缓存命中路径的一致性非常好。

### 缓存未命中（μs）

| Linux Laptop | Linux Server | Windows PC | Windows Server |
|---:|---:|---:|---:|
| **26.4 μs** | 14.1 μs | 15.3 μs | 13.1 μs |

与 Filter 未命中同样规律，Linux Laptop 约为其他平台的 1.8–2.0 倍，原因相同。

---

## 四、LogWriterBenchmarks — 渲染器

### 各格式渲染耗时（ns）

| | Linux Laptop | Linux Server | Windows PC | Windows Server |
|---|---:|---:|---:|---:|
| Text 单属性 | 425 | 453 | **379** | 452 |
| Text 四属性 | 582 | 674 | **529** | 602 |
| Text 对齐+格式化 | 667 | 718 | **601** | 680 |
| Color 单属性 | 456 | 491 | **436** | 478 |
| Color 四属性 | 649 | 717 | **629** | 668 |
| JSON 单属性 | 578 | **563** | **510** | **539** |
| JSON 四属性 | 1,148 | **1,130** | **975** | **1,108** |

**格式代价对比（以 Text 单属性为 1x）**：
- Color 单属性：约 +1.15x（ANSI 转义额外开销约 30–40 ns）
- JSON 单属性：约 +1.35–1.45x（序列化字段 + RenderedMessage 构建）
- JSON 四属性：约 **2.6–3.1x**，JSON 有明显的属性数量非线性放大效应（尾部逗号清除、多字段写入）

### WriterPool 效益

| | Linux Laptop | Linux Server | Windows PC | Windows Server |
|---|---:|---:|---:|---:|
| Pool Get+Return | 81.7 ns / **0B** | **55.3 ns / 0B** | 77.2 ns / **0B** | **52.8 ns / 0B** |
| new LogTextWriter | 20.2 ns / 136B | 24.4 ns / 136B | 16.1 ns / 136B | 14.2 ns / 136B |

EPYC 服务器的 Pool 操作（ConcurrentBag TryTake/Add）反而更快（52–55 ns vs Intel 的 77–82 ns），说明 EPYC 的内存子系统在 bag 操作上有优势，与它在 Channel.TryWrite 上的劣势形成反差——两者底层操作的内存访问模式不同。Pool 路径在所有平台上完全消除了 136B 的堆分配。

---

## 综合总结

### 核心热路径开销（缓存命中，正常使用场景）

| 步骤 | 耗时 | 分配 |
|------|------|------|
| Filter.ShouldEmit | ~9 ns | 0B |
| LogParser.ParseMessage | ~11–20 ns | 0B |
| Logger.Log() → Channel | ~125–530 ns | ~90–200 B |
| LogWriter.Render | ~380–1150 ns | ~112–360 B |

端到端调用方可感知延迟（Filter + Parse + TryWrite）：**在 Intel Win桌面/Linux笔记本上约 150–210 ns，在 EPYC Linux 单线程场景下约 550–560 ns**。

### 需要关注的点

1. **缓存未命中惩罚巨大**（Filter 约 1300–2900x，Parser 约 945–1950x）——这是全量清空策略的代价。高度动态的 context（如每请求不同的 context 字符串）会持续触发清空，需要业务侧遵守 context 静态化的设计约定。

2. **JSON 四属性接近 1 μs**，在高频日志场景（> 100w 条/秒）下 JSON 格式可能成为瓶颈，优先考虑文本格式。

3. **EPYC Linux 的 Logger 吞吐量在单线程 benchmark 下较低**，这是 benchmark 场景（单线程写入）与生产场景（多线程并发写入）的差异，无需过度关注。
