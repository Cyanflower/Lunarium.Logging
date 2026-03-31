```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |-----------:|----------:|----------:|-----------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;无规则，级别通过 (缓存命中)&#39;                               |   8.779 ns | 0.0646 ns | 0.0573 ns |   8.789 ns |  1.00 |    0.01 |      - |      - |         - |          NA |
| &#39;Include 规则，context 匹配通过 (缓存命中)&#39;                |   8.955 ns | 0.2097 ns | 0.2060 ns |   8.875 ns |  1.02 |    0.02 |      - |      - |         - |          NA |
| &#39;Include 规则，context 不匹配被拒绝 (缓存命中)&#39;              |   9.076 ns | 0.2020 ns | 0.4559 ns |   8.913 ns |  1.03 |    0.05 |      - |      - |         - |          NA |
| &#39;Exclude 规则，context 不在排除列表通过 (缓存命中)&#39;            |   8.820 ns | 0.1121 ns | 0.0994 ns |   8.797 ns |  1.00 |    0.01 |      - |      - |         - |          NA |
| &#39;Exclude 规则，context 命中排除列表被拒绝 (缓存命中)&#39;           |   8.922 ns | 0.1629 ns | 0.1524 ns |   8.875 ns |  1.02 |    0.02 |      - |      - |         - |          NA |
| &#39;无规则，缓存未命中 (近似，3000 个唯一 context，超出 2048 后缓存清空)&#39; | 240.677 ns | 2.8916 ns | 2.7048 ns | 240.105 ns | 27.42 |    0.35 | 0.0460 | 0.0126 |     218 B |          NA |
