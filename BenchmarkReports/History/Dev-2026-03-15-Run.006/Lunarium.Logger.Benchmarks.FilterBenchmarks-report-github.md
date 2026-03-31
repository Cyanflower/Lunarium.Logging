```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                          | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |-----------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;无规则，级别通过 (缓存命中)&#39;                               |   8.918 ns | 0.1344 ns | 0.1258 ns |  1.00 |    0.02 |      - |      - |         - |          NA |
| &#39;Include 规则，context 匹配通过 (缓存命中)&#39;                |   8.778 ns | 0.0846 ns | 0.0706 ns |  0.98 |    0.02 |      - |      - |         - |          NA |
| &#39;Include 规则，context 不匹配被拒绝 (缓存命中)&#39;              |   8.828 ns | 0.1250 ns | 0.1108 ns |  0.99 |    0.02 |      - |      - |         - |          NA |
| &#39;Exclude 规则，context 不在排除列表通过 (缓存命中)&#39;            |   8.824 ns | 0.1381 ns | 0.1153 ns |  0.99 |    0.02 |      - |      - |         - |          NA |
| &#39;Exclude 规则，context 命中排除列表被拒绝 (缓存命中)&#39;           |   8.964 ns | 0.2127 ns | 0.2364 ns |  1.01 |    0.03 |      - |      - |         - |          NA |
| &#39;无规则，缓存未命中 (近似，3000 个唯一 context，超出 2048 后缓存清空)&#39; | 242.754 ns | 4.9006 ns | 4.8130 ns | 27.22 |    0.64 | 0.0458 | 0.0119 |     218 B |          NA |
