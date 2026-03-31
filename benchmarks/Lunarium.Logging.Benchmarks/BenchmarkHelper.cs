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

using Lunarium.Logging.Models;
using Lunarium.Logging.Target;

namespace Lunarium.Logging.Benchmarks;

/// <summary>
/// 不输出任何内容的 NullTarget。
/// 用于隔离测量 Logger 管道开销（Channel 写入、过滤、分发），排除 I/O 噪声。
/// </summary>
internal sealed class NullTarget : ILogTarget
{
    public void Emit(LogEntry entry) { }
    public void Dispose() { }
}

/// <summary>
/// Benchmark 公共辅助工具。
/// </summary>
internal static class BenchmarkHelper
{
    /// <summary>
    /// 确保全局配置已初始化。
    /// GlobalConfigurator 只能配置一次（进程级），所有 Benchmark 类共享同一配置。
    /// 在每个 [GlobalSetup] 方法的第一行调用即可。
    /// </summary>
    public static void EnsureGlobalConfig()
        => GlobalConfigurator.ApplyDefaultIfNotConfigured();
}
