```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8037)
Intel(R) Core(TM) i7-7700 CPU @ 3.60GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2


```
| Method                                                       | Mean        | Error     | StdDev    | Median      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------------- |------------:|----------:|----------:|------------:|------:|--------:|-------:|----------:|------------:|
| &#39;Text: 纯文本消息（无属性）&#39;                                           |   344.68 ns |  6.912 ns | 16.426 ns |   345.01 ns |  1.00 |    0.07 | 0.0076 |      32 B |        1.00 |
| &#39;Text: 单属性&#39;                                                  |   372.13 ns |  7.106 ns | 13.347 ns |   373.88 ns |  1.08 |    0.06 | 0.0076 |      32 B |        1.00 |
| &#39;Text: 四属性&#39;                                                  |   486.41 ns |  9.640 ns | 20.333 ns |   485.49 ns |  1.41 |    0.09 | 0.0076 |      32 B |        1.00 |
| &#39;Text: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                  |   574.87 ns | 11.008 ns | 28.611 ns |   573.10 ns |  1.67 |    0.11 | 0.0076 |      32 B |        1.00 |
| &#39;Text: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39;  |   509.44 ns | 10.220 ns | 25.641 ns |   508.84 ns |  1.48 |    0.10 | 0.0076 |      32 B |        1.00 |
| &#39;Color: 单属性（含 ANSI 颜色转义代码）&#39;                                  |   456.58 ns |  9.832 ns | 27.731 ns |   452.70 ns |  1.33 |    0.10 | 0.0076 |      32 B |        1.00 |
| &#39;Color: 四属性&#39;                                                 |   634.36 ns | 14.626 ns | 40.771 ns |   636.78 ns |  1.84 |    0.15 | 0.0076 |      32 B |        1.00 |
| &#39;Color: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                 |   647.78 ns | 12.908 ns | 37.036 ns |   644.57 ns |  1.88 |    0.14 | 0.0076 |      32 B |        1.00 |
| &#39;Color: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39; |   659.78 ns | 30.490 ns | 86.989 ns |   631.84 ns |  1.92 |    0.27 | 0.0076 |      32 B |        1.00 |
| &#39;JSON: 单属性（含 RenderedMessage + Propertys 字段）&#39;                |   577.30 ns | 20.587 ns | 58.401 ns |   562.98 ns |  1.68 |    0.19 | 0.0153 |      64 B |        2.00 |
| &#39;JSON: 四属性&#39;                                                  |   817.03 ns | 19.785 ns | 56.766 ns |   807.25 ns |  2.38 |    0.20 | 0.0153 |      64 B |        2.00 |
| &#39;JSON: Numeric/Formattable&#39;                                  |   817.75 ns | 16.547 ns | 46.398 ns |   812.81 ns |  2.38 |    0.17 | 0.0153 |      64 B |        2.00 |
| &#39;JSON: {@Payload} 匿名类型（JsonSerializer.Serialize 回退路径）&#39;       | 1,038.59 ns | 25.428 ns | 71.302 ns | 1,027.36 ns |  3.02 |    0.25 | 0.1564 |     656 B |       20.50 |
| &#39;JSON: {@Payload} IDestructured（预序列化字节，WriteRawValue 零分配路径）&#39; |   646.40 ns | 12.924 ns | 31.214 ns |   644.04 ns |  1.88 |    0.13 | 0.0153 |      64 B |        2.00 |
| &#39;Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return()（池化路径）&#39;     |    78.32 ns |  1.647 ns |  2.023 ns |    78.02 ns |  0.23 |    0.01 |      - |         - |        0.00 |
| &#39;Alloc: new LogTextWriter()（直接分配，用于对比池化收益）&#39;                  |    59.02 ns |  2.834 ns |  8.222 ns |    56.93 ns |  0.17 |    0.03 | 0.1243 |     520 B |       16.25 |
