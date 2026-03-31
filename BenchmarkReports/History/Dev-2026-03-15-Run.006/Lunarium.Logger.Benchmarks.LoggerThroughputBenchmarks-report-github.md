```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean        | Error     | StdDev      | Median      | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|----------:|------------:|------------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Log(): 无属性，纯文本消息&#39;                              |    186.4 ns |   4.08 ns |    11.85 ns |    185.8 ns |   1.00 |    0.09 | 0.0238 |      - |     113 B |        1.00 |
| &#39;Log(): 单属性 (params object?[1] 分配)&#39;             |    192.4 ns |   4.67 ns |    13.63 ns |    192.9 ns |   1.04 |    0.10 | 0.0308 | 0.0002 |     146 B |        1.29 |
| &#39;Log(): 三属性 (params object?[3] 分配)&#39;             |    193.9 ns |   5.96 ns |    17.49 ns |    191.9 ns |   1.04 |    0.11 | 0.0398 |      - |     187 B |        1.65 |
| &#39;Log(): 五属性 (params object?[5] 分配)&#39;             |    212.3 ns |   6.00 ns |    17.69 ns |    211.4 ns |   1.14 |    0.12 | 0.0482 |      - |     227 B |        2.01 |
| &#39;Log(): 通过 ForContext 包装器 (LoggerWrapper 额外调用)&#39; |    198.7 ns |   5.37 ns |    15.41 ns |    197.7 ns |   1.07 |    0.11 | 0.0310 |      - |     146 B |        1.29 |
| &#39;Log(): 批量 100 条（测量批量分摊后每条的开销）&#39;                 | 20,618.2 ns | 806.59 ns | 2,378.24 ns | 19,901.0 ns | 111.03 |   14.54 | 3.6316 |      - |   17122 B |      151.52 |
