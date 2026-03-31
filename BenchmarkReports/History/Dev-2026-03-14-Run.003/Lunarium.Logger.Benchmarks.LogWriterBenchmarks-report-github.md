```

BenchmarkDotNet v0.14.0, Fedora Linux 42 (Workstation Edition)
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.226.5608), X64 RyuJIT AVX2


```
| Method                                                      | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------ |----------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Text: 纯文本消息（无属性）&#39;                                          | 316.12 ns |  5.679 ns |  5.034 ns |  1.00 |    0.02 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: 单属性&#39;                                                 | 399.63 ns |  6.981 ns |  6.188 ns |  1.26 |    0.03 | 0.0134 |      - |      64 B |        2.00 |
| &#39;Text: 四属性&#39;                                                 | 552.51 ns |  8.620 ns |  7.641 ns |  1.75 |    0.04 | 0.0229 |      - |     112 B |        3.50 |
| &#39;Text: 对齐 + 格式化 ({Count,8:D} {Percent:P1})&#39;                 | 523.07 ns |  8.533 ns |  8.381 ns |  1.66 |    0.04 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Text: Numeric/Formattable (int, DateTimeOffset, TimeSpan)&#39; | 443.41 ns |  8.847 ns |  9.466 ns |  1.40 |    0.04 | 0.0067 |      - |      32 B |        1.00 |
| &#39;Color: 单属性（含 ANSI 颜色转义代码）&#39;                                 | 462.37 ns |  6.985 ns |  6.192 ns |  1.46 |    0.03 | 0.0134 |      - |      64 B |        2.00 |
| &#39;Color: 四属性&#39;                                                | 692.29 ns | 11.703 ns | 10.374 ns |  2.19 |    0.05 | 0.0229 |      - |     112 B |        3.50 |
| &#39;JSON: 单属性（含 RenderedMessage + Propertys 字段）&#39;               | 515.19 ns |  8.680 ns |  7.248 ns |  1.63 |    0.03 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: 四属性&#39;                                                 | 944.65 ns | 15.268 ns | 18.750 ns |  2.99 |    0.07 | 0.0305 |      - |     144 B |        4.50 |
| &#39;JSON: Numeric/Formattable&#39;                                 | 785.40 ns | 12.851 ns | 12.021 ns |  2.49 |    0.05 | 0.0134 |      - |      64 B |        2.00 |
| &#39;JSON: Complex Object (@Destructure)&#39;                       | 962.69 ns | 18.236 ns | 17.058 ns |  3.05 |    0.07 | 0.1392 |      - |     656 B |       20.50 |
| &#39;Pool: WriterPool.Get&lt;LogTextWriter&gt;() + Return()（池化路径）&#39;    |  79.13 ns |  1.083 ns |  1.013 ns |  0.25 |    0.00 |      - |      - |         - |        0.00 |
| &#39;Alloc: new LogTextWriter()（直接分配，用于对比池化收益）&#39;                 |  50.76 ns |  1.059 ns |  1.378 ns |  0.16 |    0.00 | 0.1037 | 0.0002 |     488 B |       15.25 |
