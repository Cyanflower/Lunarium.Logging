```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |-----------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;无规则，级别通过 (缓存命中)&#39;                               |   8.663 ns | 0.1166 ns | 0.1090 ns |  1.00 |    0.02 |      - |      - |         - |          NA |
| &#39;Include 规则，context 匹配通过 (缓存命中)&#39;                |   8.889 ns | 0.1528 ns | 0.1429 ns |  1.03 |    0.02 |      - |      - |         - |          NA |
| &#39;Include 规则，context 不匹配被拒绝 (缓存命中)&#39;              |   8.921 ns | 0.2003 ns | 0.1968 ns |  1.03 |    0.03 |      - |      - |         - |          NA |
| &#39;Exclude 规则，context 不在排除列表通过 (缓存命中)&#39;            |   8.359 ns | 0.1995 ns | 0.1866 ns |  0.97 |    0.02 |      - |      - |         - |          NA |
| &#39;Exclude 规则，context 命中排除列表被拒绝 (缓存命中)&#39;           |   8.734 ns | 0.1475 ns | 0.1232 ns |  1.01 |    0.02 |      - |      - |         - |          NA |
| &#39;无规则，缓存未命中 (近似，3000 个唯一 context，超出 2048 后缓存清空)&#39; | 237.106 ns | 4.5413 ns | 4.0258 ns | 27.37 |    0.56 | 0.0458 | 0.0124 |     218 B |          NA |
