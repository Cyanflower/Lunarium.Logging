```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean        | Error     | StdDev      | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|----------:|------------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Log(): 无属性，纯文本消息&#39;                              |    187.3 ns |   5.04 ns |    14.77 ns |   1.01 |    0.11 | 0.0243 | 0.0012 |     116 B |        1.00 |
| &#39;Log(): 单属性 (params object?[1] 分配)&#39;             |    190.1 ns |   7.63 ns |    22.12 ns |   1.02 |    0.14 | 0.0319 |      - |     151 B |        1.30 |
| &#39;Log(): 三属性 (params object?[3] 分配)&#39;             |    199.4 ns |   6.17 ns |    18.00 ns |   1.07 |    0.13 | 0.0386 | 0.0026 |     185 B |        1.59 |
| &#39;Log(): 五属性 (params object?[5] 分配)&#39;             |    208.7 ns |   6.13 ns |    17.89 ns |   1.12 |    0.13 | 0.0486 |      - |     228 B |        1.97 |
| &#39;Log(): 通过 ForContext 包装器 (LoggerWrapper 额外调用)&#39; |    207.6 ns |   5.28 ns |    15.39 ns |   1.11 |    0.12 | 0.0300 | 0.0045 |     148 B |        1.28 |
| &#39;Log(): 批量 100 条（测量批量分摊后每条的开销）&#39;                 | 18,692.9 ns | 738.67 ns | 2,119.38 ns | 100.39 |   13.71 | 3.6621 |      - |   17219 B |      148.44 |
