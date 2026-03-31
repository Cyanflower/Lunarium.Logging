```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean          | Error       | StdDev      | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------ |--------------:|------------:|------------:|---------:|--------:|-------:|----------:|------------:|
| &#39;无规则，级别通过 (缓存命中)&#39;                               |      8.805 ns |   0.1861 ns |   0.1650 ns |     1.00 |    0.03 |      - |         - |          NA |
| &#39;Include 规则，context 匹配通过 (缓存命中)&#39;                |      9.779 ns |   0.1214 ns |   0.1135 ns |     1.11 |    0.02 |      - |         - |          NA |
| &#39;Include 规则，context 不匹配被拒绝 (缓存命中)&#39;              |      8.855 ns |   0.1412 ns |   0.1252 ns |     1.01 |    0.02 |      - |         - |          NA |
| &#39;Exclude 规则，context 不在排除列表通过 (缓存命中)&#39;            |      8.794 ns |   0.2008 ns |   0.1677 ns |     1.00 |    0.03 |      - |         - |          NA |
| &#39;Exclude 规则，context 命中排除列表被拒绝 (缓存命中)&#39;           |      8.800 ns |   0.1709 ns |   0.1515 ns |     1.00 |    0.02 |      - |         - |          NA |
| &#39;无规则，缓存未命中 (近似，3000 个唯一 context，超出 2048 后缓存清空)&#39; | 26,093.348 ns | 397.4993 ns | 331.9300 ns | 2,964.37 |   64.54 | 0.0305 |     218 B |          NA |
