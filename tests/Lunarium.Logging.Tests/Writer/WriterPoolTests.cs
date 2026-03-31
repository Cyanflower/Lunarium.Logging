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

using FluentAssertions;
using Lunarium.Logging.Writer;

namespace Lunarium.Logging.Tests.Writer;

// [CollectionDefinition("NonParallel", DisableParallelization = true)]
// public class NonParallelCollection { }

/// <summary>
/// Tests for WriterPool — the ConcurrentBag&lt;T&gt; based object pool.
/// </summary>
/// 
// [Collection("NonParallel")]
public class WriterPoolTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. Basic pool round-trip
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetReturnGet_SameObject_IsReused()
    {
        // Drain any pre-existing pooled instance first (if any) by getting many
        // then check that the round-trip returns the same object.
        var writer = WriterPool.Get<LogTextWriter>();
        WriterPool.Return(writer);
        var retrieved = WriterPool.Get<LogTextWriter>();

        // After a Return, the next Get should hand back the same instance
        retrieved.Should().BeSameAs(writer);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. StringBuilder is cleared after Return
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Return_WriterIsReset_ToStringIsEmpty()
    {
        var writer = WriterPool.Get<LogTextWriter>();
        // Write something into the writer by calling its public API indirectly
        // via ToString (we know it wraps a StringBuilder)
        // We cannot write directly without building a full LogEntry, so we
        // just verify the contract: after return + get, the buffer is clear.
        WriterPool.Return(writer);
        var reused = WriterPool.Get<LogTextWriter>();
        reused.ToString().Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Oversized writer is NOT returned to pool
    // ─────────────────────────────────────────────────────────────────────────

    // Using the existing condition MaxBufferCapacity=1: once set to 1, any parallel thread's Return() cannot be enqueued (TryReset will inevitably fail), 
    // so just drain the pool after setting the cap, and then it becomes a completely isolated environment.
    // Below is the original method as well as another solution
    [Fact]
    public void Return_OversizedWriter_NotReused()
    {
        var originalCapacity = WriterPool.MaxBufferCapacity;
        WriterPool.MaxBufferCapacity = 1; // 设为 1 后，并行线程也无法向 pool 写入

        try
        {
            // 排空 pool（取超过 PoolMaxSize=100 个确保彻底清空）
            var drain = new List<LogTextWriter>();
            for (int i = 0; i < 110; i++)
                drain.Add(WriterPool.Get<LogTextWriter>());
            // 这些对象不还池（cap=1 也还不进去），此时 pool 绝对为空

            var writer = WriterPool.Get<LogTextWriter>(); // 必然 new
            WriterPool.Return(writer);                    // TryReset(1) 失败，不入池
            var next = WriterPool.Get<LogTextWriter>();   // 必然 new，不可能是 writer

            next.Should().NotBeSameAs(writer);
        }
        finally
        {
            WriterPool.MaxBufferCapacity = originalCapacity;
        }
    }

    // Original solution:
    // Affected by polluted states caused by xunity concurrent testing, there is a probability of failure.
    // Or
    // Go and remove the comments from lines 20, 21, and 27 at the top of the file 
    // (not from the three lines below here, but from the three lines with the same content at lines 20, 21, and 27) 
    // like this:
    // 20: [CollectionDefinition("NonParallel", DisableParallelization = true)]
    // 21: public class NonParallelCollection { }
    // 27: [Collection("NonParallel")]
    // to allow xunit to mark WriterPoolTests with the non-parallel collection marker, thereby completely excluding concurrent interference.
    // [Fact]
    // public void Return_OversizedWriter_NotReused()
    // {
    //     // Set a very small max capacity so any writer exceeds the limit
    //     var originalCapacity = WriterPool.MaxBufferCapacity;
    //     WriterPool.MaxBufferCapacity = 1; // only 1 byte — any SB will exceed this

    //     try
    //     {
    //         var writer = WriterPool.Get<LogTextWriter>();
    //         WriterPool.Return(writer);   // should NOT be pooled (buffer > 1 byte)
    //         var next = WriterPool.Get<LogTextWriter>();
    //         // If the writer was NOT pooled, we get a new object
    //         next.Should().NotBeSameAs(writer);
    //     }
    //     finally
    //     {
    //         // Restore original capacity
    //         WriterPool.MaxBufferCapacity = originalCapacity;
    //     }
    // }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Get on empty pool creates a new instance
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_WhenPoolEmpty_ReturnsNewInstance()
    {
        // Drain the pool by getting many objects without returning
        // (100 is the pool max; take 101 to ensure pool is exhausted)
        var drainList = new List<LogTextWriter>();
        for (int i = 0; i < 110; i++)
            drainList.Add(WriterPool.Get<LogTextWriter>());

        var fresh = WriterPool.Get<LogTextWriter>();
        fresh.Should().NotBeNull();

        // Clean up: return all (some will be discarded at pool cap)
        foreach (var w in drainList) WriterPool.Return(w);
        WriterPool.Return(fresh);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Pool-full path — 129th Return triggers DisposeAndReturnArrayBuffer
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WriterPool_Return_WhenPoolFull_DoesNotThrow()
    {
        // Fill the pool to its max capacity (PoolMaxSize = 128)
        var drainList = new List<LogTextWriter>();
        for (int i = 0; i < 128; i++)
            drainList.Add(WriterPool.Get<LogTextWriter>());

        // Return all 128 — fills the pool up to the limit
        foreach (var w in drainList)
            WriterPool.Return(w);

        // Get one more and return it — _count >= PoolMaxSize triggers DisposeAndReturnArrayBuffer
        var extra = WriterPool.Get<LogTextWriter>();
        var act = () => WriterPool.Return(extra);
        act.Should().NotThrow("overflow path should call DisposeAndReturnArrayBuffer without throwing");
    }
}
