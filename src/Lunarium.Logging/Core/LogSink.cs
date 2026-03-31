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

using Lunarium.Logging.Filter;
using Lunarium.Logging.Target;

namespace Lunarium.Logging.Core;

// 封装单条日志从过滤到输出的完整链路：Enabled 检查 → LoggerFilter → 延迟解析 → ILogTarget.Emit
// 没有 Sink 名称时自动生成随机 GUID，仅用于调试和热更新路由
/// <summary>
/// Binds an <see cref="ILogTarget"/> to its associated <see cref="LoggerFilter"/>.
/// A single <see cref="Logger"/> holds a list of <c>Sink</c>s; each processes log entries
/// independently with its own filter configuration and enabled state.
/// </summary>
internal sealed class Sink : IDisposable
{
    internal string Name { get; }
    // false 时 Emit 直接返回，跳过过滤和输出；支持运行时关闭/开启
    internal bool Enabled { get; set; } = true;
    internal ILogTarget Target { get; }
    internal LoggerFilter LoggerFilter { get; }

    internal Sink(ILogTarget target, FilterConfig? filterConfig = null, string? name = null)
    {
        Target = target;

        if (filterConfig is null)
        {
            LoggerFilter = new LoggerFilter(new FilterConfig());
        }
        else
        {
            LoggerFilter = new LoggerFilter(filterConfig);
        }

        // 名称为空时自动生成 GUID，不影响日志输出，仅用于热更新路由匹配
        if (string.IsNullOrEmpty(name))
        {
            Name = Guid.NewGuid().ToString();
        }
        else
        {
            Name = name;
        }
    }

    internal Sink(ILogTarget target, string? name = null)
    {
        Target = target;
        LoggerFilter = new LoggerFilter(new FilterConfig());

        if (string.IsNullOrEmpty(name))
        {
            Name = Guid.NewGuid().ToString();
        }
        else
        {
            Name = name;
        }
    }

    internal void Emit(LogEntry logEntry)
    {
        if (!Enabled) return;

        if (LoggerFilter.ShouldEmit(logEntry))
        {
            logEntry.ParseMessage();
            Target.Emit(logEntry);
        }
    }

    // UpdateSinkConfig 读取 ISinkConfig.Enabled / FilterConfig / TextOutput，对 Target 上的 TextOutputIncludeConfig 进行并发更新
    internal void UpdateSinkConfig(ISinkConfig newConfig)
    {
        Enabled = newConfig.Enabled;

        if (newConfig.FilterConfig is null)
        {
            LoggerFilter.UpdateConfig(new FilterConfig());
        }
        else
        {
            LoggerFilter.UpdateConfig(newConfig.FilterConfig);
        }

        if (Target is ITextTarget textTarget)
        {
            if (newConfig.TextOutput is TextOutputIncludeConfig textTargetConfig)
            {
                textTarget.UpdateTextOutputIncludeConfig(textTargetConfig);
            }
            else
            {
                textTarget.UpdateTextOutputIncludeConfig(new TextOutputIncludeConfig());
            }
        }
    }

    internal void EnableSink()
    {
        Enabled = true;
    }
    
    internal void DisableSink()
    {
        Enabled = false;
    }

    // Dispose 只释放 Target；不从 _sinks 列表移除，不触发 Enabled 变更
    // 调用方应先确保不再对该 Sink 调用 Emit()（通常已 DisableSink()）
    public void Dispose()
    {
        Target.Dispose();
    }
}
