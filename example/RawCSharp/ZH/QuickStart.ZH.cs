// Copyright 2026 Cyanflower
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// ============================================================
//  本文件是 Lunarium.Logging 的快速入门示例，仅供参考，不参与编译。
//  目标：5 分钟内让第一行日志跑起来。
//  如需完整 Sink 配置，参见 SinkConfiguration.ZH.cs。
// ============================================================

using Lunarium.Logging.Target;
using Lunarium.Logging.Configuration;
using Lunarium.Logging.Extensions;
using Microsoft.Extensions.Hosting;

namespace Lunarium.Logging;

public static class QuickStartExample
{
    // ================================================================
    //  第一步：创建 Logger
    //
    //  • GlobalConfigurator.Configure() 是可选的；不调用时 Build() 自动应用默认值。
    //  • LoggerBuilder 可多次 Build()，每次返回独立 Logger 实例。
    //  • Logger 应作为全局单例持有，应用启动时创建一次，其后通过 DI 或静态属性共享。
    // ================================================================
    public static readonly ILogger Logger = new LoggerBuilder()
        .SetLoggerName("MyApp")          // 日志中显示的 Logger 名称（可选，默认空字符串）
        .AddConsoleSink()             // 最简控制台 Sink，无其他参数时使用全部默认值
        .Build();

    // ================================================================
    //  第二步：写日志
    //
    //  五个级别从低到高：Debug < Info < Warning < Error < Critical
    //  默认 FilterConfig.LogMinLevel = Info，Debug 会被过滤。
    // ================================================================
    public static void WriteBasicLogs()
    {
        Logger.Debug("调试信息，默认被 Info 级 Sink 过滤");
        Logger.Info("服务启动完成，端口 {Port}", 8080);
        Logger.Warning("内存使用率超过 {Percent}%，请关注", 85);
        Logger.Error("数据库连接失败，重试第 {Attempt} 次", 3);
        Logger.Critical("磁盘空间不足，系统即将停止");
    }

    // ================================================================
    //  第三步：记录异常
    //
    //  所有级别均支持 Exception? 参数重载，异常信息会追加到日志末尾。
    // ================================================================
    public static void WriteExceptionLogs()
    {
        try
        {
            // ...
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "处理订单 {OrderId} 时发生错误", 42);
            // 输出示例：
            // [ERROR] 处理订单 42 时发生错误
            // System.InvalidOperationException: ...
            //   at ...
        }
    }

    // ================================================================
    //  第四步：消息模板语法速查
    //
    //  属性值通过 params object?[] propertyValues 按顺序填充。
    //  属性名规则：首字符字母或 _，后续可含字母/数字/下划线/点（.后不能接数字）。
    // ================================================================
    public static void MessageTemplateSyntax()
    {
        // ── 基本属性替换 ───────────────────────────────────────────────────────
        Logger.Info("用户 {UserId} 登录", 1001);
        // 输出：用户 1001 登录

        // ── 解构（{@}）：将对象序列化为 JSON ──────────────────────────────────
        var order = new { Id = 42, Amount = 99.9m };
        Logger.Info("新订单 {@Order}", order);
        // 输出：新订单 {"Id":42,"Amount":99.9}

        // ── 字符串化（{$}）：强制调用 ToString() ──────────────────────────────
        Logger.Info("状态：{$Status}", SomeEnum.Active);
        // 输出：状态：Active（而非枚举整数值）

        // ── 对齐（正数右对齐，负数左对齐） ────────────────────────────────────
        Logger.Info("[{Level,8}] 消息", "INFO");
        // 输出：[    INFO] 消息

        Logger.Info("[{Level,-8}] 消息", "INFO");
        // 输出：[INFO    ] 消息

        // ── 格式字符串（标准 .NET 格式说明符） ────────────────────────────────
        Logger.Info("金额：{Amount:F2} 元", 1234.5);
        // 输出：金额：1234.50 元

        // ── 对齐 + 格式（顺序固定：对齐在前，格式在后） ───────────────────────
        Logger.Info("[{Amount,10:F2}]", 9.9);
        // 输出：[      9.90]

        // ── 大括号转义 ─────────────────────────────────────────────────────────
        Logger.Info("字面量大括号：{{不会被解析}}");
        // 输出：字面量大括号：{不会被解析}

        // ── null 值 ───────────────────────────────────────────────────────────
        string? name = null;
        Logger.Info("姓名：{Name}", name);
        // 输出：姓名：(null)
    }

    // ================================================================
    //  第五步：ForContext — 为日志附加上下文前缀
    //
    //  • ForContext() 返回一个轻量级 LoggerWrapper，不创建新 Logger 实例。
    //  • 多级 ForContext 路径以 . 拼接：A → ForContext("B") → Context = "A.B"
    //  • 构造时预计算 Context 字节，后续 Log 调用零额外分配。
    // ================================================================
    public static void ForContextExample()
    {
        // 按字符串指定
        var orderLogger = Logger.ForContext("Order.Service");
        orderLogger.Info("处理订单 {OrderId}", 42);
        // 输出中 Context 为：Order.Service

        // 按类型（使用类名作为 Context 字符串）
        var payLogger = Logger.ForContext<PaymentService>();
        payLogger.Info("扣款成功");
        // 输出中 Context 为：PaymentService

        // 多级嵌套（扁平化，一次解包到根 Logger）
        var rootCtx  = Logger.ForContext("App");
        var childCtx = rootCtx.ForContext("Module");
        childCtx.Info("消息");
        // 输出中 Context 为：App.Module
    }

    // ================================================================
    //  附：LoggerName 与 Context 的区别
    //
    //  LoggerName  ─ 标识 Logger 实例本身（构建时通过 .SetLoggerName() 设置，固定不变）
    //  Context     ─ 标识当前调用来源（通过 ForContext() 动态变化，可多级嵌套）
    //
    //  典型用法：
    //    LoggerName = "Runtime"（整个服务的 Logger 名）
    //    Context    = "Payment.Processor"（当前处理模块）
    // ================================================================

    private enum SomeEnum { Active, Inactive }
    private class PaymentService { }
}