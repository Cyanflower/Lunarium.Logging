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

namespace Lunarium.Logging.Http.Tests.Helpers;

/// <summary>
/// 可配置响应的 HttpMessageHandler，捕获所有发出的请求。
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public List<HttpRequestMessage> Requests { get; } = new();

    /// <summary>默认返回 200 OK。</summary>
    public FakeHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? handler = null)
    {
        _handler = handler ?? ((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
    }

    /// <summary>同步返回固定响应的便捷重载。</summary>
    public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : this((req, _) => Task.FromResult(handler(req))) { }

    /// <summary>固定返回指定状态码。</summary>
    public FakeHttpHandler(HttpStatusCode statusCode)
        : this(_ => new HttpResponseMessage(statusCode)) { }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return await _handler(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>永不返回，直到 CancellationToken 触发（模拟请求超时）。</summary>
    public static FakeHttpHandler HangUntilCancelled() =>
        new(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

    /// <summary>首次调用返回失败，之后返回成功（模拟一次重试成功）。</summary>
    public static FakeHttpHandler FailOnceThenSucceed()
    {
        int calls = 0;
        return new FakeHttpHandler(_ =>
            Interlocked.Increment(ref calls) == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK));
    }

    /// <summary>始终抛出 HttpRequestException（模拟网络错误）。</summary>
    public static FakeHttpHandler AlwaysThrow() =>
        new((_, _) => throw new HttpRequestException("Simulated network error"));
}
