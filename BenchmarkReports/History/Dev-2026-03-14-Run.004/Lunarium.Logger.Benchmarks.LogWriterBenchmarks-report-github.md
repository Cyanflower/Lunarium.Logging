```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                                      | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------ |------------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Text: 纯文本消息（无属性）&#39;                                          |   319.22 ns |  4.879 ns |  4.326 ns |  1.00 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 单属性&#39;                                                 |   407.04 ns |  7.885 ns |  7.375 ns |  1.28 |    0.03 | 0.0134 |      - |      64 B |        2.00 |
| &#39;Text: 四属性&#39;                                                 |   563.02 ns | 10.691 ns | 10.000 ns |  1.76 |    0.04 | 0.0229 |      - |     112 B |        3.50 |
| &#39;Text: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                 |   517.07 ns |  9.716 ns |  9.089 ns |  1.62 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39; |   456.17 ns |  6.116 ns |  5.721 ns |  1.43 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 单属性（含 ANSI 颜色转义代码）&#39;                                 |   469.50 ns |  9.415 ns |  9.669 ns |  1.47 |    0.04 | 0.0134 |      - |      64 B |        2.00 |
| &#39;Color: 四属性&#39;                                                |   702.30 ns | 12.925 ns | 12.090 ns |  2.20 |    0.05 | 0.0229 |      - |     112 B |        3.50 |
| &#39;JSON: 单属性（含 RenderedMessage + Propertys 字段）&#39;               |   539.82 ns | 10.793 ns | 13.650 ns |  1.69 |    0.05 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: 四属性&#39;                                                 |   947.62 ns | 10.892 ns |  9.095 ns |  2.97 |    0.05 | 0.0305 |      - |     144 B |        4.50 |
| &#39;JSON: Numeric/Formattable&#39;                                 |   787.05 ns | 15.727 ns | 19.890 ns |  2.47 |    0.07 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: Complex Object (@Destructure)&#39;                       | 1,016.48 ns | 14.899 ns | 12.441 ns |  3.18 |    0.06 | 0.1392 |      - |     656 B |       20.50 |
| &#39;Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return()（池化路径）&#39;    |    76.68 ns |  0.755 ns |  0.630 ns |  0.24 |    0.00 |      - |      - |         - |        0.00 |
| &#39;Alloc: new LogTextWriter()（直接分配，用于对比池化收益）&#39;                 |    54.83 ns |  0.862 ns |  0.923 ns |  0.17 |    0.00 | 0.1105 | 0.0002 |     520 B |       16.25 |
