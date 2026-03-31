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
using Lunarium.Logging.InternalLoggerUtils;
using System.Text.RegularExpressions;
using Xunit;

namespace Lunarium.Logging.Tests.Core;

public class InternalLoggerTests
{
    [Fact]
    public void InternalLogger_AllOverloads_WriteToFileAndDoNotCrash()
    {
        // 1. Error(string message)
        InternalLogger.Error("Test message 1");
        
        // 2. Error(Exception exception)
        InternalLogger.Error(new InvalidOperationException("Test exception 2"));
        
        // 3. Error(Exception exception, string message)
        InternalLogger.Error(new InvalidOperationException("Test exception 3"), "Test message 3");
        
        // 4. Error(string message, Exception exception)
        InternalLogger.Error("Test message 4", new InvalidOperationException("Test exception 4"));

        // Verify the file was created and contains the logs.
        string dateStamp = DateTimeOffset.Now.ToString("yyyyMMdd");
        string fileName = $"LunariumLogger-internal-{dateStamp}.log";
        string filePath = Path.Combine(AppContext.BaseDirectory, fileName);
        
        File.Exists(filePath).Should().BeTrue();
        
        var content = File.ReadAllText(filePath);
        content.Should().Contain("Test message 1");
        content.Should().Contain("Test exception 2");
        content.Should().Contain("Test message 3");
        content.Should().Contain("Test message 4");
    }
}
