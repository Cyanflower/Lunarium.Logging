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
using Lunarium.Logging.Http.Target;

namespace Lunarium.Logging.Http.Tests.Config;

public class HttpSinkConfigTests
{
    private static readonly Uri ValidEndpoint = new("http://localhost:9999/logs");

    private static HttpSinkConfig MakeConfig(Uri? endpoint = null) =>
        new()
        {
            Endpoint = endpoint ?? ValidEndpoint,
            HttpClient = new HttpClient()
        };

    // ─────────────────────────────────────────────────────────────────────────
    // 默认值
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Defaults_BatchSize_Is100()
    {
        MakeConfig().BatchSize.Should().Be(100);
    }

    [Fact]
    public void Defaults_FlushInterval_Is5Seconds()
    {
        MakeConfig().FlushInterval.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Defaults_DisposeTimeout_Is5Seconds()
    {
        MakeConfig().DisposeTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Defaults_ChannelCapacity_Is1000()
    {
        MakeConfig().ChannelCapacity.Should().Be(1000);
    }

    [Fact]
    public void Defaults_RequestTimeout_Is30Seconds()
    {
        MakeConfig().RequestTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Defaults_Enabled_IsTrue()
    {
        MakeConfig().Enabled.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CreateTarget
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateTarget_ReturnsHttpTargetInstance()
    {
        using var target = MakeConfig().CreateTarget();
        target.Should().BeOfType<HttpTarget>();
        target.Dispose();
    }

    [Fact]
    public void CreateTarget_InvalidScheme_ThrowsArgumentException()
    {
        var config = MakeConfig(new Uri("ftp://example.com/logs"));
        var act = () => config.CreateTarget();
        act.Should().Throw<ArgumentException>();
    }
}
