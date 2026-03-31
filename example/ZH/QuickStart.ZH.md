# QuickStart — Lunarium.Logging 快速入门

> 完整代码版本：[RawCSharp/QuickStart.ZH.cs](RawCSharp/QuickStart.ZH.cs)

---

## 安装

```xml
<PackageReference Include="Lunarium.Logging" Version="*" />
```

---

## 第一步：创建 Logger

```csharp
// GlobalConfigurator.Configure() 是可选的；不调用时 Build() 自动应用默认值。
// Logger 应作为全局单例持有，应用启动时创建一次，通过 DI 或静态属性共享。
public static readonly ILogger Logger = new LoggerBuilder()
    .SetLoggerName("MyApp")   // 日志中显示的 Logger 名称（可选）
    .AddConsoleSink()      // 最简控制台 Sink
    .Build();
```

> 更多 Sink 类型和配置选项见 [SinkConfiguration.ZH.md](SinkConfiguration.ZH.md)。

---

## 第二步：写日志

五个级别从低到高：`Debug < Info < Warning < Error < Critical`

默认 `FilterConfig.LogMinLevel = Info`，`Debug` 会被过滤。

```csharp
Logger.Debug("调试信息，默认被 Info 级 Sink 过滤");
Logger.Info("服务启动完成，端口 {Port}", 8080);
Logger.Warning("内存使用率超过 {Percent}%，请关注", 85);
Logger.Error("数据库连接失败，重试第 {Attempt} 次", 3);
Logger.Critical("磁盘空间不足，系统即将停止");
```

---

## 第三步：记录异常

所有级别均支持 `Exception?` 参数重载，异常信息追加到日志末尾。

```csharp
try { /* ... */ }
catch (Exception ex)
{
    Logger.Error(ex, "处理订单 {OrderId} 时发生错误", 42);
}
```

---

## 第四步：消息模板语法速查

属性值通过 `params object?[]` 按顺序填充。

| 语法 | 说明 | 输出示例 |
|------|------|----------|
| `{Property}` | 基本替换（ToString） | `用户 1001 登录` |
| `{@Object}` | 解构：序列化为 JSON | `{"Id":42,"Amount":99.9}` |
| `{$Value}` | 字符串化：强制 ToString | `Active`（枚举名而非整数） |
| `{Value,10}` | 右对齐 10 位 | `[      INFO]` |
| `{Value,-10}` | 左对齐 10 位 | `[INFO      ]` |
| `{Value:F2}` | 格式字符串 | `1234.50` |
| `{Value,10:F2}` | 对齐 + 格式（顺序固定） | `[   1234.50]` |
| `{{ }}` | 字面量大括号转义 | `{不会被解析}` |
| `{Name}` with `null` | null 值 | `(null)` |

```csharp
Logger.Info("用户 {UserId} 登录", 1001);
Logger.Info("新订单 {@Order}", new { Id = 42, Amount = 99.9m });
Logger.Info("状态：{$Status}", MyEnum.Active);
Logger.Info("[{Level,8}] 消息", "INFO");
Logger.Info("金额：{Amount:F2} 元", 1234.5);
Logger.Info("字面量：{{不解析}}");
```

> 属性名规则：首字符字母或 `_`，后续可含字母/数字/下划线/点（`.` 后不能接数字或连续点）。

---

## 第五步：ForContext — 附加上下文前缀

```csharp
// 按字符串
var orderLogger = Logger.ForContext("Order.Service");
orderLogger.Info("处理订单 {OrderId}", 42);
// Context 字段：Order.Service

// 按类型（使用类名）
var payLogger = Logger.ForContext<PaymentService>();
payLogger.Info("扣款成功");
// Context 字段：PaymentService

// 多级嵌套（自动以 . 拼接，扁平化到根 Logger）
var child = Logger.ForContext("App").ForContext("Module");
child.Info("消息");
// Context 字段：App.Module
```

`ForContext()` 返回轻量级 `LoggerWrapper`，Context/ContextBytes 构造时一次性预计算，后续 Log 调用零额外分配。

---

## LoggerName 与 Context 的区别

| 字段 | 设置时机 | 变化方式 | 典型值 |
|------|----------|----------|--------|
| `LoggerName` | `Build()` 时 `.SetLoggerName()` | 固定不变 | `"Runtime"`（服务级） |
| `Context` | `ForContext()` 动态设置 | 可多级嵌套 | `"Payment.Processor"`（模块级） |

---

## 生命周期注意事项

- Logger 是进程内单例，同一 `LoggerName` 只能 `Build()` 一次
- `GlobalConfigurator.Configure()` 整个进程只能调用一次（配置锁保护）
- 应用关闭时调用 `logger.Dispose()` 确保 Channel 中剩余日志全部刷出
