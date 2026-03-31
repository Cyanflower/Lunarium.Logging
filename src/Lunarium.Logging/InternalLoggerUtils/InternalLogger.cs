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

using System.Text;

namespace Lunarium.Logging.InternalLoggerUtils;

// SelfLog原则：InternalLogger 自身不能抛出异常，不能导致调用展全，否则会隐藏真实问题
// 同时输出到 stderr 和日志文件，手动调用频率应极低（仅在异常路径触发）
internal static class InternalLogger
{
    private static readonly object _fileLock = new object();

    #region 公开方法
    // 不同参数组合的快捷入口，全部委托给 ErrorHandler
    internal static void Error(string message) => ErrorHandler(message, null);
    internal static void Error(Exception exception) => ErrorHandler("", exception);
    internal static void Error(Exception exception, string message) => ErrorHandler(message, exception);
    internal static void Error(string message, Exception exception) => ErrorHandler(message, exception);

    #endregion

    #region 内部方法实现

    private static void ErrorHandler(string message, Exception? exception = null)
    {
        var now = DateTimeOffset.Now;
        string formatted;
        if (exception is null)
        {
            formatted = $"[{now:yyyy-MM-dd HH:mm:ss.fff zzz}] [LunariumLogger.InternalError] {message}";
        }
        else
        {
            formatted = $"[{now:yyyy-MM-dd HH:mm:ss.fff zzz}] [LunariumLogger.InternalError] {message}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
        }

        ConsoleHandler(formatted);
        FileHandler(formatted);
    }

    // ConsoleHandler 采用颜色区分内部错误和应用日志，输出到 stderr
    // 保存和恢复原始颜色避免影响其他输出
    private static void ConsoleHandler(string errorMsg)
    {
        lock (_fileLock)
        {
            var originalBg = Console.BackgroundColor;
            var originalFg = Console.ForegroundColor;

            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Error.WriteLine(errorMsg); // 写入标准错误流更合适
            // Console.ResetColor();

            // 恢复原始颜色，避免影响程序的其他部分
            Console.BackgroundColor = originalBg;
            Console.ForegroundColor = originalFg;
        }
    }

    private static void FileHandler(string errorMsg)
    {
        try
        {
            // 构造文件名: LunariumLogger-internal-20251101.log
            string dateStamp = DateTimeOffset.Now.ToString("yyyyMMdd");
            string fileName = $"LunariumLogger-internal-{dateStamp}.log";

            // 获取程序基目录
            string baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrEmpty(baseDirectory))
            {
                // 如果基目录为空, 则无法继续, 直接放弃
                return;
            }

            string filePath = Path.Combine(baseDirectory, fileName);

            // 加锁，确保线程安全
            lock (_fileLock)
            {
                // File.AppendAllText 会自动创建、打开、写入并关闭文件，非常方便
                File.AppendAllText(filePath, errorMsg, Encoding.UTF8);
            }
        }
        catch
        {
            // 写入文件失败则放弃, 不抛出任何异常
            // 这是 SelfLog 的核心原则: 不能因为记录内部错误而导致程序崩溃
        }
    }

    #endregion
}