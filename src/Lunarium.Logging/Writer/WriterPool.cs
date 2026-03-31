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

using System.Collections.Concurrent;

using Lunarium.Logging.Writer;

namespace Lunarium.Logging.Writer;

// WriterPool 为每个 LogWriter 子类维护一个独立的 Pool<T>
// 使用注册为键 'where T : LogWriter, new()' 可直接构造新对象，无需工厂注册
internal static class WriterPool
{
    internal static int MaxBufferCapacity { get; set; } = 32 * 1024;

    internal static T Get<T>() where T : LogWriter, new()
    {
        return Pool<T>.Get();
    }

    internal static void Return<T>(T writer) where T : LogWriter, new()
    {
        Pool<T>.Return(writer);
    }

    private static class Pool<T> where T : LogWriter, new()
    {
        // Writer 对象池
        private static readonly ConcurrentBag<T> _pool = new();
        // 池内对象上限
        private const int PoolMaxSize = 128;
        // 独立原子计数器, 避免 ConcurrentBag.Count 触发全局锁 (FreezeBag)
        private static int _count = 0;

        // 尝试获取一个池内对象, 池内为空则返回一个新对象
        internal static T Get()
        {
            // 尝试获取池内对象
            if (_pool.TryTake(out var writer))
            {
                Interlocked.Decrement(ref _count);
                writer.MarkAsActive();
                return writer;
            }
            // 池为空则返回新对象
            return new T();
        }

        internal static void Return(T writer)
        {
            // 池内对象数量是否到达限制
            if (Volatile.Read(ref _count) >= PoolMaxSize)
            {
                // 直接返回并清理, 不回池内
                writer.DisposeAndReturnArrayBuffer();
                return;
            }
            // 对象大小是否超限
            if (writer.TryReset(MaxBufferCapacity))
            {
                // Increment 先于 Add: 使 _count 对其他线程的 Return() 更保守,
                // 减少短暂超出 PoolMaxSize 的幅度
                Interlocked.Increment(ref _count);
                _pool.Add(writer);
            }
            else
            {
                // 直接清理, 不回池内
                writer.DisposeAndReturnArrayBuffer();
            }
        }
    }
}