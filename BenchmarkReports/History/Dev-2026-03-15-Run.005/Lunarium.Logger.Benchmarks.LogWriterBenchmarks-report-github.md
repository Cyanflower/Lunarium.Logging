```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                                       | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------- |----------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Text: 纯文本消息（无属性）&#39;                                           | 315.69 ns |  3.226 ns |  2.694 ns |  1.00 |    0.01 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 单属性&#39;                                                  | 353.99 ns |  6.879 ns |  8.189 ns |  1.12 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 四属性&#39;                                                  | 471.33 ns |  8.706 ns |  8.144 ns |  1.49 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                  | 514.56 ns |  6.930 ns |  5.787 ns |  1.63 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39;  | 467.64 ns |  8.718 ns |  8.562 ns |  1.48 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 单属性（含 ANSI 颜色转义代码）&#39;                                  | 415.76 ns |  6.767 ns |  6.330 ns |  1.32 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 四属性&#39;                                                 | 575.56 ns | 11.179 ns | 13.729 ns |  1.82 |    0.05 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                 | 598.18 ns |  4.932 ns |  4.372 ns |  1.89 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39; | 563.54 ns |  8.236 ns |  6.430 ns |  1.79 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;JSON: 单属性（含 RenderedMessage + Propertys 字段）&#39;                | 469.73 ns |  8.258 ns |  7.320 ns |  1.49 |    0.03 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: 四属性&#39;                                                  | 728.76 ns | 14.526 ns | 13.587 ns |  2.31 |    0.05 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: Numeric/Formattable&#39;                                  | 725.78 ns |  8.259 ns |  7.321 ns |  2.30 |    0.03 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: Complex Object (@Destructure)&#39;                        | 951.55 ns |  9.432 ns |  7.364 ns |  3.01 |    0.03 | 0.1392 |      - |     656 B |       20.50 |
| &#39;Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return()（池化路径）&#39;     |  78.18 ns |  0.583 ns |  0.546 ns |  0.25 |    0.00 |      - |      - |         - |        0.00 |
| &#39;Alloc: new LogTextWriter()（直接分配，用于对比池化收益）&#39;                  |  55.33 ns |  1.124 ns |  1.051 ns |  0.18 |    0.00 | 0.1105 | 0.0002 |     520 B |       16.25 |
