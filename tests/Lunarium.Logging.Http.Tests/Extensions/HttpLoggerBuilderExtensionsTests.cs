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

using Lunarium.Logging.Http.Config;

namespace Lunarium.Logging.Http.Tests.Extensions;

/// <summary>
/// HttpLoggerBuilderExtensions 集成冒烟测试。
/// 策略：构建 Logger → Emit 两条达到 batchSize=2 → TCS 等待请求。
/// </summary>
public class HttpLoggerBuilderExtensionsTests : IDisposable
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    // 用于存储所有在测试中创建的 ILogger，供 Dispose 统一释放
    private readonly List<ILogger> _loggers = [];

    public void Dispose()
    {
        foreach (var logger in _loggers)
            (logger as IDisposable)?.Dispose();
    }

    private ILogger BuildLogger(Action<LoggerBuilder> configure)
    {
        var builder = new LoggerBuilder();
        configure(builder);
        var logger = builder.Build();
        _loggers.Add(logger);
        return logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AddHttpSink (string endpoint)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddHttpSink_EmitTwice_RequestSent()
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler(async (req, _) =>
        {
            tcs.TrySetResult(await req.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var logger = BuildLogger(b => b.AddHttpSink(
            new HttpClient(handler),
            "http://localhost:9999/logs",
            batchSize: 2,
            flushInterval: TimeSpan.FromSeconds(60)));

        logger.Info("msg-a");
        logger.Info("msg-b");

        var body = await tcs.Task.WaitAsync(WaitTimeout);
        body.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddHttpSink_ContentType_IsApplicationJson()
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler((req, _) =>
        {
            tcs.TrySetResult(req.Content?.Headers.ContentType?.MediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var logger = BuildLogger(b => b.AddHttpSink(
            new HttpClient(handler),
            "http://localhost:9999/logs",
            batchSize: 2,
            flushInterval: TimeSpan.FromSeconds(60)));

        logger.Info("a");
        logger.Info("b");

        var ct = await tcs.Task.WaitAsync(WaitTimeout);
        ct.Should().Be("application/json");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AddHttpSink (HttpSinkConfig)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddHttpSink_Config_RequestSent()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler((_, _) =>
        {
            tcs.TrySetResult(true);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var config = new HttpSinkConfig
        {
            HttpClient = new HttpClient(handler),
            Endpoint = new Uri("http://localhost:9999/logs"),
            BatchSize = 2,
            FlushInterval = TimeSpan.FromSeconds(60)
        };

        var logger = BuildLogger(b => b.AddHttpSink(config));
        logger.Info("a");
        logger.Info("b");

        await tcs.Task.WaitAsync(WaitTimeout);
        handler.Requests.Should().HaveCount(1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AddSeqSink
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddSeqSink_ContentType_IsClef()
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler((req, _) =>
        {
            tcs.TrySetResult(req.Content?.Headers.ContentType?.MediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var logger = BuildLogger(b => b.AddSeqSink(
            new HttpClient(handler),
            "http://localhost:5341/api/events/raw",
            batchSize: 2,
            flushInterval: TimeSpan.FromSeconds(60)));

        logger.Info("a");
        logger.Info("b");

        var ct = await tcs.Task.WaitAsync(WaitTimeout);
        ct.Should().Be("application/vnd.serilog.clef");
    }

    [Fact]
    public async Task AddSeqSink_WithApiKey_HeaderAttached()
    {
        var tcs = new TaskCompletionSource<HttpRequestMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler((req, _) =>
        {
            tcs.TrySetResult(req);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var logger = BuildLogger(b => b.AddSeqSink(
            new HttpClient(handler),
            "http://localhost:5341/api/events/raw",
            apiKey: "my-key",
            batchSize: 2,
            flushInterval: TimeSpan.FromSeconds(60)));

        logger.Info("a");
        logger.Info("b");

        var req = await tcs.Task.WaitAsync(WaitTimeout);
        req.Headers.TryGetValues("X-Seq-ApiKey", out var vals).Should().BeTrue();
        vals!.First().Should().Be("my-key");
    }

    [Fact]
    public async Task AddSeqSink_NoApiKey_NoSeqHeader()
    {
        var tcs = new TaskCompletionSource<HttpRequestMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler((req, _) =>
        {
            tcs.TrySetResult(req);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var logger = BuildLogger(b => b.AddSeqSink(
            new HttpClient(handler),
            "http://localhost:5341/api/events/raw",
            apiKey: null,
            batchSize: 2,
            flushInterval: TimeSpan.FromSeconds(60)));

        logger.Info("a");
        logger.Info("b");

        var req = await tcs.Task.WaitAsync(WaitTimeout);
        req.Headers.TryGetValues("X-Seq-ApiKey", out _).Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AddLokiSink
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddLokiSink_ContentType_IsApplicationJson()
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler((req, _) =>
        {
            tcs.TrySetResult(req.Content?.Headers.ContentType?.MediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var logger = BuildLogger(b => b.AddLokiSink(
            new HttpClient(handler),
            "http://localhost:3100/loki/api/v1/push",
            batchSize: 2,
            flushInterval: TimeSpan.FromSeconds(60)));

        logger.Info("a");
        logger.Info("b");

        var ct = await tcs.Task.WaitAsync(WaitTimeout);
        ct.Should().Be("application/json");
    }

    [Fact]
    public async Task AddLokiSink_WithLabels_LabelsInStream()
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler(async (req, _) =>
        {
            tcs.TrySetResult(await req.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var labels = new Dictionary<string, string> { ["app"] = "myservice" };
        var logger = BuildLogger(b => b.AddLokiSink(
            new HttpClient(handler),
            "http://localhost:3100/loki/api/v1/push",
            labels: labels,
            batchSize: 2,
            flushInterval: TimeSpan.FromSeconds(60)));

        logger.Info("a");
        logger.Info("b");

        var body = await tcs.Task.WaitAsync(WaitTimeout);
        using var doc = JsonDocument.Parse(body);
        var stream = doc.RootElement.GetProperty("streams")[0].GetProperty("stream");
        stream.GetProperty("app").GetString().Should().Be("myservice");
    }
}
