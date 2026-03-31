# Benchmark Environment: Linux Server (AMD EPYC)

The following data presents the microsecond-level performance overhead and allocation details of `Lunarium.Logging` on a server-class processor (AMD EPYC 7542) running Ubuntu Linux.

---

## Environment Info

```
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7542, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.104
  [Host]     : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2
BenchmarkAt: 2026-03-12
```

---

## FilterBenchmarks-report

> **Target Class**: `LoggerFilter`
> **Measurement Target**: Throughput of `LoggerFilter.ShouldEmit()`
>
> This benchmark validates the performance overhead when log entries undergo level filtering and prefix/exclude rule matching.
> The vast majority of calls should hit the zero-allocation dictionary read path at only **~9 ns**.

| Method                                                                                    | Mean          | Error      | StdDev     | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------------------ |--------------:|-----------:|-----------:|---------:|--------:|-------:|----------:|------------:|
| No rules, level pass (cache hit)                                                          |      9.361 ns |  0.0287 ns |  0.0254 ns |     1.00 |    0.00 |      - |         - |          NA |
| Include rules, context match pass (cache hit)                                             |      9.479 ns |  0.0181 ns |  0.0151 ns |     1.01 |    0.00 |      - |         - |          NA |
| Include rules, context mismatch rejected (cache hit)                                      |      9.702 ns |  0.0126 ns |  0.0105 ns |     1.04 |    0.00 |      - |         - |          NA |
| Exclude rules, context not in exclude list pass (cache hit)                               |      9.369 ns |  0.0537 ns |  0.0502 ns |     1.00 |    0.01 |      - |         - |          NA |
| Exclude rules, context matched exclude list rejected (cache hit)                          |      9.390 ns |  0.0280 ns |  0.0249 ns |     1.00 |    0.00 |      - |         - |          NA |
| No rules, cache miss (approx., 3000 unique contexts, cache cleared after exceeding 2048)  | 13,616.814 ns | 18.8979 ns | 17.6771 ns | 1,454.58 |    4.23 | 0.0153 |     194 B |          NA |

---

## LoggerThroughputBenchmarks-report

> **Target Class**: `Logger`
> **Measurement Target**: Throughput of the caller thread pushing log entries into the Channel queue via `Logger.Log()`
>
> This benchmark measures only the write latency perceived by the caller thread. The actual filtering, formatting, and I/O overhead performed by the background worker is not included.
> It demonstrates the small boxing array allocations (~100 B range) caused by varying numbers of parameters.

| Method                                                          | Mean        | Error       | StdDev      | Median      | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |------------:|------------:|------------:|------------:|-------:|--------:|-------:|----------:|------------:|
| Log(): no properties, plain text message                        |    531.4 ns |    12.67 ns |    37.35 ns |    533.9 ns |   1.01 |    0.10 | 0.0105 |      90 B |        1.00 |
| Log(): single property (params object?[1] allocation)          |    558.4 ns |    31.42 ns |    89.65 ns |    553.7 ns |   1.06 |    0.19 | 0.0143 |     123 B |        1.37 |
| Log(): three properties (params object?[3] allocation)         |    803.4 ns |    36.02 ns |   106.20 ns |    815.2 ns |   1.52 |    0.23 | 0.0191 |     165 B |        1.83 |
| Log(): five properties (params object?[5] allocation)          |    620.3 ns |    13.22 ns |    38.99 ns |    619.0 ns |   1.17 |    0.11 | 0.0243 |     204 B |        2.27 |
| Log(): via ForContext wrapper (LoggerWrapper extra indirection) |    476.1 ns |    22.29 ns |    65.73 ns |    476.5 ns |   0.90 |    0.14 | 0.0143 |     123 B |        1.37 |
| Log(): batch 100 (amortized cost per entry)                     | 75,825.1 ns | 2,749.49 ns | 8,063.79 ns | 73,601.7 ns | 143.41 |   18.41 | 1.7700 |   14855 B |      165.06 |

---

## LogParserBenchmarks-report

> **Target Class**: `LogParser`
> **Measurement Target**: Throughput of `LogParser.ParseMessage(string)` for template string parsing
>
> Validates the cost of breaking down structured log templates (e.g. `User {Name} logged in`) into text tokens and property tokens.
> Alignment, custom format specifiers, and escape-induced splits are all leveled out under the caching mechanism (below ~15 ns).

| Method                                                                               | Mean         | Error     | StdDev    | Ratio    | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------------- |-------------:|----------:|----------:|---------:|--------:|-------:|-------:|-------:|----------:|------------:|
| Plain text, no placeholders (cache hit)                                              |     13.53 ns |  0.067 ns |  0.063 ns |     1.00 |    0.01 |      - |      - |      - |         - |          NA |
| Single property template (cache hit)                                                 |     10.77 ns |  0.114 ns |  0.107 ns |     0.80 |    0.01 |      - |      - |      - |         - |          NA |
| Three property template (cache hit)                                                  |     16.72 ns |  0.056 ns |  0.050 ns |     1.24 |    0.01 |      - |      - |      - |         - |          NA |
| Complex template: alignment + formatting + destructure prefix (cache hit)            |     19.41 ns |  0.149 ns |  0.140 ns |     1.43 |    0.01 |      - |      - |      - |         - |          NA |
| Template with {{ }} escape (cache hit)                                               |     12.78 ns |  0.052 ns |  0.047 ns |     0.94 |    0.01 |      - |      - |      - |         - |          NA |
| Cache miss (approx., 6000 unique string pool, cache cleared after exceeding 4096)    | 14,142.40 ns | 99.207 ns | 92.798 ns | 1,045.12 |    8.13 | 0.1221 | 0.0458 | 0.0153 |    1080 B |          NA |

---

## LogWriterBenchmarks-report

> **Target Classes**: `LogTextWriter`, `LogColorTextWriter`, `LogJsonWriter`, `WriterPool`
> **Measurement Target**: Throughput of `LogWriter.Render()` filling properties into templates and flushing the final output stream, plus the pooling benefit of `WriterPool`
>
> Directly demonstrates the write cost of different log formats (plain text, ANSI-colored text, JSON).
> Also shows that `WriterPool` pooling eliminates nearly all heap allocations caused by `StringBuilder` and context assembly.

| Method                                                              | Mean        | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------------------------------------- |------------:|---------:|---------:|------:|-------:|----------:|------------:|
| Text: plain text message (no properties)                            |   381.02 ns | 1.301 ns | 1.153 ns |  1.00 | 0.0134 |     112 B |        1.00 |
| Text: single property                                               |   453.26 ns | 1.425 ns | 1.190 ns |  1.19 | 0.0172 |     144 B |        1.29 |
| Text: four properties                                               |   673.61 ns | 3.096 ns | 2.896 ns |  1.77 | 0.0286 |     240 B |        2.14 |
| Text: alignment + formatting ({Count,8:D} {Percent:P1})             |   717.67 ns | 4.451 ns | 4.164 ns |  1.88 | 0.0296 |     248 B |        2.21 |
| Color: single property (with ANSI color escape codes)               |   491.06 ns | 3.237 ns | 3.028 ns |  1.29 | 0.0172 |     144 B |        1.29 |
| Color: four properties                                              |   717.35 ns | 3.654 ns | 3.418 ns |  1.88 | 0.0286 |     240 B |        2.14 |
| JSON: single property (with RenderedMessage + Propertys fields)     |   562.51 ns | 2.015 ns | 1.885 ns |  1.48 | 0.0181 |     152 B |        1.36 |
| JSON: four properties                                               | 1,130.29 ns | 4.667 ns | 4.366 ns |  2.97 | 0.0420 |     360 B |        3.21 |
| Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return() (pooled path)|    55.26 ns | 0.179 ns | 0.167 ns |  0.15 |      - |         - |        0.00 |
| Alloc: new LogTextWriter() (direct allocation, for pool comparison) |    24.38 ns | 0.539 ns | 0.620 ns |  0.06 | 0.0162 |     136 B |        1.21 |


---

# Legends

- **Mean**        : Arithmetic mean of all measurements
- **Error**       : Half of 99.9% confidence interval
- **StdDev**      : Standard deviation of all measurements
- **Ratio**       : Mean of the ratio distribution ([Current]/[Baseline])
- **Gen0**        : GC Generation 0 collects per 1000 operations
- **Allocated**   : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)
- **Alloc Ratio** : Allocated memory ratio distribution ([Current]/[Baseline])
- **1 ns**        : 1 Nanosecond (0.000000001 sec)
