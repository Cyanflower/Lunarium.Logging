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

namespace Lunarium.Logging.Config.GlobalConfig;

// 全局配置一次性锁，确保 GlobalConfigurator 只能在 Build() 前配置一次
// Build() 时若未调用过 Configure()，会自动应用默认值并锁定；之后不可重置（进程级单例）
internal static class GlobalConfigLock
{
    // true 后拒绝再次配置，GlobalConfigurator.Apply() 和 Build() 自动调用 CompleteConfig()
    internal static bool Configured = false;

    internal static void CompleteConfig()
    {
        Configured = true;
    }
}
