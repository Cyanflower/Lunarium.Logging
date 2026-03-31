```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean        | Error     | StdDev      | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|----------:|------------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Log(): 无属性，纯文本消息&#39;                              |    189.2 ns |   4.63 ns |    13.66 ns |   1.01 |    0.10 | 0.0241 | 0.0007 |     114 B |        1.00 |
| &#39;Log(): 单属性 (params object?[1] 分配)&#39;             |    197.6 ns |   5.31 ns |    15.57 ns |   1.05 |    0.11 | 0.0312 |      - |     147 B |        1.29 |
| &#39;Log(): 三属性 (params object?[3] 分配)&#39;             |    194.2 ns |   5.55 ns |    16.36 ns |   1.03 |    0.11 | 0.0393 | 0.0002 |     185 B |        1.62 |
| &#39;Log(): 五属性 (params object?[5] 分配)&#39;             |    200.2 ns |   5.53 ns |    16.14 ns |   1.06 |    0.11 | 0.0479 |      - |     226 B |        1.98 |
| &#39;Log(): 通过 ForContext 包装器 (LoggerWrapper 额外调用)&#39; |    197.4 ns |   5.32 ns |    15.28 ns |   1.05 |    0.11 | 0.0312 | 0.0002 |     148 B |        1.30 |
| &#39;Log(): 批量 100 条（测量批量分摊后每条的开销）&#39;                 | 21,019.8 ns | 563.08 ns | 1,633.61 ns | 111.64 |   11.70 | 3.6011 |      - |   16971 B |      148.87 |
