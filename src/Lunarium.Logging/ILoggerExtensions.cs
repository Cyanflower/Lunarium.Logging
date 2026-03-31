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

using Lunarium.Logging.Wrapper;

namespace Lunarium.Logging;

/// <summary>
/// Extension methods for <see cref="ILogger"/> that create context-scoped logger wrappers.
/// </summary>
public static class ILoggerExtensions
{
    /// <summary>
    /// Returns a wrapper that automatically attaches <paramref name="context"/> to every log entry.
    /// Useful for distinguishing log output from different modules or classes.
    /// </summary>
    /// <param name="logger">The logger to wrap.</param>
    /// <param name="context">The context string to attach to all log entries.</param>
    /// <returns>A new <see cref="ILogger"/> with the specified context applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public static ILogger ForContext(this ILogger logger, string context)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        // 返回一个包装器，该包装器持有原始 logger 和新的上下文
        return new LoggerWrapper(logger, context);
    }

    /// <summary>
    /// Convenience overload of <see cref="ForContext(ILogger, string)"/> that uses the full name of <typeparamref name="T"/> as the context.
    /// </summary>
    /// <typeparam name="T">The type whose full name is used as the context. Typically the calling class.</typeparam>
    /// <param name="logger">The logger to wrap.</param>
    /// <returns>A new <see cref="ILogger"/> with the type name applied as context.</returns>
    public static ILogger ForContext<T>(this ILogger logger)
    {
        return logger.ForContext(typeof(T).FullName ?? typeof(T).Name);
    }
}

