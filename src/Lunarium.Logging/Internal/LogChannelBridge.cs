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

using System.Threading.Channels;

namespace Lunarium.Logging.Internal;


// 用于在 Logger 和外部消费者之间传递日志的桥接器。
// 封装了一个无界/有界 Channel，并提供对 Reader 和 Writer 的访问，
// 确保生产者和消费者可以安全地解耦。
internal sealed class LogChannelBridge<T>
{
    private readonly Channel<T> _channel;

    // 获取用于读取日志的 ChannelReader
    internal ChannelReader<T> Reader => _channel.Reader;

    // 获取用于写入日志的 ChannelWriter (仅供内部使用)
    internal ChannelWriter<T> Writer => _channel.Writer;

    // 创建一个新的 Channel 桥接器。
    // capacity：Channel 的容量限制。如果为 null，则创建无界 Channel。
    // 如果指定容量，当 Channel 满时，新日志会被丢弃。
    internal LogChannelBridge(int? capacity = null)
    {
        _channel = capacity.HasValue
            ? Channel.CreateBounded<T>(new BoundedChannelOptions(capacity.Value)
            {
                FullMode = BoundedChannelFullMode.DropWrite  // 满时丢弃新消息
            })
            : Channel.CreateUnbounded<T>();
    }
}