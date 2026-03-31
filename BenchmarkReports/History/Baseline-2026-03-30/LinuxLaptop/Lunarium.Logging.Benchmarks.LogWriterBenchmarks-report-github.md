```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                                       | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------- |----------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Text: 纯文本消息（无属性）&#39;                                           | 328.91 ns |  4.522 ns |  4.009 ns |  1.00 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 单属性&#39;                                                  | 356.61 ns |  4.797 ns |  4.252 ns |  1.08 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 四属性&#39;                                                  | 446.02 ns |  4.715 ns |  4.180 ns |  1.36 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                  | 526.32 ns |  8.962 ns |  8.801 ns |  1.60 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39;  | 465.06 ns |  5.542 ns |  5.184 ns |  1.41 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 单属性（含 ANSI 颜色转义代码）&#39;                                  | 438.36 ns |  8.284 ns |  8.136 ns |  1.33 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 四属性&#39;                                                 | 605.66 ns |  7.488 ns |  6.253 ns |  1.84 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                 | 626.05 ns |  9.897 ns |  9.258 ns |  1.90 |    0.04 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39; | 591.34 ns |  9.297 ns |  8.242 ns |  1.80 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;JSON: 单属性（含 RenderedMessage + Propertys 字段）&#39;                | 506.76 ns |  5.780 ns |  5.407 ns |  1.54 |    0.02 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: 四属性&#39;                                                  | 759.57 ns | 12.741 ns | 11.918 ns |  2.31 |    0.04 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: Numeric/Formattable&#39;                                  | 772.94 ns | 14.916 ns | 15.960 ns |  2.35 |    0.05 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: {@Payload} 匿名类型（JsonSerializer.Serialize 回退路径）&#39;       | 979.06 ns | 14.622 ns | 12.210 ns |  2.98 |    0.05 | 0.1392 |      - |     656 B |       20.50 |
| &#39;JSON: {@Payload} IDestructured（预序列化字节，WriteRawValue 零分配路径）&#39; | 634.73 ns |  6.439 ns |  5.376 ns |  1.93 |    0.03 | 0.0134 |      - |      64 B |        2.00 |
| &#39;Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return()（池化路径）&#39;     |  77.23 ns |  0.649 ns |  0.575 ns |  0.23 |    0.00 |      - |      - |         - |        0.00 |
| &#39;Alloc: new LogTextWriter()（直接分配，用于对比池化收益）&#39;                  |  54.56 ns |  1.126 ns |  1.205 ns |  0.17 |    0.00 | 0.1105 | 0.0002 |     520 B |       16.25 |
