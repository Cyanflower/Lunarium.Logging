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

global using FluentAssertions;
global using Xunit;
global using Lunarium.Logging;
global using Lunarium.Logging.Models;
global using Lunarium.Logging.Parser;
global using Lunarium.Logging.Http.Serializer;
global using Lunarium.Logging.Http.Target;
global using Lunarium.Logging.Http.Config;
global using Lunarium.Logging.Http;
global using Lunarium.Logging.Http.Tests.Helpers;
global using System.Net;
global using System.Net.Http;
global using System.Text.Json;
