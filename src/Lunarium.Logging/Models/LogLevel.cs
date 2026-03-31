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

namespace Lunarium.Logging;

/// <summary>
/// Severity levels for log events, ordered from lowest to highest.
/// </summary>
public enum LogLevel
{
    /// <summary>Detailed diagnostic information, typically useful only during development.</summary>
    Debug,
    /// <summary>General informational messages that track normal application flow.</summary>
    Info,
    /// <summary>Potentially harmful situations that deserve attention but do not indicate failure.</summary>
    Warning,
    /// <summary>Error events that may still allow the application to continue running.</summary>
    Error,
    /// <summary>Severe error events that indicate the application cannot continue.</summary>
    Critical
}
