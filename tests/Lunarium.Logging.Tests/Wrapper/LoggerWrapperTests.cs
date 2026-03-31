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
using Lunarium.Logging.Wrapper;
using Lunarium.Logging.Models;
using NSubstitute;

namespace Lunarium.Logging.Tests.Wrapper;

/// <summary>
/// Tests for LoggerWrapper — the ForContext decorator.
/// Uses NSubstitute to mock the underlying ILogger and capture Log() arguments.
/// </summary>
public class LoggerWrapperTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (ILogger mock, LoggerWrapper wrapper) MakeWrapper(string context)
    {
        var mock = Substitute.For<ILogger>();
        var wrapper = new LoggerWrapper(mock, context);
        return (mock, wrapper);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Basic context attachment
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Log_EmptyPassedContext_UsesWrapperContext()
    {
        var (mock, wrapper) = MakeWrapper("MyCtx");
        wrapper.Log(LogLevel.Info, message: "msg");

        mock.Received(1).Log(
            LogLevel.Info,
            ex: (Exception?)null,
            message: "msg",
            context: "MyCtx",
            contextBytes: Arg.Any<ReadOnlyMemory<byte>>(),
            scope: Arg.Any<string>(),
            propertyValues: Arg.Any<object?[]>());
    }

    [Fact]
    public void Log_NullPassedContext_UsesWrapperContext()
    {
        var (mock, wrapper) = MakeWrapper("MyCtx");
        // Passing null context — should still attach wrapper's context
        wrapper.Log(LogLevel.Warning, message: "msg", context: null!);

        mock.Received(1).Log(
            LogLevel.Warning,
            ex: null,
            message: "msg",
            context: "MyCtx",
            contextBytes: Arg.Any<ReadOnlyMemory<byte>>(),
            scope: Arg.Any<string>(),
            propertyValues: Arg.Any<object?[]>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Context combination (inner context non-empty)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Log_NonEmptyPassedContext_CombinedWithDot()
    {
        var (mock, wrapper) = MakeWrapper("Outer");
        wrapper.Log(LogLevel.Info, message: "msg", context: "Inner");

        mock.Received(1).Log(
            LogLevel.Info,
            ex: null,
            message: "msg",
            context: "Outer.Inner",
            contextBytes: Arg.Any<ReadOnlyMemory<byte>>(),
            scope: Arg.Any<string>(),
            propertyValues: Arg.Any<object?[]>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Nested wrappers (ForContext chained)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Log_NestedWrappers_ContextPathBuiltCorrectly()
    {
        var innerMock = Substitute.For<ILogger>();
        var first = new LoggerWrapper(innerMock, "A");
        var second = new LoggerWrapper(first, "B");

        second.GetContext().Should().Be("A.B");
    }

    [Fact]
    public void Log_TripleNested_ContextPathBuiltCorrectly()
    {
        var innerMock = Substitute.For<ILogger>();
        var w1 = new LoggerWrapper(innerMock, "A");
        var w2 = new LoggerWrapper(w1, "B");
        var w3 = new LoggerWrapper(w2, "C");

        w3.GetContext().Should().Be("A.B.C");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Exception passthrough
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Log_WithException_ExceptionPassedThrough()
    {
        var (mock, wrapper) = MakeWrapper("Ctx");
        var ex = new InvalidOperationException("test");
        wrapper.Log(LogLevel.Error, ex: ex, message: "msg");

        mock.Received(1).Log(
            LogLevel.Error,
            ex: ex,
            message: "msg",
            context: "Ctx",
            contextBytes: Arg.Any<ReadOnlyMemory<byte>>(),
            scope: Arg.Any<string>(),
            propertyValues: Arg.Any<object?[]>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Property values passthrough
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Log_WithProperties_PropertiesPassedThrough()
    {
        var (mock, wrapper) = MakeWrapper("Ctx");
        wrapper.Log(LogLevel.Info, message: "Hello {Name}", propertyValues: ["Alice"]);

        mock.Received(1).Log(
            LogLevel.Info,
            ex: null,
            message: "Hello {Name}",
            context: "Ctx",
            contextBytes: Arg.Any<ReadOnlyMemory<byte>>(),
            scope: Arg.Any<string>(),
            propertyValues: Arg.Is<object?[]>(arr => arr.Length == 1 && (string?)arr[0] == "Alice"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. GetContextSpan — bytes match GetContext()
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetContextSpan_DecodesToSameStringAsGetContext()
    {
        var (_, wrapper) = MakeWrapper("MyModule");
        var context = wrapper.GetContext();
        var span = wrapper.GetContextSpan();
        System.Text.Encoding.UTF8.GetString(span.Span).Should().Be(context);
    }

    [Fact]
    public void GetContextSpan_NestedWrapper_DecodesToFullPath()
    {
        var innerMock = Substitute.For<ILogger>();
        var w1 = new LoggerWrapper(innerMock, "A");
        var w2 = new LoggerWrapper(w1, "B");

        var decoded = System.Text.Encoding.UTF8.GetString(w2.GetContextSpan().Span);
        decoded.Should().Be("A.B");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. DisposeAsync — does not propagate to inner logger
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_IsNoop_DoesNotDisposeInner()
    {
        var (mock, wrapper) = MakeWrapper("Ctx");
        await wrapper.DisposeAsync();
        // Inner logger must NOT have been disposed
        await mock.DidNotReceive().DisposeAsync();
    }
}
