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
using Lunarium.Logging.Parser;

namespace Lunarium.Logging.Tests.Parser;

/// <summary>
/// Tests for LogParser — the core message template state-machine parser.
/// All tests are designed to be idempotent because the parser uses a shared
/// static LRU cache (ConcurrentDictionary, max 4096 entries).
/// </summary>
public class LogParserTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static MessageTemplate Parse(string message) =>
        LogParser.ParseMessage(message);

    private static IReadOnlyList<MessageTemplateTokens> Tokens(string message) =>
        Parse(message).MessageTemplateTokens;

    private static TextToken AsText(MessageTemplateTokens t) => (TextToken)t;
    private static PropertyToken AsProp(MessageTemplateTokens t) => (PropertyToken)t;

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Null / Empty → EmptyMessageTemplate
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMessage_NullInput_ReturnsEmptyTemplate()
    {
        var result = LogParser.ParseMessage(null!);
        result.Should().BeSameAs(LogParser.EmptyMessageTemplate);
    }

    [Fact]
    public void ParseMessage_EmptyString_ReturnsEmptyTemplate()
    {
        var result = LogParser.ParseMessage("");
        result.Should().BeSameAs(LogParser.EmptyMessageTemplate);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Pure-text (no placeholders)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMessage_PureText_SingleTextToken()
    {
        var tokens = Tokens("Hello World");
        tokens.Should().HaveCount(1);
        AsText(tokens[0]).Text.Should().Be("Hello World");
    }

    [Fact]
    public void ParseMessage_TrailingOpenBrace_TreatedAsText()
    {
        // "abcd{" — dangling { at end → entire string is a TextToken
        var tokens = Tokens("abcd{");
        tokens.Should().HaveCount(1);
        tokens[0].Should().BeOfType<TextToken>();
    }

    [Fact]
    public void ParseMessage_SingleCloseBrace_TreatedAsText()
    {
        var tokens = Tokens("abcd}");
        tokens.Should().HaveCount(1);
        tokens[0].Should().BeOfType<TextToken>();
    }

    [Fact]
    public void ParseMessage_LooseCloseBrace_MidString_TreatedAsText()
    {
        // "abc } def" — a lone } in the middle is just text
        var tokens = Tokens("abc } def");
        tokens.Should().HaveCount(1);
        AsText(tokens[0]).Text.Should().Be("abc } def");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Escape sequences {{ and }}
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMessage_DoubleBraces_EscapedToSingleBrace()
    {
        // "{{ }}" → "{ }"
        var tokens = Tokens("{{ }}");
        tokens.Should().HaveCount(1);
        AsText(tokens[0]).Text.Should().Be("{ }");
    }

    [Fact]
    public void ParseMessage_DoubleBraceAroundWord_EscapedNotProperty()
    {
        // "{{name}}" → "{name}" as text, not a PropertyToken
        var tokens = Tokens("{{name}}");
        tokens.Should().HaveCount(1);
        tokens[0].Should().BeOfType<TextToken>();
        AsText(tokens[0]).Text.Should().Be("{name}");
    }

    [Fact]
    public void ParseMessage_EscapeInText_LiteralBrace()
    {
        // "value: {{10}}" → "value: {10}"
        var tokens = Tokens("value: {{10}}");
        tokens.Should().HaveCount(1);
        AsText(tokens[0]).Text.Should().Be("value: {10}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Valid property names
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("{Name}", "Name")]
    [InlineData("{_private}", "_private")]
    [InlineData("{v1}", "v1")]
    [InlineData("{A}", "A")]
    [InlineData("{Order.Id}", "Order.Id")]
    [InlineData("{My_Field.Sub}", "My_Field.Sub")]
    public void ParseMessage_ValidPropertyName_ReturnsPropertyToken(string template, string expectedName)
    {
        var tokens = Tokens(template);
        tokens.Should().HaveCount(1);
        var prop = AsProp(tokens[0]);
        prop.PropertyName.Should().Be(expectedName);
        prop.Destructuring.Should().Be(Destructuring.Default);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Invalid property names → fall back to TextToken
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("{}", "{}")]                    // empty property
    [InlineData("{1abc}", "{1abc}")]            // leading digit
    [InlineData("{Order.}", "{Order.}")]        // trailing dot
    [InlineData("{Order..Id}", "{Order..Id}")] // double dot
    [InlineData("{Order.1Id}", "{Order.1Id}")] // dot then digit
    [InlineData("{ab,cd:}", "{ab,cd:}")]        // format/alignment trailing colon
    [InlineData("{abcd:}", "{abcd:}")]          // trailing colon only
    [InlineData("{abcd,}", "{abcd,}")]          // trailing comma only
    public void ParseMessage_InvalidPropertyName_FallsBackToTextToken(string template, string expectedText)
    {
        var tokens = Tokens(template);
        tokens.Should().HaveCount(1);
        tokens[0].Should().BeOfType<TextToken>();
        AsText(tokens[0]).Text.Should().Be(expectedText);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Destructuring prefixes @ and $
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMessage_AtPrefix_Destructure()
    {
        var tokens = Tokens("{@Object}");
        tokens.Should().HaveCount(1);
        var prop = AsProp(tokens[0]);
        prop.PropertyName.Should().Be("Object");
        prop.Destructuring.Should().Be(Destructuring.Destructure);
    }

    [Fact]
    public void ParseMessage_DollarPrefix_Stringify()
    {
        var tokens = Tokens("{$Object}");
        tokens.Should().HaveCount(1);
        var prop = AsProp(tokens[0]);
        prop.PropertyName.Should().Be("Object");
        prop.Destructuring.Should().Be(Destructuring.Stringify);
    }

    [Theory]
    [InlineData("{@}")]   // @ with no name
    [InlineData("{$}")]   // $ with no name
    public void ParseMessage_PrefixWithNoName_FallsBackToTextToken(string template)
    {
        var tokens = Tokens(template);
        tokens.Should().HaveCount(1);
        tokens[0].Should().BeOfType<TextToken>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Alignment
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMessage_RightAlignment_PositiveValue()
    {
        var tokens = Tokens("{Value,10}");
        var prop = AsProp(tokens[0]);
        prop.PropertyName.Should().Be("Value");
        prop.Alignment.Should().Be(10);
        prop.Format.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_LeftAlignment_NegativeValue()
    {
        var tokens = Tokens("{Value,-10}");
        var prop = AsProp(tokens[0]);
        prop.Alignment.Should().Be(-10);
    }

    [Theory]
    [InlineData("{Value,abc}")]        // non-numeric alignment
    [InlineData("{Value, 10}")]        // leading space in alignment
    [InlineData("{Value,10 }")]        // trailing space in alignment
    public void ParseMessage_InvalidAlignment_FallsBackToTextToken(string template)
    {
        var tokens = Tokens(template);
        tokens.Should().HaveCount(1);
        tokens[0].Should().BeOfType<TextToken>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. Format specifier
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMessage_FormatSpecifier_CapturedCorrectly()
    {
        var tokens = Tokens("{Value:F2}");
        var prop = AsProp(tokens[0]);
        prop.PropertyName.Should().Be("Value");
        prop.Format.Should().Be("F2");
        prop.Alignment.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_AlignmentAndFormat_BothCaptured()
    {
        var tokens = Tokens("{Value,10:F2}");
        var prop = AsProp(tokens[0]);
        prop.Alignment.Should().Be(10);
        prop.Format.Should().Be("F2");
    }

    [Theory]
    [InlineData("{Value: F2}")]   // leading space in format
    [InlineData("{Value:F2 }")]   // trailing space in format
    public void ParseMessage_InvalidFormat_FallsBackToTextToken(string template)
    {
        var tokens = Tokens(template);
        tokens.Should().HaveCount(1);
        tokens[0].Should().BeOfType<TextToken>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. Mixed templates — text + multiple properties
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMessage_TextAndProperty_CorrectTokenOrder()
    {
        // "User {Name} logged in" → Text("User ") + Prop("Name") + Text(" logged in")
        var tokens = Tokens("User {Name} logged in");
        tokens.Should().HaveCount(3);
        tokens[0].Should().BeOfType<TextToken>();
        AsText(tokens[0]).Text.Should().Be("User ");
        tokens[1].Should().BeOfType<PropertyToken>();
        AsProp(tokens[1]).PropertyName.Should().Be("Name");
        tokens[2].Should().BeOfType<TextToken>();
        AsText(tokens[2]).Text.Should().Be(" logged in");
    }

    [Fact]
    public void ParseMessage_MultipleProperties_AllCaptured()
    {
        var tokens = Tokens("User {Name} logged in at {Time:O}");
        // Expected: Text + Prop(Name) + Text + Prop(Time)
        tokens.Should().HaveCount(4);
        AsProp(tokens[1]).PropertyName.Should().Be("Name");
        AsProp(tokens[3]).PropertyName.Should().Be("Time");
        AsProp(tokens[3]).Format.Should().Be("O");
    }

    [Fact]
    public void ParseMessage_AdjacentProperties_BothCaptured()
    {
        // "{A}{B}" — no text between properties
        var tokens = Tokens("{A}{B}");
        tokens.Should().HaveCount(2);
        AsProp(tokens[0]).PropertyName.Should().Be("A");
        AsProp(tokens[1]).PropertyName.Should().Be("B");
    }

    [Fact]
    public void ParseMessage_UnclosedBrace_PriorTextAndBraceAsText()
    {
        // "abc { def" — { with no closing } → everything from { onward is text
        var tokens = Tokens("abc { def");
        // "abc " is TextToken, then "{ def" is TextToken (merged adjacent texts)
        tokens.Should().HaveCount(1);
        tokens[0].Should().BeOfType<TextToken>();
        AsText(tokens[0]).Text.Should().Be("abc { def");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. Cache — same template returns same object
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMessage_SameTemplate_ReturnsCachedInstance()
    {
        // Calling twice should return the same MessageTemplate object
        const string template = "Unique_Cache_Test_{Value}";
        var first = Parse(template);
        var second = Parse(template);
        second.Should().BeSameAs(first);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. RawText stored in PropertyToken
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMessage_PropertyToken_RawTextMatchesOriginalBraces()
    {
        var tokens = Tokens("{Name,10:F2}");
        var prop = AsProp(tokens[0]);
        prop.RawText.Text.Should().Be("{Name,10:F2}");
    }

    [Fact]
    public void ParseMessage_PropertyToken_RawText_DestructuringPrefixIncluded()
    {
        var tokens = Tokens("{@Object}");
        var prop = AsProp(tokens[0]);
        // Raw text should contain the @ prefix
        prop.RawText.Text.Should().Be("{@Object}");
    }
    // ─────────────────────────────────────────────────────────────────────────
    // 12. Single-char property name with format / alignment (regression for
    //     the missing-slice bug in the PropertyName → Format/Alignment transition)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMessage_SingleCharProperty_WithFormat_ParsedCorrectly()
    {
        // Regression: {V:F2} previously produced format = "V:F2" (bug)
        // After fix should produce propertyName = "V", format = "F2"
        var tokens = Tokens("{V:F2}");
        tokens.Should().HaveCount(1);
        var prop = AsProp(tokens[0]);
        prop.PropertyName.Should().Be("V");
        prop.Format.Should().Be("F2");
        prop.Alignment.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_SingleCharProperty_WithAlignment_ParsedCorrectly()
    {
        // {V,10} — single char + alignment
        var tokens = Tokens("{V,10}");
        tokens.Should().HaveCount(1);
        var prop = AsProp(tokens[0]);
        prop.PropertyName.Should().Be("V");
        prop.Alignment.Should().Be(10);
        prop.Format.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_SingleCharProperty_WithAlignmentAndFormat_ParsedCorrectly()
    {
        // {V,10:F2} — single char + alignment + format
        var tokens = Tokens("{V,10:F2}");
        tokens.Should().HaveCount(1);
        var prop = AsProp(tokens[0]);
        prop.PropertyName.Should().Be("V");
        prop.Alignment.Should().Be(10);
        prop.Format.Should().Be("F2");
    }

    [Fact]
    public void ParseMessage_SingleCharProperty_NoSuffix_StillWorks()
    {
        // Make sure the fix didn't break the simple {V} case
        var tokens = Tokens("{V}");
        tokens.Should().HaveCount(1);
        var prop = AsProp(tokens[0]);
        prop.PropertyName.Should().Be("V");
        prop.Format.Should().BeNull();
        prop.Alignment.Should().BeNull();
    }
}
