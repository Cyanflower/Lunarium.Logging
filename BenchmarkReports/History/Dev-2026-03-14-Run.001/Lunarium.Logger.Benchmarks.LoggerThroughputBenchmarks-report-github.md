```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean        | Error     | StdDev      | Ratio  | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|----------:|------------:|-------:|--------:|-------:|-------:|-------:|----------:|------------:|
| &#39;Log(): 无属性，纯文本消息&#39;                              |    152.9 ns |   4.54 ns |    13.18 ns |   1.01 |    0.12 | 0.0186 |      - |      - |      88 B |        1.00 |
| &#39;Log(): 单属性 (params object?[1] 分配)&#39;             |    186.7 ns |   4.49 ns |    13.24 ns |   1.23 |    0.14 | 0.0255 |      - |      - |     121 B |        1.38 |
| &#39;Log(): 三属性 (params object?[3] 分配)&#39;             |    184.9 ns |   5.04 ns |    14.63 ns |   1.22 |    0.14 | 0.0341 |      - |      - |     161 B |        1.83 |
| &#39;Log(): 五属性 (params object?[5] 分配)&#39;             |    191.7 ns |   4.72 ns |    13.76 ns |   1.26 |    0.14 | 0.0429 |      - |      - |     202 B |        2.30 |
| &#39;Log(): 通过 ForContext 包装器 (LoggerWrapper 额外调用)&#39; |    164.3 ns |   4.12 ns |    11.94 ns |   1.08 |    0.12 | 0.0229 | 0.0079 | 0.0005 |     120 B |        1.36 |
| &#39;Log(): 批量 100 条（测量批量分摊后每条的开销）&#39;                 | 18,255.5 ns | 587.66 ns | 1,723.50 ns | 120.23 |   15.25 | 3.0212 | 0.1526 |      - |   14515 B |      164.94 |
