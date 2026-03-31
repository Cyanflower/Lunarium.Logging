```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean        | Error     | StdDev      | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|----------:|------------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Log(): 无属性，纯文本消息&#39;                              |    173.6 ns |   4.66 ns |    13.45 ns |   1.01 |    0.11 | 0.0241 | 0.0010 |     114 B |        1.00 |
| &#39;Log(): 单属性 (params object?[1] 分配)&#39;             |    190.7 ns |   5.00 ns |    14.59 ns |   1.10 |    0.12 | 0.0310 | 0.0029 |     149 B |        1.31 |
| &#39;Log(): 三属性 (params object?[3] 分配)&#39;             |    193.6 ns |   6.32 ns |    18.64 ns |   1.12 |    0.14 | 0.0393 |      - |     185 B |        1.62 |
| &#39;Log(): 五属性 (params object?[5] 分配)&#39;             |    200.1 ns |   5.44 ns |    15.78 ns |   1.16 |    0.13 | 0.0479 |      - |     226 B |        1.98 |
| &#39;Log(): 通过 ForContext 包装器 (LoggerWrapper 额外调用)&#39; |    200.9 ns |   4.16 ns |    11.86 ns |   1.16 |    0.11 | 0.0310 | 0.0005 |     147 B |        1.29 |
| &#39;Log(): 批量 100 条（测量批量分摊后每条的开销）&#39;                 | 19,467.3 ns | 579.81 ns | 1,691.32 ns | 112.80 |   13.08 | 3.6011 |      - |   16946 B |      148.65 |
