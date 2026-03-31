```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                                               | Mean        | Error     | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------------------------------------------------------- |------------:|----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;LogEntryChannelTarget: 零编码，原样传递 LogEntry 引用&#39;                        |    50.30 ns |  1.005 ns |   1.117 ns |  1.00 |    0.03 |      - |         - |          NA |
| &#39;ByteChannelTarget: 渲染 + byte[] 拷贝（ToArray，跳过 UTF-8→string 解码）&#39;      |   992.23 ns | 35.287 ns | 104.044 ns | 19.73 |    2.10 | 0.0286 |     136 B |          NA |
| &#39;StringChannelTarget: 渲染 + UTF-8→string 解码（Encoding.UTF8.GetString）&#39; | 1,065.78 ns | 42.002 ns | 123.843 ns | 21.20 |    2.49 | 0.0420 |     208 B |          NA |
