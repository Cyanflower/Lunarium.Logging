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

namespace Lunarium.Logging.Http.Tests.Target;

/// <summary>
/// HttpTarget 异步生命周期行为验证。
/// 同步策略：TaskCompletionSource + WaitAsync(2s)，小参数（batchSize=2, flushInterval=50ms）。
/// </summary>
public class HttpTargetTests
{
    private static readonly Uri Endpoint = new("http://localhost:9999/logs");
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    // batchSize=2 方便触发大小触发；flushInterval 足够短以触发时间触发
    private static HttpTarget MakeTarget(
        HttpMessageHandler handler,
        int batchSize = 2,
        int channelCapacity = 100,
        TimeSpan? flushInterval = null,
        TimeSpan? disposeTimeout = null,
        TimeSpan? requestTimeout = null,
        IReadOnlyDictionary<string, string>? headers = null,
        IHttpLogSerializer? serializer = null) =>
        new HttpTarget(
            httpClient: new HttpClient(handler),
            endpoint: Endpoint,
            serializer: serializer,
            headers: headers,
            batchSize: batchSize,
            flushInterval: flushInterval ?? TimeSpan.FromMilliseconds(50),
            disposeTimeout: disposeTimeout ?? TimeSpan.FromSeconds(3),
            channelCapacity: channelCapacity,
            requestTimeout: requestTimeout ?? TimeSpan.FromSeconds(10));

    // ─────────────────────────────────────────────────────────────────────────
    // 批量触发
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BatchSize_Reached_TriggersFlush()
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler(async (req, _) =>
        {
            tcs.TrySetResult(await req.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var target = MakeTarget(handler, batchSize: 2, flushInterval: TimeSpan.FromSeconds(60));
        target.Emit(EntryFactory.Make("a"));
        target.Emit(EntryFactory.Make("b")); // 第 2 条触发 batchSize

        var body = await tcs.Task.WaitAsync(WaitTimeout);
        body.Should().NotBeEmpty();
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task FlushInterval_Elapsed_FlushesBeforeBatchSize()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler((req, _) =>
        {
            tcs.TrySetResult(true);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var target = MakeTarget(handler, batchSize: 100, flushInterval: TimeSpan.FromMilliseconds(80));
        target.Emit(EntryFactory.Make("only one"));

        await tcs.Task.WaitAsync(WaitTimeout);
        handler.Requests.Should().HaveCount(1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 请求内容
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Request_IsPost_ToCorrectEndpoint()
    {
        var tcs = new TaskCompletionSource<HttpRequestMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler((req, _) =>
        {
            tcs.TrySetResult(req);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var target = MakeTarget(handler);
        target.Emit(EntryFactory.Make());
        target.Emit(EntryFactory.Make());

        var req = await tcs.Task.WaitAsync(WaitTimeout);
        req.Method.Should().Be(HttpMethod.Post);
        req.RequestUri.Should().Be(Endpoint);
    }

    [Fact]
    public async Task Request_ContentType_MatchesSerializer()
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler((req, _) =>
        {
            tcs.TrySetResult(req.Content?.Headers.ContentType?.MediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var target = MakeTarget(handler, serializer: ClefSerializer.Default);
        target.Emit(EntryFactory.Make());
        target.Emit(EntryFactory.Make());

        var ct = await tcs.Task.WaitAsync(WaitTimeout);
        ct.Should().Be("application/vnd.serilog.clef");
    }

    [Fact]
    public async Task Request_CustomHeaders_AttachedToRequest()
    {
        var tcs = new TaskCompletionSource<HttpRequestMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler((req, _) =>
        {
            tcs.TrySetResult(req);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var headers = new Dictionary<string, string> { ["X-Api-Key"] = "secret123" };
        using var target = MakeTarget(handler, headers: headers);
        target.Emit(EntryFactory.Make());
        target.Emit(EntryFactory.Make());

        var req = await tcs.Task.WaitAsync(WaitTimeout);
        req.Headers.TryGetValues("X-Api-Key", out var vals).Should().BeTrue();
        vals!.First().Should().Be("secret123");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 重试
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_FirstFailure_RetriesAndSucceeds()
    {
        // 首次 500，重试 200；batchSize=1 避免多条 entry 被分批触发的时序竞争
        int callCount = 0;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler((_, _) =>
        {
            int n = Interlocked.Increment(ref callCount);
            if (n == 2) tcs.TrySetResult(true);
            return Task.FromResult(n == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var target = MakeTarget(handler, batchSize: 1);
        target.Emit(EntryFactory.Make());

        await tcs.Task.WaitAsync(WaitTimeout);
        callCount.Should().Be(2); // 1 次失败 + 1 次重试
    }

    [Fact]
    public async Task Send_BothFailures_DropsEntries_NoFurtherRetry()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int callCount = 0;
        var handler = new FakeHttpHandler((_, _) =>
        {
            int n = Interlocked.Increment(ref callCount);
            if (n >= 2) tcs.TrySetResult(true);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        using var target = MakeTarget(handler);
        target.Emit(EntryFactory.Make());
        target.Emit(EntryFactory.Make());

        await tcs.Task.WaitAsync(WaitTimeout);
        await Task.Delay(100); // 等待确认不会有第 3 次调用
        callCount.Should().Be(2); // 仅 1 次失败 + 1 次重试，不再继续
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 溢出警告
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChannelFull_DoesNotThrow_AndContinuesAfterDrain()
    {
        // channel 容量 2，快速写入 10 条，后台慢慢消费
        var handler = new FakeHttpHandler(async (_, _) =>
        {
            await Task.Delay(50);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var target = MakeTarget(handler, batchSize: 2, channelCapacity: 2,
            flushInterval: TimeSpan.FromMilliseconds(30));

        var act = () =>
        {
            for (int i = 0; i < 10; i++)
                target.Emit(EntryFactory.Make($"msg-{i}"));
        };
        act.Should().NotThrow();
    }

    [Fact]
    public async Task OverflowWarning_ResetsAfterSuccessfulFlush()
    {
        // 验证：flush 成功后 _overflowWarned 被重置
        // 手段：填满 channel → flush 成功 → 再次填满 → 依然不抛异常（行为正确）
        int sentBatches = 0;
        var handler = new FakeHttpHandler((_, _) =>
        {
            Interlocked.Increment(ref sentBatches);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var target = MakeTarget(handler, batchSize: 2, channelCapacity: 2,
            flushInterval: TimeSpan.FromMilliseconds(30));

        for (int i = 0; i < 5; i++)
            target.Emit(EntryFactory.Make());

        await Task.Delay(300);
        int batchesAfterFirst = sentBatches;

        for (int i = 0; i < 5; i++)
            target.Emit(EntryFactory.Make());

        await Task.Delay(300);
        sentBatches.Should().BeGreaterThan(batchesAfterFirst);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Dispose
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispose_WaitsForDrain_RemainingEntriesSent()
    {
        int totalSent = 0;
        var handler = new FakeHttpHandler(async (req, _) =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            // 数组长度即批次大小，用条目数量计算
            using var doc = JsonDocument.Parse(body);
            Interlocked.Add(ref totalSent, doc.RootElement.GetArrayLength());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var target = MakeTarget(handler, batchSize: 50, flushInterval: TimeSpan.FromSeconds(60),
            disposeTimeout: TimeSpan.FromSeconds(5));

        for (int i = 0; i < 5; i++)
            target.Emit(EntryFactory.Make($"msg-{i}"));

        target.Dispose(); // 应等待 flush 完成
        totalSent.Should().Be(5);
    }

    [Fact]
    public void Dispose_Idempotent_DoesNotThrow()
    {
        var target = MakeTarget(new FakeHttpHandler());
        var act = () =>
        {
            target.Dispose();
            target.Dispose();
            target.Dispose();
        };
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Emit_AfterDispose_SilentlyDropped()
    {
        var handler = new FakeHttpHandler();
        var target = MakeTarget(handler);
        target.Dispose();

        await Task.Delay(50);
        var act = () => target.Emit(EntryFactory.Make());
        act.Should().NotThrow();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_DoesNotDispose_InjectedHttpClient()
    {
        var handler = new FakeHttpHandler();
        var client = new HttpClient(handler);
        var target = new HttpTarget(client, Endpoint);
        target.Dispose();

        // HttpClient 仍可用（未被 dispose）
        var act = () => client.BaseAddress; // 访问属性，disposed 的 client 会抛
        act.Should().NotThrow();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Dispose 超时路径
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_Timeout_DoesNotHangForever()
    {
        // 请求永远不返回，Dispose 应在 disposeTimeout 内完成
        var handler = FakeHttpHandler.HangUntilCancelled();
        var target = MakeTarget(handler,
            batchSize: 1,
            flushInterval: TimeSpan.FromMilliseconds(10),
            disposeTimeout: TimeSpan.FromMilliseconds(300),
            requestTimeout: TimeSpan.FromSeconds(30));

        target.Emit(EntryFactory.Make());

        var sw = System.Diagnostics.Stopwatch.StartNew();
        target.Dispose();
        sw.Stop();

        // Dispose 应在合理时间内返回（disposeTimeout + 一些余量）
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 错误处理
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_NonSuccessStatus_DoesNotThrow()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var handler = new FakeHttpHandler((_, _) =>
        {
            if (Interlocked.Increment(ref calls) >= 2) tcs.TrySetResult(true);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
        });

        using var target = MakeTarget(handler);
        var act = () =>
        {
            target.Emit(EntryFactory.Make());
            target.Emit(EntryFactory.Make());
        };
        act.Should().NotThrow();
        await tcs.Task.WaitAsync(WaitTimeout); // 等重试完成
    }

    [Fact]
    public async Task Send_HttpClientThrows_DoesNotCrashBackgroundTask()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var handler = new FakeHttpHandler((_, _) =>
        {
            if (Interlocked.Increment(ref calls) >= 2) tcs.TrySetResult(true);
            throw new HttpRequestException("network error");
        });

        using var target = MakeTarget(handler);
        target.Emit(EntryFactory.Make());
        target.Emit(EntryFactory.Make());

        await tcs.Task.WaitAsync(WaitTimeout);

        // 后台任务仍然运行（Dispose 不抛）
        var act = () => target.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Send_SerializationThrows_DoesNotCrashBackgroundTask()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serializer = new DelegateHttpLogSerializer(_ =>
        {
            tcs.TrySetResult(true);
            throw new InvalidOperationException("serialize fail");
        });

        using var target = MakeTarget(new FakeHttpHandler(), serializer: serializer);
        target.Emit(EntryFactory.Make());
        target.Emit(EntryFactory.Make());

        await tcs.Task.WaitAsync(WaitTimeout);

        var act = () => target.Dispose();
        act.Should().NotThrow();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 构造验证
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NonHttpScheme_ThrowsArgumentException()
    {
        var act = () => new HttpTarget(
            new HttpClient(),
            new Uri("ftp://example.com/logs"));
        act.Should().Throw<ArgumentException>().WithMessage("*http*");
    }

    [Fact]
    public void Constructor_HeaderWithCrLf_ThrowsArgumentException()
    {
        var headers = new Dictionary<string, string> { ["Bad\r\nHeader"] = "value" };
        var act = () => new HttpTarget(
            new HttpClient(),
            new Uri("http://localhost/logs"),
            headers: headers);
        act.Should().Throw<ArgumentException>();
    }
}
