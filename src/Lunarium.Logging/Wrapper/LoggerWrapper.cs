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

namespace Lunarium.Logging.Wrapper;

// 日志包装器（装饰器），用于为一个已存在的 ILogger 实例添加固定的上下文信息。
// 每次通过此包装器记录日志时，它都会自动附加这个上下文。
internal sealed class LoggerWrapper : ILogger, IContextProvider
{
    // 引用被包装的日志实例或下一层包装器
    private readonly ILogger _logger;
    // 此包装器要附加的上下文
    private readonly string _context;
    private readonly byte[] _contextBytes;

    // 初始化一个新的 LoggerWrapper 实例。
    // logger：要包装的 ILogger 实例。
    // context：要与此包装器关联的上下文信息。
    internal LoggerWrapper(ILogger logger, string context)
    {
        // 扁平化：始终持有 root logger，避免链式调用触发中间层 slow path
        if (logger is LoggerWrapper wrapper)
        {
            _logger = wrapper._logger; // 直接持有最底层的 Logger 实例，避免多层包装导致的性能问题，同时实现扁平化
            _context = $"{wrapper.GetContext()}.{context}";
        }
        else
        {
            _logger = logger;
            _context = context;
        }

        _contextBytes = Encoding.UTF8.GetBytes(_context);
    }

    public string GetContext()
    {
        return _context;
    }

    public ReadOnlyMemory<byte> GetContextSpan()
    {
        return _contextBytes;
    }

    // 记录一条日志。将自己的上下文与传入的上下文组合，然后传递给被包装的日志记录器。
    public void Log(LogLevel level, Exception? ex = null, string message = "", string context = "", ReadOnlyMemory<byte> contextBytes = default, string scope = "", params object?[] propertyValues)
    {
        // 快速路径：使用者没有提供额外的即时 context
        if (string.IsNullOrEmpty(context))
        {
            // 直接透传固化好的字符串引用和字节引用，实现零分配调用
            _logger.Log(
                level: level,
                ex: ex,
                message: message,
                context: _context,
                contextBytes: _contextBytes,
                scope: scope,
                propertyValues: propertyValues);
            return;
        }

        // 慢速路径：提供了额外的即时上下文，分配不可避免
        // 此时传递拼接后的字符串，但 bytes 传 default (除非调用方自己带了 bytes)
        // 这样底层的 LogWriter 如果发现 bytes 为空，会 fallback 到对新字符串进行编码
        var tempContext = $"{_context}.{context}";
        _logger.Log(
            level: level,
            ex: ex,
            message: message,
            context: tempContext,
            contextBytes: Encoding.UTF8.GetBytes(tempContext), 
            scope: scope,
            propertyValues: propertyValues);
    }

    // 包装器不应该直接穿透 Dispose 直接销毁掉根日志实例，会导致以该根日志实例为基础的所有日志系统关闭
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}