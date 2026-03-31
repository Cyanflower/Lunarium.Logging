```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.20348.4171)
AMD EPYC 7542, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2


```
| Method                                                       | Mean        | Error     | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------- |------------:|----------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Text: 纯文本消息（无属性）&#39;                                           |   356.96 ns |  2.194 ns | 1.945 ns |  1.00 |    0.01 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Text: 单属性&#39;                                                  |   402.28 ns |  1.914 ns | 1.790 ns |  1.13 |    0.01 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Text: 四属性&#39;                                                  |   483.38 ns |  3.511 ns | 3.112 ns |  1.35 |    0.01 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Text: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                  |   575.95 ns |  9.000 ns | 8.418 ns |  1.61 |    0.02 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Text: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39;  |   508.18 ns |  5.321 ns | 4.977 ns |  1.42 |    0.02 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Color: 单属性（含 ANSI 颜色转义代码）&#39;                                  |   463.61 ns |  3.163 ns | 2.641 ns |  1.30 |    0.01 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Color: 四属性&#39;                                                 |   628.85 ns |  6.719 ns | 6.285 ns |  1.76 |    0.02 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Color: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                 |   650.84 ns |  2.852 ns | 2.528 ns |  1.82 |    0.01 | 0.0038 |      - |      32 B |        1.00 |
| &#39;Color: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39; |   611.48 ns |  5.321 ns | 4.978 ns |  1.71 |    0.02 | 0.0038 |      - |      32 B |        1.00 |
| &#39;JSON: 单属性（含 RenderedMessage + Propertys 字段）&#39;                |   524.79 ns |  3.300 ns | 3.087 ns |  1.47 |    0.01 | 0.0076 |      - |      64 B |        2.00 |
| &#39;JSON: 四属性&#39;                                                  |   809.55 ns |  7.480 ns | 6.997 ns |  2.27 |    0.02 | 0.0076 |      - |      64 B |        2.00 |
| &#39;JSON: Numeric/Formattable&#39;                                  |   845.09 ns |  9.057 ns | 8.472 ns |  2.37 |    0.03 | 0.0076 |      - |      64 B |        2.00 |
| &#39;JSON: {@Payload} 匿名类型（JsonSerializer.Serialize 回退路径）&#39;       | 1,035.94 ns | 10.358 ns | 8.649 ns |  2.90 |    0.03 | 0.0782 |      - |     656 B |       20.50 |
| &#39;JSON: {@Payload} IDestructured（预序列化字节，WriteRawValue 零分配路径）&#39; |   681.01 ns |  5.920 ns | 5.538 ns |  1.91 |    0.02 | 0.0076 |      - |      64 B |        2.00 |
| &#39;Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return()（池化路径）&#39;     |    51.41 ns |  0.109 ns | 0.102 ns |  0.14 |    0.00 |      - |      - |         - |        0.00 |
| &#39;Alloc: new LogTextWriter()（直接分配，用于对比池化收益）&#39;                  |    39.31 ns |  0.449 ns | 0.420 ns |  0.11 |    0.00 | 0.0622 | 0.0001 |     520 B |       16.25 |
