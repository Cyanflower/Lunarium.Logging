# Benchmark Environment: Linux Laptop (i7-8750H)

The following data presents the microsecond-level performance overhead and allocation details of `Lunarium.Logging` on a mobile processor (Intel Core i7-8750H) running Fedora Linux.

---

## Environment Info

```
BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
BenchmarkAt: 2026-03-12
```

---

## FilterBenchmarks-report

> **Target Class**: `LoggerFilter`
> **Measurement Target**: Throughput of `LoggerFilter.ShouldEmit()`
>
> This benchmark validates the performance overhead when log entries undergo level filtering and prefix/exclude rule matching.
> The vast majority of calls should hit the zero-allocation dictionary read path at only **~9 ns**.

| Method                                                                                    | Mean          | Error       | StdDev      | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------------------ |--------------:|------------:|------------:|---------:|--------:|-------:|----------:|------------:|
| No rules, level pass (cache hit)                                                          |      8.917 ns |   0.2037 ns |   0.2346 ns |     1.00 |    0.04 |      - |         - |          NA |
| Include rules, context match pass (cache hit)                                             |      8.790 ns |   0.0863 ns |   0.0720 ns |     0.99 |    0.03 |      - |         - |          NA |
| Include rules, context mismatch rejected (cache hit)                                      |      9.523 ns |   0.1237 ns |   0.1158 ns |     1.07 |    0.03 |      - |         - |          NA |
| Exclude rules, context not in exclude list pass (cache hit)                               |      8.888 ns |   0.2043 ns |   0.1911 ns |     1.00 |    0.03 |      - |         - |          NA |
| Exclude rules, context matched exclude list rejected (cache hit)                          |      8.811 ns |   0.1450 ns |   0.1210 ns |     0.99 |    0.03 |      - |         - |          NA |
| No rules, cache miss (approx., 3000 unique contexts, cache cleared after exceeding 2048)  | 26,014.008 ns | 419.6278 ns | 392.5201 ns | 2,919.23 |   84.74 | 0.0305 |     216 B |          NA |

---

## LoggerThroughputBenchmarks-report

> **Target Class**: `Logger`
> **Measurement Target**: Throughput of the caller thread pushing log entries into the Channel queue via `Logger.Log()`
>
> This benchmark measures only the write latency perceived by the caller thread. The actual filtering, formatting, and I/O overhead performed by the background worker is not included.
> It demonstrates the small boxing array allocations (~100 B range) caused by varying numbers of parameters.

| Method                                                          | Mean        | Error     | StdDev      | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |------------:|----------:|------------:|-------:|--------:|-------:|-------:|----------:|------------:|
| Log(): no properties, plain text message                        |    149.4 ns |   4.80 ns |    13.69 ns |   1.01 |    0.13 | 0.0188 | 0.0005 |      90 B |        1.00 |
| Log(): single property (params object?[1] allocation)          |    185.9 ns |   5.17 ns |    15.24 ns |   1.25 |    0.15 | 0.0260 | 0.0002 |     123 B |        1.37 |
| Log(): three properties (params object?[3] allocation)         |    189.3 ns |   4.67 ns |    13.78 ns |   1.28 |    0.15 | 0.0339 | 0.0010 |     161 B |        1.79 |
| Log(): five properties (params object?[5] allocation)          |    204.4 ns |   5.35 ns |    15.53 ns |   1.38 |    0.16 | 0.0420 | 0.0026 |     201 B |        2.23 |
| Log(): via ForContext wrapper (LoggerWrapper extra indirection) |    183.1 ns |   4.48 ns |    13.20 ns |   1.24 |    0.14 | 0.0257 | 0.0007 |     122 B |        1.36 |
| Log(): batch 100 (amortized cost per entry)                     | 19,773.6 ns | 637.78 ns | 1,870.49 ns | 133.45 |   17.58 | 3.0212 | 0.1526 |   14477 B |      160.86 |

---

## LogParserBenchmarks-report

> **Target Class**: `LogParser`
> **Measurement Target**: Throughput of `LogParser.ParseMessage(string)` for template string parsing
>
> Validates the cost of breaking down structured log templates (e.g. `User {Name} logged in`) into text tokens and property tokens.
> Alignment, custom format specifiers, and escape-induced splits are all leveled out under the caching mechanism (below ~15 ns).

| Method                                                                               | Mean         | Error      | StdDev     | Ratio    | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------------- |-------------:|-----------:|-----------:|---------:|--------:|-------:|-------:|----------:|------------:|
| Plain text, no placeholders (cache hit)                                              |     13.54 ns |   0.177 ns |   0.148 ns |     1.00 |    0.01 |      - |      - |         - |          NA |
| Single property template (cache hit)                                                 |     11.52 ns |   0.229 ns |   0.191 ns |     0.85 |    0.02 |      - |      - |         - |          NA |
| Three property template (cache hit)                                                  |     17.60 ns |   0.412 ns |   0.405 ns |     1.30 |    0.03 |      - |      - |         - |          NA |
| Complex template: alignment + formatting + destructure prefix (cache hit)            |     18.74 ns |   0.384 ns |   0.340 ns |     1.38 |    0.03 |      - |      - |         - |          NA |
| Template with {{ }} escape (cache hit)                                               |     14.05 ns |   0.265 ns |   0.221 ns |     1.04 |    0.02 |      - |      - |         - |          NA |
| Cache miss (approx., 6000 unique string pool, cache cleared after exceeding 4096)    | 26,437.30 ns | 407.861 ns | 361.558 ns | 1,952.35 |   32.74 | 0.1526 | 0.0610 |    1085 B |          NA |

---

## LogWriterBenchmarks-report

> **Target Classes**: `LogTextWriter`, `LogColorTextWriter`, `LogJsonWriter`, `WriterPool`
> **Measurement Target**: Throughput of `LogWriter.Render()` filling properties into templates and flushing the final output stream, plus the pooling benefit of `WriterPool`
>
> Directly demonstrates the write cost of different log formats (plain text, ANSI-colored text, JSON).
> Also shows that `WriterPool` pooling eliminates nearly all heap allocations caused by `StringBuilder` and context assembly.

| Method                                                              | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------------------------------------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| Text: plain text message (no properties)                            |   347.95 ns |  5.346 ns |  5.000 ns |  1.00 |    0.02 | 0.0234 |     112 B |        1.00 |
| Text: single property                                               |   425.43 ns |  8.352 ns |  8.937 ns |  1.22 |    0.03 | 0.0305 |     144 B |        1.29 |
| Text: four properties                                               |   582.14 ns | 11.430 ns | 11.737 ns |  1.67 |    0.04 | 0.0505 |     240 B |        2.14 |
| Text: alignment + formatting ({Count,8:D} {Percent:P1})             |   666.54 ns | 12.731 ns | 16.101 ns |  1.92 |    0.05 | 0.0525 |     248 B |        2.21 |
| Color: single property (with ANSI color escape codes)               |   455.94 ns |  7.754 ns |  7.253 ns |  1.31 |    0.03 | 0.0305 |     144 B |        1.29 |
| Color: four properties                                              |   649.25 ns |  8.767 ns |  8.201 ns |  1.87 |    0.03 | 0.0505 |     240 B |        2.14 |
| JSON: single property (with RenderedMessage + Propertys fields)     |   577.74 ns |  9.754 ns |  8.647 ns |  1.66 |    0.03 | 0.0315 |     152 B |        1.36 |
| JSON: four properties                                               | 1,148.39 ns | 10.269 ns |  9.103 ns |  3.30 |    0.05 | 0.0763 |     360 B |        3.21 |
| Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return() (pooled path)|    81.72 ns |  1.227 ns |  1.148 ns |  0.23 |    0.00 |      - |         - |        0.00 |
| Alloc: new LogTextWriter() (direct allocation, for pool comparison) |    20.17 ns |  0.429 ns |  0.495 ns |  0.06 |    0.00 | 0.0289 |     136 B |        1.21 |

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
