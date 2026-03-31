```

BenchmarkDotNet v0.14.0, Ubuntu 24.04 LTS (Noble Numbat)
AMD EPYC 7542, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.104
  [Host]     : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2


```
| Method                                                       | Mean        | Error     | StdDev    | Median      | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------- |------------:|----------:|----------:|------------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Text: 纯文本消息（无属性）&#39;                                           |   341.43 ns |  5.272 ns |  4.674 ns |   339.94 ns |  1.00 |    0.02 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Text: 单属性&#39;                                                  |   388.20 ns |  2.758 ns |  2.580 ns |   387.57 ns |  1.14 |    0.02 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Text: 四属性&#39;                                                  |   515.13 ns |  9.841 ns | 10.938 ns |   510.26 ns |  1.51 |    0.04 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Text: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                  |   583.08 ns | 10.188 ns |  9.031 ns |   579.38 ns |  1.71 |    0.03 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Text: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39;  |   502.11 ns |  7.591 ns |  7.100 ns |   499.83 ns |  1.47 |    0.03 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Color: 单属性（含 ANSI 颜色转义代码）&#39;                                  |   471.37 ns |  3.996 ns |  3.542 ns |   470.71 ns |  1.38 |    0.02 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Color: 四属性&#39;                                                 |   650.75 ns |  5.131 ns |  4.800 ns |   650.84 ns |  1.91 |    0.03 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Color: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                 |   667.52 ns |  4.106 ns |  3.841 ns |   667.23 ns |  1.96 |    0.03 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Color: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39; |   632.76 ns |  1.115 ns |  0.931 ns |   632.86 ns |  1.85 |    0.02 | 0.0038 |      - |      32 B |        1.00 |
| &#39;JSON: 单属性（含 RenderedMessage + Propertys 字段）&#39;                |   531.01 ns |  1.817 ns |  1.517 ns |   530.89 ns |  1.56 |    0.02 | 0.0076 |      - |      64 B |        2.00 |
| &#39;JSON: 四属性&#39;                                                  |   922.87 ns |  7.640 ns |  6.773 ns |   921.64 ns |  2.70 |    0.04 | 0.0076 |      - |      64 B |        2.00 |
| &#39;JSON: Numeric/Formattable&#39;                                  |   837.61 ns | 15.456 ns | 12.907 ns |   831.03 ns |  2.45 |    0.05 | 0.0076 |      - |      64 B |        2.00 |
| &#39;JSON: {@Payload} 匿名类型（JsonSerializer.Serialize 回退路径）&#39;       | 1,147.71 ns | 22.747 ns | 20.164 ns | 1,148.77 ns |  3.36 |    0.07 | 0.0782 |      - |     656 B |       20.50 |
| &#39;JSON: {@Payload} IDestructured（预序列化字节，WriteRawValue 零分配路径）&#39; |   697.90 ns |  4.183 ns |  3.709 ns |   696.62 ns |  2.04 |    0.03 | 0.0076 |      - |      64 B |        2.00 |
| &#39;Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return()（池化路径）&#39;     |    53.31 ns |  0.147 ns |  0.123 ns |    53.26 ns |  0.16 |    0.00 |      - |      - |         - |        0.00 |
| &#39;Alloc: new LogTextWriter()（直接分配，用于对比池化收益）&#39;                  |    64.20 ns |  1.346 ns |  3.839 ns |    62.34 ns |  0.19 |    0.01 | 0.0621 | 0.0001 |     520 B |       16.25 |
