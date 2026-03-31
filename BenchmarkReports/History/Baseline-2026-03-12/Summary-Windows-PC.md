# Benchmark Environment: Windows 11 PC (i7-7700)

The following data presents the microsecond-level performance overhead and allocation details of `Lunarium.Logging` on a desktop processor (Intel Core i7-7700) running Windows 11.

---

## Environment Info

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8037)
Intel(R) Core(TM) i7-7700 CPU @ 3.60GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.200
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

| Method                                                                                    | Mean          | Error       | StdDev      | Median        | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------------------ |--------------:|------------:|------------:|--------------:|---------:|--------:|-------:|----------:|------------:|
| No rules, level pass (cache hit)                                                          |      9.301 ns |   0.2207 ns |   0.5246 ns |      9.228 ns |     1.00 |    0.08 |      - |         - |          NA |
| Include rules, context match pass (cache hit)                                             |      8.650 ns |   0.2088 ns |   0.3311 ns |      8.488 ns |     0.93 |    0.06 |      - |         - |          NA |
| Include rules, context mismatch rejected (cache hit)                                      |      8.908 ns |   0.2070 ns |   0.3223 ns |      8.844 ns |     0.96 |    0.06 |      - |         - |          NA |
| Exclude rules, context not in exclude list pass (cache hit)                               |      8.642 ns |   0.1989 ns |   0.2977 ns |      8.484 ns |     0.93 |    0.06 |      - |         - |          NA |
| Exclude rules, context matched exclude list rejected (cache hit)                          |      8.459 ns |   0.1953 ns |   0.1827 ns |      8.413 ns |     0.91 |    0.05 |      - |         - |          NA |
| No rules, cache miss (approx., 3000 unique contexts, cache cleared after exceeding 2048)  | 14,453.402 ns | 282.6941 ns | 325.5510 ns | 14,402.010 ns | 1,558.71 |   91.78 | 0.0305 |     189 B |          NA |

---

## LoggerThroughputBenchmarks-report

> **Target Class**: `Logger`
> **Measurement Target**: Throughput of the caller thread pushing log entries into the Channel queue via `Logger.Log()`
>
> This benchmark measures only the write latency perceived by the caller thread. The actual filtering, formatting, and I/O overhead performed by the background worker is not included.
> It demonstrates the small boxing array allocations (~100 B range) caused by varying numbers of parameters.

| Method                                                          | Mean        | Error     | StdDev    | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |------------:|----------:|----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| Log(): no properties, plain text message                        |    125.2 ns |   2.15 ns |   2.01 ns |   1.00 |    0.02 | 0.0212 | 0.0002 |      89 B |        1.00 |
| Log(): single property (params object?[1] allocation)          |    133.2 ns |   1.55 ns |   1.37 ns |   1.06 |    0.02 | 0.0288 |      - |     121 B |        1.36 |
| Log(): three properties (params object?[3] allocation)         |    139.2 ns |   2.27 ns |   2.23 ns |   1.11 |    0.02 | 0.0384 | 0.0007 |     162 B |        1.82 |
| Log(): five properties (params object?[5] allocation)          |    148.0 ns |   2.96 ns |   3.40 ns |   1.18 |    0.03 | 0.0482 | 0.0005 |     202 B |        2.27 |
| Log(): via ForContext wrapper (LoggerWrapper extra indirection) |    125.7 ns |   2.49 ns |   2.20 ns |   1.00 |    0.02 | 0.0288 | 0.0002 |     121 B |        1.36 |
| Log(): batch 100 (amortized cost per entry)                     | 13,854.6 ns | 141.09 ns | 131.98 ns | 110.73 |    2.00 | 3.4027 | 0.1984 |   14522 B |      163.17 |

---

## LogParserBenchmarks-report

> **Target Class**: `LogParser`
> **Measurement Target**: Throughput of `LogParser.ParseMessage(string)` for template string parsing
>
> Validates the cost of breaking down structured log templates (e.g. `User {Name} logged in`) into text tokens and property tokens.
> Alignment, custom format specifiers, and escape-induced splits are all leveled out under the caching mechanism (below ~15 ns).

| Method                                                                               | Mean         | Error      | StdDev     | Ratio    | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------------- |-------------:|-----------:|-----------:|---------:|--------:|-------:|-------:|-------:|----------:|------------:|
| Plain text, no placeholders (cache hit)                                              |     14.99 ns |   0.359 ns |   0.580 ns |     1.00 |    0.05 |      - |      - |      - |         - |          NA |
| Single property template (cache hit)                                                 |     11.40 ns |   0.293 ns |   0.371 ns |     0.76 |    0.04 |      - |      - |      - |         - |          NA |
| Three property template (cache hit)                                                  |     18.42 ns |   0.427 ns |   0.927 ns |     1.23 |    0.08 |      - |      - |      - |         - |          NA |
| Complex template: alignment + formatting + destructure prefix (cache hit)            |     20.56 ns |   0.472 ns |   0.838 ns |     1.37 |    0.08 |      - |      - |      - |         - |          NA |
| Template with {{ }} escape (cache hit)                                               |     14.05 ns |   0.342 ns |   0.320 ns |     0.94 |    0.04 |      - |      - |      - |         - |          NA |
| Cache miss (approx., 6000 unique string pool, cache cleared after exceeding 4096)    | 15,322.49 ns | 299.344 ns | 367.622 ns | 1,023.64 |   45.53 | 0.1678 | 0.0763 | 0.0305 |    1074 B |          NA |

---

## LogWriterBenchmarks-report

> **Target Classes**: `LogTextWriter`, `LogColorTextWriter`, `LogJsonWriter`, `WriterPool`
> **Measurement Target**: Throughput of `LogWriter.Render()` filling properties into templates and flushing the final output stream, plus the pooling benefit of `WriterPool`
>
> Directly demonstrates the write cost of different log formats (plain text, ANSI-colored text, JSON).
> Also shows that `WriterPool` pooling eliminates nearly all heap allocations caused by `StringBuilder` and context assembly.

| Method                                                              | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------------------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| Text: plain text message (no properties)                            | 312.83 ns |  5.786 ns |  6.191 ns | 311.01 ns |  1.00 |    0.03 | 0.0267 |     112 B |        1.00 |
| Text: single property                                               | 379.28 ns |  7.602 ns | 11.609 ns | 372.93 ns |  1.21 |    0.04 | 0.0343 |     144 B |        1.29 |
| Text: four properties                                               | 528.72 ns | 10.603 ns | 19.389 ns | 517.78 ns |  1.69 |    0.07 | 0.0572 |     240 B |        2.14 |
| Text: alignment + formatting ({Count,8:D} {Percent:P1})             | 600.71 ns | 11.913 ns | 19.904 ns | 591.67 ns |  1.92 |    0.07 | 0.0591 |     248 B |        2.21 |
| Color: single property (with ANSI color escape codes)               | 435.53 ns |  8.727 ns | 12.234 ns | 431.92 ns |  1.39 |    0.05 | 0.0343 |     144 B |        1.29 |
| Color: four properties                                              | 628.98 ns | 12.530 ns | 19.508 ns | 627.70 ns |  2.01 |    0.07 | 0.0572 |     240 B |        2.14 |
| JSON: single property (with RenderedMessage + Propertys fields)     | 510.44 ns | 10.211 ns | 15.897 ns | 507.24 ns |  1.63 |    0.06 | 0.0362 |     152 B |        1.36 |
| JSON: four properties                                               | 975.17 ns | 19.241 ns | 36.608 ns | 966.14 ns |  3.12 |    0.13 | 0.0858 |     360 B |        3.21 |
| Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return() (pooled path)|  77.15 ns |  1.565 ns |  1.675 ns |  76.61 ns |  0.25 |    0.01 |      - |         - |        0.00 |
| Alloc: new LogTextWriter() (direct allocation, for pool comparison) |  16.11 ns |  0.488 ns |  1.431 ns |  15.40 ns |  0.05 |    0.00 | 0.0325 |     136 B |        1.21 |

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
