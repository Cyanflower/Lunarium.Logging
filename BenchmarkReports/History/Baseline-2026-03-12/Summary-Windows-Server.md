# Benchmark Environment: Windows Server (AMD EPYC)

The following data presents the microsecond-level performance overhead and allocation details of `Lunarium.Logging` on a server-class processor (AMD EPYC 7402) running Windows 10 Server.

---

## Environment Info

```
BenchmarkDotNet v0.14.0, Windows 10 (10.0.20348.4171)
AMD EPYC 7402, 1 CPU, 16 logical and 8 physical cores
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

| Method                                                                                    | Mean          | Error      | StdDev     | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------------------ |--------------:|-----------:|-----------:|---------:|--------:|-------:|----------:|------------:|
| No rules, level pass (cache hit)                                                          |      9.186 ns |  0.0346 ns |  0.0270 ns |     1.00 |    0.00 |      - |         - |          NA |
| Include rules, context match pass (cache hit)                                             |      9.078 ns |  0.1355 ns |  0.1201 ns |     0.99 |    0.01 |      - |         - |          NA |
| Include rules, context mismatch rejected (cache hit)                                      |      9.085 ns |  0.1554 ns |  0.1298 ns |     0.99 |    0.01 |      - |         - |          NA |
| Exclude rules, context not in exclude list pass (cache hit)                               |      9.162 ns |  0.1739 ns |  0.1626 ns |     1.00 |    0.02 |      - |         - |          NA |
| Exclude rules, context matched exclude list rejected (cache hit)                          |      9.089 ns |  0.1404 ns |  0.1173 ns |     0.99 |    0.01 |      - |         - |          NA |
| No rules, cache miss (approx., 3000 unique contexts, cache cleared after exceeding 2048)  | 12,121.558 ns | 50.3954 ns | 44.6742 ns | 1,319.64 |    6.00 | 0.0153 |     194 B |          NA |

---

## LoggerThroughputBenchmarks-report

> **Target Class**: `Logger`
> **Measurement Target**: Throughput of the caller thread pushing log entries into the Channel queue via `Logger.Log()`
>
> This benchmark measures only the write latency perceived by the caller thread. The actual filtering, formatting, and I/O overhead performed by the background worker is not included.
> It demonstrates the small boxing array allocations (~100 B range) caused by varying numbers of parameters.

| Method                                                          | Mean        | Error     | StdDev      | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |------------:|----------:|------------:|------:|--------:|-------:|-------:|----------:|------------:|
| Log(): no properties, plain text message                        |    202.2 ns |   5.31 ns |    15.31 ns |  1.01 |    0.11 | 0.0105 |      - |      89 B |        1.00 |
| Log(): single property (params object?[1] allocation)          |    171.6 ns |   4.34 ns |    12.60 ns |  0.85 |    0.09 | 0.0143 | 0.0033 |     121 B |        1.36 |
| Log(): three properties (params object?[3] allocation)         |    413.2 ns |  19.32 ns |    56.65 ns |  2.06 |    0.32 | 0.0191 |      - |     163 B |        1.83 |
| Log(): five properties (params object?[5] allocation)          |    306.4 ns |  12.74 ns |    37.38 ns |  1.52 |    0.22 | 0.0241 | 0.0007 |     201 B |        2.26 |
| Log(): via ForContext wrapper (LoggerWrapper extra indirection) |    176.6 ns |   5.22 ns |    15.22 ns |  0.88 |    0.10 | 0.0143 | 0.0017 |     120 B |        1.35 |
| Log(): batch 100 (amortized cost per entry)                     | 19,162.5 ns | 756.01 ns | 2,217.24 ns | 95.33 |   13.15 | 1.7090 | 0.0305 |   14449 B |      162.35 |

---

## LogParserBenchmarks-report

> **Target Class**: `LogParser`
> **Measurement Target**: Throughput of `LogParser.ParseMessage(string)` for template string parsing
>
> Validates the cost of breaking down structured log templates (e.g. `User {Name} logged in`) into text tokens and property tokens.
> Alignment, custom format specifiers, and escape-induced splits are all leveled out under the caching mechanism (below ~15 ns).

| Method                                                                               | Mean         | Error      | StdDev    | Ratio  | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------------- |-------------:|-----------:|----------:|-------:|--------:|-------:|-------:|-------:|----------:|------------:|
| Plain text, no placeholders (cache hit)                                              |     13.86 ns |   0.295 ns |  0.276 ns |   1.00 |    0.03 |      - |      - |      - |         - |          NA |
| Single property template (cache hit)                                                 |     10.71 ns |   0.128 ns |  0.120 ns |   0.77 |    0.02 |      - |      - |      - |         - |          NA |
| Three property template (cache hit)                                                  |     16.73 ns |   0.211 ns |  0.197 ns |   1.21 |    0.03 |      - |      - |      - |         - |          NA |
| Complex template: alignment + formatting + destructure prefix (cache hit)            |     19.55 ns |   0.272 ns |  0.241 ns |   1.41 |    0.03 |      - |      - |      - |         - |          NA |
| Template with {{ }} escape (cache hit)                                               |     13.13 ns |   0.275 ns |  0.257 ns |   0.95 |    0.03 |      - |      - |      - |         - |          NA |
| Cache miss (approx., 6000 unique string pool, cache cleared after exceeding 4096)    | 13,096.81 ns | 103.631 ns | 96.937 ns | 945.43 |   19.20 | 0.1221 | 0.0458 | 0.0153 |    1079 B |          NA |

---

## LogWriterBenchmarks-report

> **Target Classes**: `LogTextWriter`, `LogColorTextWriter`, `LogJsonWriter`, `WriterPool`
> **Measurement Target**: Throughput of `LogWriter.Render()` filling properties into templates and flushing the final output stream, plus the pooling benefit of `WriterPool`
>
> Directly demonstrates the write cost of different log formats (plain text, ANSI-colored text, JSON).
> Also shows that `WriterPool` pooling eliminates nearly all heap allocations caused by `StringBuilder` and context assembly.

| Method                                                              | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------------------------------------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| Text: plain text message (no properties)                            |   364.02 ns |  3.667 ns |  3.251 ns |  1.00 |    0.01 | 0.0134 |     112 B |        1.00 |
| Text: single property                                               |   451.82 ns |  3.996 ns |  3.738 ns |  1.24 |    0.01 | 0.0172 |     144 B |        1.29 |
| Text: four properties                                               |   602.26 ns |  8.354 ns |  7.814 ns |  1.65 |    0.03 | 0.0286 |     240 B |        2.14 |
| Text: alignment + formatting ({Count,8:D} {Percent:P1})             |   679.50 ns |  8.096 ns |  7.177 ns |  1.87 |    0.02 | 0.0296 |     248 B |        2.21 |
| Color: single property (with ANSI color escape codes)               |   477.80 ns |  2.547 ns |  2.382 ns |  1.31 |    0.01 | 0.0172 |     144 B |        1.29 |
| Color: four properties                                              |   668.48 ns |  8.632 ns |  8.074 ns |  1.84 |    0.03 | 0.0286 |     240 B |        2.14 |
| JSON: single property (with RenderedMessage + Propertys fields)     |   539.17 ns |  4.138 ns |  3.668 ns |  1.48 |    0.02 | 0.0181 |     152 B |        1.36 |
| JSON: four properties                                               | 1,107.56 ns | 14.937 ns | 13.972 ns |  3.04 |    0.05 | 0.0420 |     360 B |        3.21 |
| Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return() (pooled path)|    52.84 ns |  0.242 ns |  0.226 ns |  0.15 |    0.00 |      - |         - |        0.00 |
| Alloc: new LogTextWriter() (direct allocation, for pool comparison) |    14.15 ns |  0.306 ns |  0.287 ns |  0.04 |    0.00 | 0.0162 |     136 B |        1.21 |

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
