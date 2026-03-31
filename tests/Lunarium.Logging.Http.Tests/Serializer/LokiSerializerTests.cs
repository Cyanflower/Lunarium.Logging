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

namespace Lunarium.Logging.Http.Tests.Serializer;

/// <summary>
/// LokiSerializer Loki Push API v1 格式正确性验证。
/// 策略：JsonDocument.Parse 整体结构，导航到 streams[0]。
/// </summary>
public class LokiSerializerTests
{
    private static readonly Dictionary<string, string> TestLabels = new()
    {
        ["app"] = "myservice",
        ["env"] = "test"
    };

    private static async Task<JsonElement> SerializeAsync(
        IReadOnlyList<LogEntry> entries,
        IReadOnlyDictionary<string, string>? labels = null)
    {
        var serializer = new LokiSerializer(labels ?? TestLabels);
        var content = serializer.Serialize(entries);
        var json = await content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 根结构
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Root_HasStreamsArray()
    {
        var root = await SerializeAsync([EntryFactory.Make()]);
        root.TryGetProperty("streams", out var streams).Should().BeTrue();
        streams.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // stream labels
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Stream_ContainsAllLabels()
    {
        var root = await SerializeAsync([EntryFactory.Make()]);
        var stream = root.GetProperty("streams")[0].GetProperty("stream");
        stream.GetProperty("app").GetString().Should().Be("myservice");
        stream.GetProperty("env").GetString().Should().Be("test");
    }

    [Fact]
    public async Task Stream_EmptyLabels_ProducesEmptyStreamObject()
    {
        var root = await SerializeAsync([EntryFactory.Make()], labels: new Dictionary<string, string>());
        var stream = root.GetProperty("streams")[0].GetProperty("stream");
        stream.EnumerateObject().Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // values
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Values_LengthMatchesEntryCount()
    {
        var root = await SerializeAsync(EntryFactory.MakeBatch(4));
        root.GetProperty("streams")[0].GetProperty("values").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public async Task Values_EachItem_IsTwoElementArray()
    {
        var root = await SerializeAsync([EntryFactory.Make()]);
        var value = root.GetProperty("streams")[0].GetProperty("values")[0];
        value.ValueKind.Should().Be(JsonValueKind.Array);
        value.GetArrayLength().Should().Be(2);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 时间戳
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Timestamp_IsStringType()
    {
        var root = await SerializeAsync([EntryFactory.Make()]);
        var ts = root.GetProperty("streams")[0].GetProperty("values")[0][0];
        ts.ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task Timestamp_IsPositiveNanosecondValue()
    {
        var root = await SerializeAsync([EntryFactory.Make()]);
        var tsStr = root.GetProperty("streams")[0].GetProperty("values")[0][0].GetString()!;
        long.TryParse(tsStr, out long ns).Should().BeTrue();
        ns.Should().BePositive();
    }

    [Fact]
    public async Task Timestamp_SubMillisecondTicks_ContributeToNanoseconds()
    {
        // 同一毫秒内两个不同 tick 偏移的时间戳，纳秒值应不同
        var base1 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, 0, TimeSpan.Zero);
        var base2 = base1.AddTicks(5000); // +500µs，仍在同一毫秒

        var root1 = await SerializeAsync([EntryFactory.Make(timestamp: base1)]);
        var root2 = await SerializeAsync([EntryFactory.Make(timestamp: base2)]);

        var ns1 = long.Parse(root1.GetProperty("streams")[0].GetProperty("values")[0][0].GetString()!);
        var ns2 = long.Parse(root2.GetProperty("streams")[0].GetProperty("values")[0][0].GetString()!);

        ns2.Should().BeGreaterThan(ns1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // log line
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogLine_IsValidJson()
    {
        var root = await SerializeAsync([EntryFactory.Make()]);
        var line = root.GetProperty("streams")[0].GetProperty("values")[0][1].GetString()!;
        var act = () => JsonDocument.Parse(line);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task LogLine_ContainsTimestampAndLevelFields()
    {
        var root = await SerializeAsync([EntryFactory.Make(level: LogLevel.Warning)]);
        var line = root.GetProperty("streams")[0].GetProperty("values")[0][1].GetString()!;
        using var doc = JsonDocument.Parse(line);
        doc.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue();
        doc.RootElement.GetProperty("level").GetString().Should().Be("Warning");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Content-Type
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ContentType_IsApplicationJson()
    {
        var serializer = new LokiSerializer(TestLabels);
        var content = serializer.Serialize([EntryFactory.Make()]);
        content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }
}
