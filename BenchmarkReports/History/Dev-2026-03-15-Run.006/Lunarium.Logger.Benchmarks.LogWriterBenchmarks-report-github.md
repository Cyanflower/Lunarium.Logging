```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                                       | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------- |----------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Text: 纯文本消息（无属性）&#39;                                           | 320.77 ns |  5.418 ns |  5.068 ns |  1.00 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 单属性&#39;                                                  | 354.22 ns |  5.387 ns |  4.776 ns |  1.10 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 四属性&#39;                                                  | 476.89 ns |  9.326 ns | 11.102 ns |  1.49 |    0.04 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                  | 516.29 ns |  9.877 ns |  9.701 ns |  1.61 |    0.04 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39;  | 466.54 ns |  6.161 ns |  5.461 ns |  1.45 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 单属性（含 ANSI 颜色转义代码）&#39;                                  | 418.80 ns |  8.148 ns |  9.057 ns |  1.31 |    0.03 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 四属性&#39;                                                 | 572.01 ns | 11.007 ns |  9.757 ns |  1.78 |    0.04 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                 | 597.57 ns | 10.100 ns |  9.447 ns |  1.86 |    0.04 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39; | 567.30 ns | 11.090 ns | 15.180 ns |  1.77 |    0.05 | 0.0067 |      - |      32 B |        1.00 |
| &#39;JSON: 单属性（含 RenderedMessage + Propertys 字段）&#39;                | 470.95 ns |  9.208 ns | 14.061 ns |  1.47 |    0.05 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: 四属性&#39;                                                  | 720.05 ns | 13.334 ns | 11.820 ns |  2.25 |    0.05 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: Numeric/Formattable&#39;                                  | 750.30 ns | 14.974 ns | 17.244 ns |  2.34 |    0.06 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: {@Payload} 匿名类型（JsonSerializer.Serialize 回退路径）&#39;       | 953.12 ns | 16.366 ns | 14.508 ns |  2.97 |    0.06 | 0.1392 |      - |     656 B |       20.50 |
| &#39;JSON: {@Payload} IDestructured（预序列化字节，WriteRawValue 零分配路径）&#39; | 601.97 ns |  6.021 ns |  5.028 ns |  1.88 |    0.03 | 0.0134 |      - |      64 B |        2.00 |
| &#39;Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return()（池化路径）&#39;     |  77.33 ns |  0.570 ns |  0.476 ns |  0.24 |    0.00 |      - |      - |         - |        0.00 |
| &#39;Alloc: new LogTextWriter()（直接分配，用于对比池化收益）&#39;                  |  56.85 ns |  1.173 ns |  1.397 ns |  0.18 |    0.01 | 0.1105 | 0.0001 |     520 B |       16.25 |
