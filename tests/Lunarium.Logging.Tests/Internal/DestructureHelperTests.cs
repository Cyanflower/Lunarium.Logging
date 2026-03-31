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
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Lunarium.Logging.Internal;
using Xunit;

namespace Lunarium.Logging.Tests.Internal;

/// <summary>
/// Tests for DestructureHelper — the public API exposed to IDestructurable.Destructure() callers.
/// Tests construct the helper with bufferWriterIsMainWriter=false + jsonWriterIsMainWriter=false
/// so the Utf8JsonWriter is properly bound to the BufferWriter, enabling end-to-end verification.
/// </summary>
public class DestructureHelperTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helper: create a properly-bound DestructureHelper for standalone testing
    // ─────────────────────────────────────────────────────────────────────────

    private static (BufferWriter buf, Utf8JsonWriter jsonWriter, DestructureHelper helper) MakeHelper()
    {
        var buf = new BufferWriter(256);
        var jsonWriter = new Utf8JsonWriter(Stream.Null, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            SkipValidation = false
        });
        // bufferWriterIsMainWriter=false → buf.Reset() (already empty, no-op)
        // jsonWriterIsMainWriter=false  → jsonWriter.Reset(buf) → now writes to buf
        var helper = new DestructureHelper(buf, false, jsonWriter, false);
        return (buf, jsonWriter, helper);
    }

    private static string FlushAndRead(DestructureHelper helper)
    {
        helper.TryFlush().Should().BeTrue();
        return Encoding.UTF8.GetString(helper.WrittenSpan);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 1. Object writing
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WriteStartEndObject_EmptyObject_ProducesValidJson()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Be("{}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 2. Array writing
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WriteStartEndArray_EmptyArray_ProducesValidJson()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartArray();
        helper.WriteEndArray();
        FlushAndRead(helper).Should().Be("[]");
    }

    [Fact]
    public void WriteArray_WithValues_ProducesCorrectJson()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartArray();
        helper.WriteNumberValue(1);
        helper.WriteNumberValue(2);
        helper.WriteNumberValue(3);
        helper.WriteEndArray();
        var json = FlushAndRead(helper);
        json.Should().Be("[1,2,3]");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 3. WritePropertyName overloads
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WritePropertyName_String_WritesCorrectKey()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("Name");
        helper.WriteStringValue("Alice");
        helper.WriteEndObject();
        var json = FlushAndRead(helper);
        json.Should().Contain("\"Name\"").And.Contain("\"Alice\"");
    }

    [Fact]
    public void WritePropertyName_ReadOnlySpanChar_WritesCorrectKey()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("Age".AsSpan());
        helper.WriteNumberValue(30);
        helper.WriteEndObject();
        var json = FlushAndRead(helper);
        json.Should().Contain("\"Age\"").And.Contain("30");
    }

    [Fact]
    public void WritePropertyName_JsonEncodedText_WritesCorrectKey()
    {
        var (_, _, helper) = MakeHelper();
        var encodedKey = JsonEncodedText.Encode("Level");
        helper.WriteStartObject();
        helper.WritePropertyName(encodedKey);
        helper.WriteStringValue("Info");
        helper.WriteEndObject();
        var json = FlushAndRead(helper);
        json.Should().Contain("\"Level\"").And.Contain("\"Info\"");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 4. WriteValue (standalone value) overloads
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WriteStringValue_String_WritesQuotedValue()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("V");
        helper.WriteStringValue("hello");
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("\"hello\"");
    }

    [Fact]
    public void WriteStringValue_ReadOnlySpanChar_WritesValue()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("V");
        helper.WriteStringValue("world".AsSpan());
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("\"world\"");
    }

    [Fact]
    public void WriteStringValue_JsonEncodedText_WritesValue()
    {
        var (_, _, helper) = MakeHelper();
        var val = JsonEncodedText.Encode("encoded-val");
        helper.WriteStartObject();
        helper.WritePropertyName("V");
        helper.WriteStringValue(val);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("\"encoded-val\"");
    }

    [Fact]
    public void WriteStringValue_DateTime_WritesDateString()
    {
        var (_, _, helper) = MakeHelper();
        var dt = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        helper.WriteStartObject();
        helper.WritePropertyName("D");
        helper.WriteStringValue(dt);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("2026");
    }

    [Fact]
    public void WriteStringValue_DateTimeOffset_WritesDateString()
    {
        var (_, _, helper) = MakeHelper();
        var dto = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);
        helper.WriteStartObject();
        helper.WritePropertyName("D");
        helper.WriteStringValue(dto);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("2026");
    }

    [Fact]
    public void WriteStringValue_Guid_WritesGuidString()
    {
        var (_, _, helper) = MakeHelper();
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        helper.WriteStartObject();
        helper.WritePropertyName("G");
        helper.WriteStringValue(guid);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("12345678");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void WriteNumberValue_Int_WritesCorrectNumber(int value)
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("N");
        helper.WriteNumberValue(value);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain(value.ToString());
    }

    [Fact]
    public void WriteNumberValue_Long_WritesLargeNumber()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("N");
        helper.WriteNumberValue(long.MaxValue);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain(long.MaxValue.ToString());
    }

    [Fact]
    public void WriteNumberValue_Uint_WritesUnsigned()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("N");
        helper.WriteNumberValue(uint.MaxValue);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain(uint.MaxValue.ToString());
    }

    [Fact]
    public void WriteNumberValue_Ulong_WritesUnsignedLong()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("N");
        helper.WriteNumberValue(ulong.MaxValue);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain(ulong.MaxValue.ToString());
    }

    [Fact]
    public void WriteNumberValue_Float_WritesFloat()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("F");
        helper.WriteNumberValue(3.14f);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("3.14");
    }

    [Fact]
    public void WriteNumberValue_Double_WritesDouble()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("D");
        helper.WriteNumberValue(2.71828);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("2.71828");
    }

    [Fact]
    public void WriteNumberValue_Decimal_WritesDecimal()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("D");
        helper.WriteNumberValue(99.99m);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("99.99");
    }

    [Fact]
    public void WriteBooleanValue_True_WritesTrue()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("OK");
        helper.WriteBooleanValue(true);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("true");
    }

    [Fact]
    public void WriteBooleanValue_False_WritesFalse()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("OK");
        helper.WriteBooleanValue(false);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("false");
    }

    [Fact]
    public void WriteNullValue_WritesNull()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("N");
        helper.WriteNullValue();
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("null");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 5. Combined Write (name + value) methods
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WriteString_NameAndValue_WritesKeyValue()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteString("Name", "Alice");
        helper.WriteEndObject();
        var json = FlushAndRead(helper);
        json.Should().Contain("\"Name\"").And.Contain("\"Alice\"");
    }

    [Fact]
    public void WriteString_JsonEncodedTextName_WritesKeyValue()
    {
        var (_, _, helper) = MakeHelper();
        var key = JsonEncodedText.Encode("EncodedKey");
        helper.WriteStartObject();
        helper.WriteString(key, "Val");
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("\"EncodedKey\"").And.Contain("\"Val\"");
    }

    [Fact]
    public void WriteString_DateTime_WritesKeyValue()
    {
        var (_, _, helper) = MakeHelper();
        var dt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        helper.WriteStartObject();
        helper.WriteString("Created", dt);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("\"Created\"").And.Contain("2026");
    }

    [Fact]
    public void WriteString_DateTimeOffset_WritesKeyValue()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteString("At", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("\"At\"").And.Contain("2026");
    }

    [Fact]
    public void WriteString_Guid_WritesKeyValue()
    {
        var (_, _, helper) = MakeHelper();
        var guid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        helper.WriteStartObject();
        helper.WriteString("Id", guid);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("\"Id\"").And.Contain("aaaaaaaa");
    }

    [Fact]
    public void WriteNumber_Int_WritesKeyValue()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteNumber("Count", 99);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("\"Count\"").And.Contain("99");
    }

    [Fact]
    public void WriteNumber_Long_WritesKeyValue()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteNumber("Big", 1_000_000_000L);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("1000000000");
    }

    [Fact]
    public void WriteNumber_Double_WritesKeyValue()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteNumber("PI", 3.14);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("3.14");
    }

    [Fact]
    public void WriteBoolean_WritesKeyValue()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteBoolean("Active", true);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("\"Active\"").And.Contain("true");
    }

    [Fact]
    public void WriteNull_WritesKeyNull()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteNull("Missing");
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("\"Missing\"").And.Contain("null");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 6. WriteRawValue
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WriteRawValue_InObject_WritesRawBytes()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WritePropertyName("Data");
        helper.WriteRawValue("[1,2,3]"u8);
        helper.WriteEndObject();
        FlushAndRead(helper).Should().Contain("[1,2,3]");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 7. TryFlush behavior
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TryFlush_WhenDepthZero_ReturnsTrueAndProducesBytes()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteEndObject();
        helper.TryFlush().Should().BeTrue();
        helper.WrittenSpan.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void TryFlush_WhenDepthNonZero_ReturnsFalse()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject(); // depth = 1, not closed
        helper.TryFlush().Should().BeFalse();
    }

    [Fact]
    public void TryFlush_CalledTwice_SecondCallReturnsTrueWithSameBytes()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteString("K", "V");
        helper.WriteEndObject();
        var first = helper.TryFlush();
        var second = helper.TryFlush(); // Utf8JsonWriter is already flushed, CurrentDepth==0 still true
        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 8. WrittenSpan
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WrittenSpan_AfterFlush_ContainsJsonBytes()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteString("X", "Y");
        helper.WriteEndObject();
        helper.TryFlush();
        var bytes = helper.WrittenSpan;
        bytes.IsEmpty.Should().BeFalse();
        Encoding.UTF8.GetString(bytes).Should().Contain("\"X\"").And.Contain("\"Y\"");
    }

    [Fact]
    public void WrittenSpan_BeforeFlush_IsEmpty()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        // Don't flush
        helper.WrittenSpan.IsEmpty.Should().BeTrue();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 9. Dispose
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteEndObject();
        helper.TryFlush();
        var act = () => helper.Dispose(resetBufferWriter: false, resetJsonWriter: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WithBothReset_DoesNotThrow()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteEndObject();
        helper.TryFlush();
        var act = () => helper.Dispose(resetBufferWriter: true, resetJsonWriter: true);
        act.Should().NotThrow();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 10. Complex nested JSON (integration)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ComplexNestedObject_ProducesValidJson()
    {
        var (_, _, helper) = MakeHelper();
        helper.WriteStartObject();
        helper.WriteString("Name", "Alice");
        helper.WriteNumber("Age", 30);
        helper.WriteBoolean("Active", true);
        helper.WriteNull("Extra");
        helper.WritePropertyName("Tags");
        helper.WriteStartArray();
        helper.WriteStringValue("a");
        helper.WriteStringValue("b");
        helper.WriteEndArray();
        helper.WriteEndObject();
        var json = FlushAndRead(helper);

        JsonDocument.Parse(json).Should().NotBeNull();
        json.Should().Contain("\"Name\"").And.Contain("\"Alice\"");
        json.Should().Contain("\"Age\"").And.Contain("30");
        json.Should().Contain("\"Active\"").And.Contain("true");
        json.Should().Contain("\"Extra\"").And.Contain("null");
        json.Should().Contain("\"Tags\"").And.Contain("[\"a\",\"b\"]");
    }
}
