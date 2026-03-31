```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean        | Error     | StdDev      | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|----------:|------------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Log(): 无属性，纯文本消息&#39;                              |    188.3 ns |   5.19 ns |    15.31 ns |   1.01 |    0.12 | 0.0272 |     128 B |        1.00 |
| &#39;Log(): 单属性 (params object?[1] 分配)&#39;             |    199.1 ns |   6.30 ns |    18.27 ns |   1.06 |    0.13 | 0.0339 |     160 B |        1.25 |
| &#39;Log(): 三属性 (params object?[3] 分配)&#39;             |    190.4 ns |   7.89 ns |    22.88 ns |   1.02 |    0.15 | 0.0424 |     200 B |        1.56 |
| &#39;Log(): 五属性 (params object?[5] 分配)&#39;             |    201.3 ns |   5.39 ns |    15.71 ns |   1.08 |    0.12 | 0.0508 |     240 B |        1.88 |
| &#39;Log(): 通过 ForContext 包装器 (LoggerWrapper 额外调用)&#39; |    185.4 ns |   5.43 ns |    15.83 ns |   0.99 |    0.12 | 0.0339 |     160 B |        1.25 |
| &#39;Log(): 批量 100 条（测量批量分摊后每条的开销）&#39;                 | 20,117.3 ns | 652.20 ns | 1,881.75 ns | 107.57 |   13.37 | 3.9063 |   18400 B |      143.75 |
