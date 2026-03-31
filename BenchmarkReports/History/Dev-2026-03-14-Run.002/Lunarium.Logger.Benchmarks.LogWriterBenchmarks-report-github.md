```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                                      | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------ |------------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Text: 纯文本消息（无属性）&#39;                                          |   317.54 ns |  4.519 ns |  4.227 ns |  1.00 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 单属性&#39;                                                 |   404.33 ns |  7.925 ns | 10.023 ns |  1.27 |    0.03 | 0.0134 |      - |      64 B |        2.00 |
| &#39;Text: 四属性&#39;                                                 |   573.94 ns | 11.124 ns | 10.925 ns |  1.81 |    0.04 | 0.0229 |      - |     112 B |        3.50 |
| &#39;Text: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                 |   511.25 ns |  7.141 ns |  6.330 ns |  1.61 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39; |   451.50 ns |  5.959 ns |  5.574 ns |  1.42 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 单属性（含 ANSI 颜色转义代码）&#39;                                 |   466.27 ns |  6.147 ns |  5.750 ns |  1.47 |    0.03 | 0.0134 |      - |      64 B |        2.00 |
| &#39;Color: 四属性&#39;                                                |   680.53 ns |  9.420 ns |  7.866 ns |  2.14 |    0.04 | 0.0229 |      - |     112 B |        3.50 |
| &#39;JSON: 单属性（含 RenderedMessage + Propertys 字段）&#39;               | 1,162.28 ns | 22.379 ns | 26.640 ns |  3.66 |    0.09 | 2.7542 | 0.0477 |   12961 B |      405.03 |
| &#39;JSON: 四属性&#39;                                                 | 1,612.18 ns | 31.817 ns | 29.762 ns |  5.08 |    0.11 | 2.7695 | 0.0439 |   13041 B |      407.53 |
| &#39;JSON: Numeric/Formattable&#39;                                 | 1,432.98 ns | 16.976 ns | 15.049 ns |  4.51 |    0.07 | 2.7542 | 0.0477 |   12961 B |      405.03 |
| &#39;JSON: Complex Object (@Destructure)&#39;                       |          NA |        NA |        NA |     ? |       ? |     NA |     NA |        NA |           ? |
| &#39;Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return()（池化路径）&#39;    |    77.15 ns |  1.465 ns |  1.224 ns |  0.24 |    0.00 |      - |      - |         - |        0.00 |
| &#39;Alloc: new LogTextWriter()（直接分配，用于对比池化收益）&#39;                 |    55.03 ns |  1.131 ns |  1.058 ns |  0.17 |    0.00 | 0.1105 | 0.0002 |     520 B |       16.25 |

Benchmarks with issues:
  LogWriterBenchmarks.'JSON: Complex Object (@Destructure)': DefaultJob
