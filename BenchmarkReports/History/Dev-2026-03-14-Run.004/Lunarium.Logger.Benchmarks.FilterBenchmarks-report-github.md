```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |-----------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;无规则，级别通过 (缓存命中)&#39;                               |   8.922 ns | 0.1438 ns | 0.1201 ns |  1.00 |    0.02 |      - |      - |         - |          NA |
| &#39;Include 规则，context 匹配通过 (缓存命中)&#39;                |   8.883 ns | 0.1567 ns | 0.1389 ns |  1.00 |    0.02 |      - |      - |         - |          NA |
| &#39;Include 规则，context 不匹配被拒绝 (缓存命中)&#39;              |   8.901 ns | 0.1995 ns | 0.1666 ns |  1.00 |    0.02 |      - |      - |         - |          NA |
| &#39;Exclude 规则，context 不在排除列表通过 (缓存命中)&#39;            |   8.819 ns | 0.1010 ns | 0.0789 ns |  0.99 |    0.02 |      - |      - |         - |          NA |
| &#39;Exclude 规则，context 命中排除列表被拒绝 (缓存命中)&#39;           |   9.147 ns | 0.1769 ns | 0.1893 ns |  1.03 |    0.02 |      - |      - |         - |          NA |
| &#39;无规则，缓存未命中 (近似，3000 个唯一 context，超出 2048 后缓存清空)&#39; | 238.376 ns | 3.4696 ns | 3.0758 ns | 26.72 |    0.48 | 0.0463 | 0.0110 |     219 B |          NA |
