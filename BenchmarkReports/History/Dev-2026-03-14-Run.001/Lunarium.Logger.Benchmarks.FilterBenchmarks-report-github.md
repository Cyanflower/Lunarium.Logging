```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean          | Error       | StdDev      | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------ |--------------:|------------:|------------:|---------:|--------:|-------:|----------:|------------:|
| &#39;无规则，级别通过 (缓存命中)&#39;                               |      8.893 ns |   0.1487 ns |   0.1319 ns |     1.00 |    0.02 |      - |         - |          NA |
| &#39;Include 规则，context 匹配通过 (缓存命中)&#39;                |      8.754 ns |   0.0996 ns |   0.0883 ns |     0.98 |    0.02 |      - |         - |          NA |
| &#39;Include 规则，context 不匹配被拒绝 (缓存命中)&#39;              |      8.677 ns |   0.0800 ns |   0.0748 ns |     0.98 |    0.02 |      - |         - |          NA |
| &#39;Exclude 规则，context 不在排除列表通过 (缓存命中)&#39;            |      8.759 ns |   0.1312 ns |   0.1163 ns |     0.99 |    0.02 |      - |         - |          NA |
| &#39;Exclude 规则，context 命中排除列表被拒绝 (缓存命中)&#39;           |      8.741 ns |   0.1037 ns |   0.0866 ns |     0.98 |    0.02 |      - |         - |          NA |
| &#39;无规则，缓存未命中 (近似，3000 个唯一 context，超出 2048 后缓存清空)&#39; | 26,013.800 ns | 504.3028 ns | 471.7252 ns | 2,925.68 |   66.09 | 0.0305 |     219 B |          NA |
