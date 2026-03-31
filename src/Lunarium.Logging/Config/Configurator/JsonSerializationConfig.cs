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

// src/Lunarium.Logging/GlobalConfig/JsonSerializationConfig.cs
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;

namespace Lunarium.Logging.Config.Configurator;

// JSON 序列化全局配置，所有使用 {@Object} 解构的 Writer 共享同一份 JsonSerializerOptions
// Options 采用懒加载 + 双检锁；调用任意 ConfigXxx 后会重置 _options，下次访问时重新构建
internal static class JsonSerializationConfig
{
    private static JsonSerializerOptions? _options;
    private static IJsonTypeInfoResolver? _customResolver;
    private static readonly object _lock = new();

    // true = 保留中文/Emoji 原样（默认）；false = 转义为 \uXXXX
    internal static bool EnableUnsafeRelaxedJsonEscaping { get; private set; } = true;

    internal static bool WriteIndented { get; private set; } = false;

    // 懒加载，首次访问时构建；ConfigXxx 调用后置 null 触发重建
    internal static JsonSerializerOptions Options
    {
        get
        {
            if (_options == null)
            {
                lock (_lock)
                {
                    _options ??= CreateOptions();
                }
            }
            return _options;
        }
    }

    internal static void ConfigUnsafeRelaxedJsonEscaping(bool preserve)
    {
        EnableUnsafeRelaxedJsonEscaping = preserve;
        ResetOptions();
    }

    internal static void ConfigCustomResolver(IJsonTypeInfoResolver resolver)
    {
        _customResolver = resolver;
        ResetOptions();
    }

    internal static void ConfigWriteIndented(bool indented)
    {
        WriteIndented = indented;
        ResetOptions();
    }

    private static void ResetOptions()
    {
        lock (_lock)
        {
            _options = null;
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = WriteIndented,
            // 默认情况下处理循环引用
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };

        // 对于日志输出场景，使用 UnsafeRelaxedJsonEscaping 是安全的：
        // - 日志文件不会被嵌入到 HTML/JS 中执行
        // - 允许所有 Unicode 字符（包括 Emoji、中文等）直接输出
        // - 仅转义 JSON 必须转义的字符（" \ 控制字符）
        if (EnableUnsafeRelaxedJsonEscaping)
        {
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        }
        else
        {
            // 使用默认编码器（会转义非 ASCII 字符）
            options.Encoder = JavaScriptEncoder.Default;
        }

        if (_customResolver != null)
        {
            options.TypeInfoResolverChain.Add(_customResolver);
        }

        return options;
    }
}
