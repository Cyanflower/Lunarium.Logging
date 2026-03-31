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

using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using FluentAssertions;
using Lunarium.Logging.Config.GlobalConfig;
using Lunarium.Logging.Internal;
using Lunarium.Logging.Config.Models;
using Lunarium.Logging.Core;
using Lunarium.Logging.Target;

namespace Lunarium.Logging.Tests.Core;

/// <summary>
/// 多线程安全性测试：验证并发 .Log() 调用的正确性。
/// </summary>
public class LoggerConcurrencyTests
{
    private static readonly FieldInfo? _isConfiguringField =
        typeof(GlobalConfigurator).GetField("_isConfiguring",
            BindingFlags.Static | BindingFlags.NonPublic);

    private static void ResetAll()
    {
        GlobalConfigLock.Configured = false;
        _isConfiguringField?.SetValue(null, false);
        GlobalConfigurator.ApplyDefaultIfNotConfigured();
    }

    private static (ILogger logger, Channel<string> ch) MakeLogger()
    {
        var ch = Channel.CreateUnbounded<string>();
        var target = new StringChannelTarget(ch.Writer, toJson: false, isColor: false);
        var cfg = new FilterConfig();
        var sinks = new List<Sink> { new(target, cfg) };
        return (new Logger(sinks, "ConcurrentTest"), ch);
    }

    [Fact]
    public async Task Log_ConcurrentCalls_NoException()
    {
        ResetAll();
        var (logger, ch) = MakeLogger();
        const int taskCount = 1000;

        var tasks = Enumerable.Range(0, taskCount)
            .Select(i => Task.Run(() => logger.Info("Concurrent test {Id}", i)));

        var act = () => Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();

        await logger.DisposeAsync();
    }

    [Fact]
    public async Task Log_ConcurrentCalls_AllMessagesReceived()
    {
        ResetAll();
        var (logger, ch) = MakeLogger();
        const int taskCount = 500;
        var ids = new HashSet<int>();

        var tasks = Enumerable.Range(0, taskCount)
            .Select(i => Task.Run(() =>
            {
                logger.Info("Message {Id}", i);
                lock (ids) ids.Add(i);
            }));

        await Task.WhenAll(tasks);
        await logger.DisposeAsync();

        var receivedIds = new HashSet<int>();
        await foreach (var msg in ch.Reader.ReadAllAsync())
        {
            var match = Regex.Match(msg, @"Message (\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                receivedIds.Add(id);
        }

        receivedIds.Count.Should().Be(taskCount, "all messages should be received");
    }

}
