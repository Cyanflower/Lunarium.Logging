# Lunarium.Logging — 注释规范

本文档记录项目源码注释的统一方案，作为补写/重写注释任务的参考依据。

---

## 总体原则

| 目标 | 语言 | 格式 |
|------|------|------|
| 库用户（IntelliSense / NuGet 文档） | 英文 | XML `///` |
| 内部维护者 | 中文 | 普通 `//` |

- **XML 注释**：只用于 `public` / `protected` 成员，供外部消费者阅读
- **中文 `//` 注释**：用于所有可见性层级，表达设计意图、约束、注意事项
- 两者内容**不重复**：中文 `//` 只写 XML 里没有、但对维护有价值的信息

---

## 按可见性分类

### `internal` 类型 / 成员

> 不写 XML，只写中文 `//`

```csharp
// 全局时间戳时区配置，所有 Logger 共享同一时区设置
// 通过 GlobalConfigurator 在 Build() 前一次性写入，之后只读
internal static class LogTimestampConfig
{
    // 仅在 Mode == Custom 时生效；默认值 Utc 是占位，不会被其他模式读取
    internal static TimeZoneInfo CustomTimeZone { get; private set; } = TimeZoneInfo.Utc;
}
```

注释覆盖目标：
- 类/枚举级别：这是什么、在整体架构中的职责
- 有非显而易见行为的属性/字段：默认值的含义、使用约束
- 方法：只在有不明显的副作用、前置条件、调用时机时写

### `public` / `protected` 成员

> 英文 XML + 按需中文 `//`

```csharp
// null 表示不覆盖 Target 的默认行为，由 Target 自行决定（与 false 语义不同）
/// <summary>
/// Output format override. <see langword="true"/> forces JSON output, <see langword="false"/> forces plain text.
/// <see langword="null"/> (default) leaves the target's own default in effect.
/// </summary>
public bool? ToJson { get; init; } = null;
```

中文 `//` 放在 XML 块**上方**，仅在以下情况添加：
- XML 无法覆盖的维护侧信息（为什么这样设计、改这里要注意什么）
- 与其他成员存在隐式依赖或约束关系
- 非显而易见的默认值语义

纯显而易见的成员只写 XML，不加中文 `//`：

```csharp
/// <summary>Whether this sink is active. Defaults to <see langword="true"/>.</summary>
public bool Enabled { get; init; } = true;
```

---

## XML 注释格式约定

### 基本结构

```csharp
/// <summary>
/// One or two sentences describing what this does.
/// </summary>
/// <remarks>
/// Additional behavioral details a consumer needs to know.
/// Use <remarks> for non-trivial constraints or interaction notes.
/// </remarks>
/// <param name="foo">Description of parameter.</param>
/// <returns>Description of return value.</returns>
/// <exception cref="ArgumentNullException"><paramref name="foo"/> is <see langword="null"/>.</exception>
```

### 常用标签

| 标签 | 用途 |
|------|------|
| `<see cref="X"/>` | 引用类型或成员 |
| `<see langword="null"/>` / `<see langword="true"/>` | 关键字高亮 |
| `<c>code</c>` | 行内代码片段（类名、参数值、格式字符串等） |
| `<para>` | `<remarks>` 内的段落分隔 |

### 枚举值

每个枚举成员都应有 `<summary>`（哪怕很短）：

```csharp
/// <summary>Unix timestamp in seconds.</summary>
Unix,
/// <summary>ISO 8601 extended format (e.g. <c>2026-01-01T12:00:00.000+08:00</c>).</summary>
ISO8601,
```

### `<remarks>` 的使用时机

- 有使用前提或约束（如"至少需要启用一种轮转策略"）
- 与其他类型有重要的行为关联
- 默认值需要解释（写在 `<summary>` 放不下时）

不要滥用 `<remarks>`，简单成员的约束直接写在 `<summary>` 末尾即可。

---

## 方法内部注释

只在以下情况写内联中文 `//`：

- **算法意图**：非显而易见的逻辑判断、状态机分支
- **约束说明**：为什么不能用更简单的写法（性能、线程安全、AOT 限制等）
- **魔法数字**：常量的来源和含义
- **引用外部规范**：环境变量名称、协议细节等

```csharp
// 对于日志输出场景，使用 UnsafeRelaxedJsonEscaping 是安全的：
// - 日志文件不会被嵌入到 HTML/JS 中执行
// - 允许所有 Unicode 字符（包括 Emoji、中文等）直接输出
// - 仅转义 JSON 必须转义的字符（" \ 控制字符）
if (EnableUnsafeRelaxedJsonEscaping)
    options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
```

不要注释显而易见的操作：

```csharp
// ❌ 不必要
_options = null; // 重置选项

// ✅ 有价值
_options = null; // 置 null 触发下次访问时重建，而非立即重建（懒加载）
```

---

## 行长与格式

- 中文 `//` 注释**以可读性为优先**，不强制单行
- 多个相关说明点用列表形式（`// -`）比长句更清晰
- XML `<summary>` 通常 1-2 句，超过 3 句考虑移入 `<remarks>`

---

## 已完成文件

| 目录 | 状态 |
|------|------|
| `src/Lunarium.Logging/Config/GlobalConfig/` | ✅ 完成 |
| `src/Lunarium.Logging/Config/Models/` | ✅ 完成 |
| `src/Lunarium.Logging/` 根目录文件 | ✅ 完成 |
| `src/Lunarium.Logging/Core/` | ✅ 完成 |
| `src/Lunarium.Logging/Models/` | ✅ 完成 |
| `src/Lunarium.Logging/Parser/` | ✅ 完成 |
| `src/Lunarium.Logging/Target/` | ✅ 完成（2026-03-30 补全漏写成员） |
| `src/Lunarium.Logging/Writer/` | ✅ 完成 |
| `src/Lunarium.Logging/Filter/` | ✅ 完成（已有良好注释，本轮确认） |
| `src/Lunarium.Logging/Internal/` | ✅ 完成（已有良好注释，本轮确认） |
| `src/Lunarium.Logging/Wrapper/` | ✅ 完成 |
| `src/Lunarium.Logging/InternalLoggerUtils/` | ✅ 完成 |
| `src/Lunarium.Logging/Utils/` | ✅ 完成 |
| `src/Lunarium.Logging.Hosting/` | ✅ 完成 |
| `src/Lunarium.Logging.Configuration/` | ✅ 完成 |
| `src/Lunarium.Logging.Http/` | ✅ 完成 |
