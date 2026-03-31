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

namespace Lunarium.Logging.Parser;

/// <summary>Controls how a property value is serialized when rendered.</summary>
public enum Destructuring
{
    /// <summary>Default (<c>{Name}</c>): calls <c>ToString()</c> or uses type-specific formatting.</summary>
    Default,
    /// <summary>Structured destructuring (<c>{@Object}</c>): renders the value as a JSON object.</summary>
    Destructure,
    /// <summary>stringify (<c>{$Object}</c>): renders the value by calling <c>ToString()</c>.</summary>
    Stringify
}

/// <summary>
/// The parsed result of a log message template string.
/// Holds a sequence of <see cref="MessageTemplateTokens"/> (text or property holes)
/// plus the original message encoded as UTF-8 bytes for zero-copy JSON output.
/// </summary>
public record MessageTemplate
{
    /// <summary>Ordered list of text segments and property holes parsed from the template.</summary>
    public IReadOnlyList<MessageTemplateTokens> MessageTemplateTokens { get; }

    // OriginalMessageBytes 用于 JSON 输出的 OriginalMessage 字段，直接写入字节，无需再次编码
    internal readonly ReadOnlyMemory<byte> OriginalMessageBytes;


    internal MessageTemplate(MessageTemplateTokens[] messageTemplateTokens, ReadOnlyMemory<byte> originalMessageBytes)
    {
        MessageTemplateTokens = (IReadOnlyList<MessageTemplateTokens>)messageTemplateTokens;
        OriginalMessageBytes = originalMessageBytes;
    }
}

/// <summary>Base type for all parsed token kinds in a message template.</summary>
public abstract record MessageTemplateTokens;

/// <summary>A literal text segment that is written verbatim to the output.</summary>
public record TextToken : MessageTemplateTokens
{
    /// <summary>The original text string.</summary>
    public string Text { get; }
    // TextBytes 是 Text 的 UTF-8 预编码，异步渲染时驱动零拷贝写入
    /// <summary>Pre-encoded UTF-8 bytes of <see cref="Text"/>. Written directly to the output buffer for zero-allocation rendering.</summary>
    public ReadOnlyMemory<byte> TextBytes { get; }
    internal TextToken(string text)
    {
        Text = text;
        TextBytes = Encoding.UTF8.GetBytes(text);
    }
}

/// <summary>A property placeholder (e.g. <c>{Name}</c>, <c>{@Obj,10:D}</c>) parsed from the message template.</summary>
public record PropertyToken : MessageTemplateTokens
{
    /// <summary>The property name without prefix or alignment/format decorators.</summary>
    public string PropertyName { get; }
    /// <summary>The raw placeholder text as it appeared in the original template, used as a fallback when no argument is found.</summary>
    public TextToken RawText { get; }
    /// <summary>Optional format string (the part after <c>:</c>), e.g. <c>"D"</c> or <c>"yyyy-MM-dd"</c>.</summary>
    public string? Format { get; }
    /// <summary>Optional alignment width. Positive = right-align, negative = left-align.</summary>
    public int? Alignment { get; }
    // FormatString 是已展开的 composite format，如 "{0,10:D}"，由 BuildFormatString() 在构造时一次性计算并缓存
    /// <summary>
    /// Pre-built composite format string (e.g. <c>"{0,10:D}"</c>) combining alignment and format.
    /// Computed once at construction time to avoid per-render allocations.
    /// </summary>
    public string FormatString { get; }
    /// <summary>Whether the value should be destructured (<c>@</c>), stringified (<c>$</c>), or formatted normally.</summary>
    public Destructuring Destructuring { get; }
    
    // byte area
    /// <summary>Pre-encoded UTF-8 bytes of <see cref="PropertyName"/>. Used by writers for zero-allocation output.</summary>
    public ReadOnlyMemory<byte> PropertyNameBytes { get; }

    internal PropertyToken(
        string propertyName,
        TextToken rawText,
        string? format = null,
        int? alignment = null,
        Destructuring destructuring = Destructuring.Default
    )
    {
        PropertyName = propertyName;
        RawText = rawText;
        Format = format;
        Alignment = alignment;
        Destructuring = destructuring;
        FormatString = BuildFormatString(alignment, format);

        PropertyNameBytes = Encoding.UTF8.GetBytes(propertyName);
    }

    private static string BuildFormatString(int? alignment, string? format)
    {
        if (!alignment.HasValue && string.IsNullOrEmpty(format)) return "{0}";

        var sb = new StringBuilder("{0");

        // 添加对齐: {0,10} 或 {0,-10}
        if (alignment.HasValue)
        {
            sb.Append(',');
            sb.Append(alignment.Value);
        }

        // 添加格式: {0:D} 或 {0,10:D}
        if (!string.IsNullOrEmpty(format))
        {
            sb.Append(':');
            sb.Append(format);
        }

        sb.Append('}');
        return sb.ToString();
    }
}