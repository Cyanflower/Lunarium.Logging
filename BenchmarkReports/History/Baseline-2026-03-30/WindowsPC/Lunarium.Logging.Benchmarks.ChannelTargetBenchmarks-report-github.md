```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8037)
Intel(R) Core(TM) i7-7700 CPU @ 3.60GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2


```
| Method                                                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------------------------------------------------------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;LogEntryChannelTarget: 零编码，原样传递 LogEntry 引用&#39;                        |    43.01 ns |  0.889 ns |  1.092 ns |  1.00 |    0.04 |      - |         - |          NA |
| &#39;ByteChannelTarget: 渲染 + byte[] 拷贝（ToArray，跳过 UTF-8→string 解码）&#39;      |   932.17 ns | 17.308 ns | 24.823 ns | 21.69 |    0.78 | 0.0324 |     136 B |          NA |
| &#39;StringChannelTarget: 渲染 + UTF-8→string 解码（Encoding.UTF8.GetString）&#39; | 1,017.45 ns | 20.242 ns | 40.425 ns | 23.67 |    1.10 | 0.0458 |     208 B |          NA |
