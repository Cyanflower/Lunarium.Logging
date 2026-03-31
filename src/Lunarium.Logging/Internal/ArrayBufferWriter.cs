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

using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Numerics;

namespace Lunarium.Logging.Internal;

internal sealed class BufferWriter : IBufferWriter<byte>, IDisposable
{
    private static readonly int MAX_ARRAY_SIZE = Array.MaxLength;
    private byte[] _buffer;
    private int _index;

    internal BufferWriter(int capacity = 4096)
    {
        if (capacity <= 0) _buffer = Array.Empty<byte>();
        else _buffer = new byte[capacity];
        _index = 0;
    }

    // ===== 低级 IBufferWriter<byte> API =====

    internal int Capacity => _buffer.Length;
    internal int WrittenCount => _index;

    // 已写入字节数，等价于 WrittenCount
    internal int Length => _index;

    // 已写入内容的只读视图，零拷贝
    internal ReadOnlySpan<byte> WrittenSpan => new ReadOnlySpan<byte>(_buffer, 0, _index);

    // 按字节索引访问已写内容（只读）。用于 JSON 尾部逗号检查等场景
    internal byte this[int index] => _buffer[index];

    public void Advance(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (_index > _buffer.Length - count) throw new InvalidOperationException("Writing beyond the buffer zone boundary");
        _index += count;
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        if (sizeHint > 0 && _buffer.Length - _index < sizeHint)
            EnsureCapacity(sizeHint);
        else if (_index == _buffer.Length)
            EnsureCapacity(1);
        return _buffer.AsSpan(_index);
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (sizeHint > 0 && _buffer.Length - _index < sizeHint)
            EnsureCapacity(sizeHint);
        else if (_index == _buffer.Length)
            EnsureCapacity(1);
        return _buffer.AsMemory(_index);
    }

    // ===== 高级写入 API =====

    // 将字符串编码为 UTF-8 追加到缓冲区
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Append(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        AppendSpan(value.AsSpan());
    }

    // 将 char span 编码为 UTF-8 追加到缓冲区
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AppendSpan(ReadOnlySpan<char> chars)
    {
        if (chars.IsEmpty) return;
        int maxBytes = Encoding.UTF8.GetMaxByteCount(chars.Length);
        EnsureCapacity(maxBytes);
        int written = Encoding.UTF8.GetBytes(chars, _buffer.AsSpan(_index));
        _index += written;
    }

    // 追加单个字符（ASCII 走快速路径，无需 Encoding 调用）
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Append(char c)
    {
        if (c < 0x80)
        {
            EnsureCapacity(1);
            _buffer[_index++] = (byte)c;
            return;
        }
        Span<char> single = stackalloc char[1];
        single[0] = c;
        AppendSpan(single);
    }

    // 追加任意对象（调用 ToString() 后编码为 UTF-8）。用于 Exception 等场景
    internal void Append(object? value)
    {
        if (value is null) return;
        Append(value.ToString());
    }

    // 追加字节 span
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Append(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return;
        EnsureCapacity(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(_index));
        _index += bytes.Length;
    }

    // 追加换行符（Environment.NewLine）
    internal void AppendLine()
    {
        Append(Environment.NewLine);
    }

    // 格式化后追加。等价于 string.Format(InvariantCulture, format, arg)，适用于带对齐/格式的复合格式字符串
    internal void AppendFormat(string format, object? arg)
    {
        Append(string.Format(CultureInfo.InvariantCulture, format, arg));
    }

    // 字符串专用重载：绕过 string.Format，直接编码 UTF-8，避免中间字符串分配。
    // "{0}"：直接写入，零分配。
    // "{0,N}"（纯对齐，无格式化符）：手动补空格，零分配。
    // 其余格式（含 ":"）：回退到通用路径。
    internal void AppendFormat(string format, string? value)
    {
        value ??= "";

        // 快速路径："{0}"（最常见情况，直接写入）
        if (format is "{0}")
        {
            AppendSpan(value.AsSpan());
            return;
        }

        // 对齐路径："{0,N}" 或 "{0,-N}"（无格式化符号）
        if (TryParseAlignmentFormat(format, out int alignment))
        {
            int padding = Math.Abs(alignment) - value.Length;
            if (alignment > 0) // 右对齐：先补空格再写内容
            {
                if (padding > 0) AppendSpaces(padding);
                AppendSpan(value.AsSpan());
            }
            else // 左对齐：先写内容再补空格
            {
                AppendSpan(value.AsSpan());
                if (padding > 0) AppendSpaces(padding);
            }
            return;
        }

        // 回退：含格式化符（"{0:X}" 等），string 较少用但语法合法
        Append(string.Format(CultureInfo.InvariantCulture, format, value));
    }

    // 尝试解析纯对齐格式字符串 "{0,N}"（不含 ":" 的情况）
    private static bool TryParseAlignmentFormat(string format, out int alignment)
    {
        alignment = 0;
        // 最短合法形式是 "{0,1}" = 6 个字符
        if (format.Length < 6 || format[0] != '{' || format[1] != '0' || format[2] != ',' || format[^1] != '}')
            return false;
        var alignPart = format.AsSpan(3, format.Length - 4);
        // 含冒号则有格式化符，不走此路径
        if (alignPart.IndexOf(':') >= 0)
            return false;
        return int.TryParse(alignPart, out alignment);
    }

    // 向缓冲区写入 count 个 ASCII 空格
    private void AppendSpaces(int count)
    {
        EnsureCapacity(count);
        _buffer.AsSpan(_index, count).Fill((byte)' ');
        _index += count;
    }

    // 将 IUtf8SpanFormattable 直接格式化为 UTF-8 字节写入缓冲区，无中间字符串分配。
    // 适用于 int/long/double/DateTimeOffset 等所有实现该接口的类型（.NET 8+）
    internal void AppendFormattable<T>(T value, ReadOnlySpan<char> format = default)
        where T : IUtf8SpanFormattable
    {
        int sizeHint = 64;
        while (true)
        {
            EnsureCapacity(sizeHint);
            if (value.TryFormat(_buffer.AsSpan(_index), out int written, format, CultureInfo.InvariantCulture))
            {
                _index += written;
                return;
            }
            sizeHint *= 2;
        }
    }

    // ===== 缓冲区修改 =====

    internal void Rewind(int index)
    {
        if (index < 0 || index > _index) throw new ArgumentOutOfRangeException(nameof(index));
        _index = index;
    }

    // 移除末尾 count 个字节。用于去除 JSON 尾部逗号（单字节 ASCII）等场景，O(1)
    internal void RemoveLast(int count = 1)
    {
        _index -= count;
    }

    // 移除指定位置的字节段。末尾移除退化为 O(1)；中间移除需内存移动
    internal void Remove(int startIndex, int count)
    {
        if (startIndex + count == _index)
        {
            _index -= count;
            return;
        }
        Buffer.BlockCopy(_buffer, startIndex + count, _buffer, startIndex, _index - startIndex - count);
        _index -= count;
    }

    // ===== 输出 =====

    // 将已写内容直接写入流，零拷贝，无分配
    internal void FlushTo(Stream stream) => stream.Write(_buffer, 0, _index);

    // 将已写内容解码为字符串（UTF-8 → string），供 StringChannelTarget 使用
    public override string ToString() => Encoding.UTF8.GetString(_buffer, 0, _index);

    // ===== 池化 & 生命周期 =====

    internal void Reset()
    {
        _index = 0;
    }

    public void Dispose()
    {
        _buffer = null!;
    }

    private void EnsureCapacity(int sizeHint)
    {
        // 1. 使用 uint 进行计算可以自然处理 int.MaxValue 附近的边界
        uint request = (uint)(sizeHint <= 0 ? 1 : sizeHint);
        uint minCapacity = (uint)_index + request;
        if (minCapacity > (uint)_buffer.Length)
        {
            uint newSize;
            if (_buffer.Length == 0)
            {
                newSize = Math.Max(minCapacity, 256u); // 给个初始阈值
                // 如果初始值不是 2 的幂，对齐到 2 的幂
                if (!BitOperations.IsPow2(newSize))
                    newSize = 1u << (32 - BitOperations.LeadingZeroCount(newSize - 1));
            }
            else
            {
                // 直接计算下一个 2 的幂
                newSize = 1u << (32 - BitOperations.LeadingZeroCount(minCapacity - 1));
            }
            // 3. 边界检查：防止超过 .NET 数组最大限制
            if (newSize > (uint)MAX_ARRAY_SIZE)
            {
                newSize = Math.Max(minCapacity, (uint)MAX_ARRAY_SIZE);
                if (newSize > (uint)MAX_ARRAY_SIZE)
                    throw new OutOfMemoryException("Buffer size limit exceeded.");
            }
            // 4. 执行分配
            byte[] newBuffer = new byte[newSize];
            _buffer.AsSpan(0, _index).CopyTo(newBuffer);
            _buffer = newBuffer;
        }
    }
}
