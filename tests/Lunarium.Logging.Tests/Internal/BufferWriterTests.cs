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

using System.Text;
using Lunarium.Logging.Config.GlobalConfig;
using Lunarium.Logging.Internal;
using Xunit;
using FluentAssertions;

namespace Lunarium.Logging.Tests.Internal;

public class BufferWriterTests
{
    [Fact]
    public void Remove_ShouldRemoveCorectByteRange()
    {
        using var writer = new BufferWriter(16);
        writer.Append("abcdefg"); // 7 bytes
        
        // Remove "cd" (index 2, count 2)
        writer.Remove(2, 2);
        
        writer.ToString().Should().Be("abefg");
        writer.Length.Should().Be(5);
        writer.WrittenCount.Should().Be(5);
    }

    [Fact]
    public void Remove_AtEnd_ShouldBeEquivalentToRemoveLast()
    {
        using var writer = new BufferWriter(16);
        writer.Append("abcde");
        
        writer.Remove(3, 2); // Remove "de"
        
        writer.ToString().Should().Be("abc");
        writer.Length.Should().Be(3);
    }

    [Fact]
    public void RemoveLast_ShouldDecreaseIndex()
    {
        using var writer = new BufferWriter(16);
        writer.Append("abcde");
        
        writer.RemoveLast(2);
        
        writer.ToString().Should().Be("abc");
        writer.Length.Should().Be(3);
    }

    [Fact]
    public void Append_NonAsciiChar_ShouldEncodeCorrectly()
    {
        using var writer = new BufferWriter(16);
        writer.Append('€'); // 3 bytes in UTF-8
        writer.Append('中'); // 3 bytes in UTF-8
        
        var expected = "€中";
        writer.ToString().Should().Be(expected);
        writer.Length.Should().Be(6);
    }

    [Fact]
    public void GetSpan_ShouldReturnCorrectSliceAndEnsureCapacity()
    {
        using var writer = new BufferWriter(10);
        writer.Append("12345");
        
        // Request span that fits
        var span = writer.GetSpan(5);
        span.Length.Should().BeGreaterThanOrEqualTo(5);
        
        // Request span that causes expansion
        var bigSpan = writer.GetSpan(100);
        bigSpan.Length.Should().BeGreaterThanOrEqualTo(100);
        writer.Capacity.Should().BeGreaterThanOrEqualTo(105);
    }

    [Fact]
    public void Indexer_ShouldReturnCorrectByte()
    {
        using var writer = new BufferWriter(16);
        writer.Append("abc");
        
        writer[0].Should().Be((byte)'a');
        writer[1].Should().Be((byte)'b');
        writer[2].Should().Be((byte)'c');
    }

    [Fact]
    public void WrittenSpan_ShouldReturnCorrectView()
    {
        using var writer = new BufferWriter(16);
        writer.Append("abc");

        var span = writer.WrittenSpan;
        span.Length.Should().Be(3);
        span[0].Should().Be((byte)'a');
        span[1].Should().Be((byte)'b');
        span[2].Should().Be((byte)'c');
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Advance
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Advance_NegativeCount_Throws()
    {
        using var writer = new BufferWriter(16);
        writer.GetSpan(4); // ensure there's space
        var act = () => writer.Advance(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Advance_BeyondBuffer_Throws()
    {
        using var writer = new BufferWriter(4);
        var span = writer.GetSpan(4);
        // Advance one past the available bytes
        var act = () => writer.Advance(5);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Advance_ValidCount_UpdatesLength()
    {
        using var writer = new BufferWriter(16);
        var span = writer.GetSpan(4);
        span[0] = (byte)'X';
        writer.Advance(1);
        writer.Length.Should().Be(1);
        writer.ToString().Should().Be("X");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetMemory
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetMemory_WithSizeHint_EnsuresCapacity()
    {
        using var writer = new BufferWriter(4);
        writer.Append("ab");
        var mem = writer.GetMemory(100); // triggers expansion
        mem.Length.Should().BeGreaterThanOrEqualTo(100);
        writer.Capacity.Should().BeGreaterThanOrEqualTo(102);
    }

    [Fact]
    public void GetMemory_WhenFull_Expands()
    {
        using var writer = new BufferWriter(2);
        writer.Append("ab"); // fills 2 bytes
        var mem = writer.GetMemory(); // _index == _buffer.Length, triggers expand
        mem.Length.Should().BeGreaterThan(0);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Append(object?)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AppendObject_Null_IsNoop()
    {
        using var writer = new BufferWriter(16);
        writer.Append((object?)null);
        writer.Length.Should().Be(0);
        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void AppendObject_NonNull_CallsToString()
    {
        using var writer = new BufferWriter(16);
        writer.Append((object?)42);
        writer.ToString().Should().Be("42");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Append(ReadOnlySpan<byte>)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AppendByteSpan_Empty_IsNoop()
    {
        using var writer = new BufferWriter(16);
        writer.Append(ReadOnlySpan<byte>.Empty);
        writer.Length.Should().Be(0);
    }

    [Fact]
    public void AppendByteSpan_WritesBytes()
    {
        using var writer = new BufferWriter(16);
        writer.Append("hello"u8);
        writer.Length.Should().Be(5);
        writer.ToString().Should().Be("hello");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // AppendLine
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AppendLine_AppendsNewLine()
    {
        using var writer = new BufferWriter(32);
        writer.Append("line");
        writer.AppendLine();
        writer.ToString().Should().Be("line" + Environment.NewLine);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // AppendFormat(string, object?)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AppendFormat_Object_FormatsCorrectly()
    {
        using var writer = new BufferWriter(32);
        writer.AppendFormat("{0:D4}", 7);
        writer.ToString().Should().Be("0007");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // AppendFormat(string, string?) — fast paths
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AppendFormat_DirectPath_WritesValue()
    {
        using var writer = new BufferWriter(32);
        writer.AppendFormat("{0}", "hello");
        writer.ToString().Should().Be("hello");
    }

    [Fact]
    public void AppendFormat_DirectPath_NullValue_WritesEmpty()
    {
        using var writer = new BufferWriter(32);
        writer.AppendFormat("{0}", (string?)null);
        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void AppendFormat_RightAlign_PadsCorrectly()
    {
        using var writer = new BufferWriter(32);
        writer.AppendFormat("{0,6}", "ab");
        writer.ToString().Should().Be("    ab");
    }

    [Fact]
    public void AppendFormat_LeftAlign_PadsCorrectly()
    {
        using var writer = new BufferWriter(32);
        writer.AppendFormat("{0,-6}", "ab");
        writer.ToString().Should().Be("ab    ");
    }

    [Fact]
    public void AppendFormat_RightAlign_ContentExceedsWidth_NoPadding()
    {
        using var writer = new BufferWriter(32);
        writer.AppendFormat("{0,2}", "hello"); // content longer than width
        writer.ToString().Should().Be("hello");
    }

    [Fact]
    public void AppendFormat_LeftAlign_ContentExceedsWidth_NoPadding()
    {
        using var writer = new BufferWriter(32);
        writer.AppendFormat("{0,-2}", "hello");
        writer.ToString().Should().Be("hello");
    }

    [Fact]
    public void AppendFormat_WithFormatSpecifier_FallsBack()
    {
        // "{0:X}" contains ':' so TryParseAlignmentFormat fails, falls to string.Format
        using var writer = new BufferWriter(32);
        writer.AppendFormat("{0:X}", "abc");
        // string.Format("{0:X}", "abc") on a string returns "abc" (format ignored for string)
        writer.ToString().Should().Be("abc");
    }

    [Fact]
    public void AppendFormat_TooShortFormat_FallsBack()
    {
        // Format shorter than 6 chars: TryParseAlignmentFormat returns false → fallback
        using var writer = new BufferWriter(32);
        writer.AppendFormat("{0}", "xy"); // exactly 3 chars, fast path hits the "{0}" case
        writer.ToString().Should().Be("xy");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // AppendFormattable<T>
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AppendFormattable_Int_WritesCorrectly()
    {
        using var writer = new BufferWriter(32);
        writer.AppendFormattable(12345);
        writer.ToString().Should().Be("12345");
    }

    [Fact]
    public void AppendFormattable_Double_WithFormat_WritesFormatted()
    {
        using var writer = new BufferWriter(32);
        writer.AppendFormattable(3.14159, "F2");
        writer.ToString().Should().Be("3.14");
    }

    [Fact]
    public void AppendFormattable_DateTimeOffset_ISO8601_WritesCorrectly()
    {
        using var writer = new BufferWriter(64);
        var ts = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);
        writer.AppendFormattable(ts, "O");
        writer.ToString().Should().StartWith("2026-03-15");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Rewind
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Rewind_ValidIndex_TruncatesContent()
    {
        using var writer = new BufferWriter(32);
        writer.Append("abcdefg");
        writer.Rewind(3);
        writer.Length.Should().Be(3);
        writer.ToString().Should().Be("abc");
    }

    [Fact]
    public void Rewind_NegativeIndex_Throws()
    {
        using var writer = new BufferWriter(32);
        writer.Append("abc");
        var act = () => writer.Rewind(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rewind_BeyondWritten_Throws()
    {
        using var writer = new BufferWriter(32);
        writer.Append("abc");
        var act = () => writer.Rewind(10);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rewind_ToZero_ClearsContent()
    {
        using var writer = new BufferWriter(32);
        writer.Append("hello");
        writer.Rewind(0);
        writer.Length.Should().Be(0);
        writer.ToString().Should().BeEmpty();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // FlushTo(Stream)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FlushTo_Stream_WritesAllBytes()
    {
        using var writer = new BufferWriter(32);
        writer.Append("flush-me");
        using var ms = new MemoryStream();
        writer.FlushTo(ms);
        var result = Encoding.UTF8.GetString(ms.ToArray());
        result.Should().Be("flush-me");
    }

    [Fact]
    public void FlushTo_Stream_Empty_WritesNothing()
    {
        using var writer = new BufferWriter(32);
        using var ms = new MemoryStream();
        writer.FlushTo(ms);
        ms.Length.Should().Be(0);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Reset
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Reset_ClearsWrittenContent()
    {
        using var writer = new BufferWriter(32);
        writer.Append("some content");
        writer.Reset();
        writer.Length.Should().Be(0);
        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Reset_PreservesCapacity()
    {
        using var writer = new BufferWriter(64);
        writer.Append("data");
        int capBefore = writer.Capacity;
        writer.Reset();
        writer.Capacity.Should().Be(capBefore);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Dispose
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var writer = new BufferWriter(16);
        writer.Append("hi");
        var act = () => writer.Dispose();
        act.Should().NotThrow();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // EnsureCapacity — empty buffer initial path
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Append_EmptyBufferCapacity_GrowsOnFirstWrite()
    {
        // capacity <= 0 → _buffer = Array.Empty<byte>()
        using var writer = new BufferWriter(0);
        writer.Capacity.Should().Be(0);
        writer.Append("hello");
        writer.ToString().Should().Be("hello");
        writer.Capacity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Append_String_Null_IsNoop()
    {
        using var writer = new BufferWriter(16);
        writer.Append((string?)null);
        writer.Length.Should().Be(0);
    }

    [Fact]
    public void AppendSpan_Empty_IsNoop()
    {
        using var writer = new BufferWriter(16);
        writer.AppendSpan(ReadOnlySpan<char>.Empty);
        writer.Length.Should().Be(0);
    }
}
