```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                                               | Mean        | Error     | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------------------------------------------------------- |------------:|----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;LogEntryChannelTarget: 零编码，原样传递 LogEntry 引用&#39;                        |    45.83 ns |  0.936 ns |   2.074 ns |  1.00 |    0.06 |      - |         - |          NA |
| &#39;ByteChannelTarget: 渲染 + byte[] 拷贝（ToArray，跳过 UTF-8→string 解码）&#39;      |   915.00 ns | 40.064 ns | 118.130 ns | 20.00 |    2.72 | 0.0267 |     128 B |          NA |
| &#39;StringChannelTarget: 渲染 + UTF-8→string 解码（Encoding.UTF8.GetString）&#39; | 1,129.60 ns | 23.411 ns |  69.027 ns | 24.70 |    1.85 | 0.0401 |     192 B |          NA |
