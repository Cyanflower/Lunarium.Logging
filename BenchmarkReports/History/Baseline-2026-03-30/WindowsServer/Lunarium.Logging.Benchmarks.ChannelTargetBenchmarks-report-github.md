```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.20348.4171)
AMD EPYC 7542, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2


```
| Method                                                               | Mean        | Error     | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------------------------------------------------------- |------------:|----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;LogEntryChannelTarget: 零编码，原样传递 LogEntry 引用&#39;                        |    68.57 ns |  2.322 ns |   6.846 ns |  1.01 |    0.16 |      - |         - |          NA |
| &#39;ByteChannelTarget: 渲染 + byte[] 拷贝（ToArray，跳过 UTF-8→string 解码）&#39;      | 1,073.44 ns | 33.661 ns |  99.250 ns | 15.84 |    2.37 | 0.0153 |     136 B |          NA |
| &#39;StringChannelTarget: 渲染 + UTF-8→string 解码（Encoding.UTF8.GetString）&#39; | 1,261.69 ns | 34.605 ns | 102.033 ns | 18.61 |    2.65 | 0.0229 |     208 B |          NA |
