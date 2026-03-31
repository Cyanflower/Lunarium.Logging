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

public class DelegateHttpLogSerializerTests
{
    [Fact]
    public void Serialize_DelegateInvoked_WithCorrectEntries()
    {
        IReadOnlyList<LogEntry>? received = null;
        var serializer = new DelegateHttpLogSerializer(entries =>
        {
            received = entries;
            return new ByteArrayContent([]);
        });

        var entries = EntryFactory.MakeBatch(2);
        serializer.Serialize(entries);

        received.Should().BeSameAs(entries);
    }

    [Fact]
    public void Serialize_ReturnsContentFromDelegate()
    {
        var expected = new ByteArrayContent([1, 2, 3]);
        var serializer = new DelegateHttpLogSerializer(_ => expected);
        var result = serializer.Serialize([EntryFactory.Make()]);
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public void ContentType_UsesProvidedValue()
    {
        var serializer = new DelegateHttpLogSerializer(_ => new ByteArrayContent([]), "text/plain");
        serializer.ContentType.Should().Be("text/plain");
    }

    [Fact]
    public void ContentType_Default_IsApplicationJson()
    {
        var serializer = new DelegateHttpLogSerializer(_ => new ByteArrayContent([]));
        serializer.ContentType.Should().Be("application/json");
    }
}
