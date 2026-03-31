```

BenchmarkDotNet v0.14.0, Ubuntu 24.04 LTS (Noble Numbat)
AMD EPYC 7542, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.104
  [Host]     : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2


```
| Method                                                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------------------------------------------------------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;LogEntryChannelTarget: 零编码，原样传递 LogEntry 引用&#39;                        |    83.51 ns |  4.789 ns |  13.43 ns |  1.02 |    0.22 |      - |         - |          NA |
| &#39;ByteChannelTarget: 渲染 + byte[] 拷贝（ToArray，跳过 UTF-8→string 解码）&#39;      | 1,110.11 ns | 45.660 ns | 134.63 ns | 13.60 |    2.59 | 0.0153 |     136 B |          NA |
| &#39;StringChannelTarget: 渲染 + UTF-8→string 解码（Encoding.UTF8.GetString）&#39; | 1,164.52 ns | 48.385 ns | 142.66 ns | 14.27 |    2.72 | 0.0229 |     208 B |          NA |
