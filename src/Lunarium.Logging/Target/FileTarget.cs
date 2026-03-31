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

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using Lunarium.Logging.Writer;

namespace Lunarium.Logging.Target;

/// <summary>
/// Writes log entries to a file with optional size-based and/or daily rotation.
/// Both rotation strategies can be enabled independently; either condition triggers a rotation.
/// UTF-8 bytes from the writer layer are written directly to <see cref="FileStream"/> with no intermediate buffering.
/// </summary>
public sealed class FileTarget : ILogTarget, IJsonTextTarget, ITextTarget
{
    // 进程级路径追踪：防止不同 Logger 的 FileTarget 写入同一文件
    private static readonly ConcurrentDictionary<string, byte> _activePaths =
        new(StringComparer.OrdinalIgnoreCase);

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    // 定时刷新缓冲区
    private readonly Timer _flushTimer;
    private bool _disposed = false;

    // 规范化路径（用于 Dispose 时精确释放）
    private readonly string _normalizedLogFilePath;
    // 路径是否已注册（保证 Dispose 幂等）
    private bool _pathRegistered = false;

    // === 日志配置 ===
    /// <summary>When <see langword="true"/>, entries are rendered as JSON. Defaults to <see langword="false"/>.</summary>
    public bool ToJson { get; init; } = false;
    /// <summary>When <see langword="true"/>, ANSI color codes are included in text output. Defaults to <see langword="false"/>.</summary>
    public bool IsColor { get; init; } = false;
    private TextOutputIncludeConfig _textOutputIncludeConfig;
    private readonly string _logFilePath;
    // 单个文件的最大字节数，-1 表示不限制
    private readonly long _maxFileSize;
    // 是否在新的一天开始时轮转
    private readonly bool _rotateOnNewDay;
    // 最大保留文件数，≤0 表示不限制
    private readonly int _maxFile;

    // === 当前文件状态 ===
    private string? _currentLogFilePath;
    private FileStream? _fileStream;
    // 当前文件字节数（仅在 maxFileSize > 0 时有意义）
    private long _currentFileSize;
    // 当前文件对应的日期（仅在 rotateOnNewDay 时有意义）
    private DateTime? _currentFileDate;

    /// <summary>Initializes a new <see cref="FileTarget"/>.</summary>
    /// <param name="logFilePath">Base path for the log file, e.g. <c>logs/app.log</c>.</param>
    /// <param name="maxFileSizeMB">
    /// Maximum size of a single file in MB. A rotation is triggered when the limit is reached.
    /// ≤0 disables size-based rotation.
    /// </param>
    /// <param name="rotateOnNewDay">
    /// When <see langword="true"/>, a new file is created at the start of each day.
    /// The file name includes the date (daily-only) or a full timestamp (combined with size rotation).
    /// </param>
    /// <param name="maxFile">
    /// Maximum number of log files to retain. ≤0 means unlimited.
    /// At least one rotation strategy must be enabled when this is positive.
    /// </param>
    /// <param name="toJson">When <see langword="true"/>, entries are rendered as JSON. Defaults to <see langword="false"/>.</param>
    /// <param name="isColor">When <see langword="true"/>, ANSI color codes are included. Defaults to <see langword="false"/>.</param>
    /// <param name="textOutputIncludeConfig">Controls which fields are included in text output. <see langword="null"/> uses all-included defaults.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="maxFile"/> is positive but no rotation strategy is enabled.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified path is already in use by another active <see cref="FileTarget"/>.</exception>
    public FileTarget(
        string logFilePath,
        double maxFileSizeMB = 0,
        bool rotateOnNewDay = false,
        int maxFile = 0, bool?
        toJson = false,
        bool? isColor = false,
        TextOutputIncludeConfig? textOutputIncludeConfig = null)
    {
        _logFilePath = logFilePath;
        _maxFileSize = maxFileSizeMB > 0 ? (long)(maxFileSizeMB * 1024 * 1024) : -1;
        _rotateOnNewDay = rotateOnNewDay;
        _maxFile = maxFile;
        ToJson = toJson ?? false;
        IsColor = isColor ?? false;
        _textOutputIncludeConfig = textOutputIncludeConfig ?? new TextOutputIncludeConfig();

        if (_maxFile > 0 && _maxFileSize <= 0 && !_rotateOnNewDay)
        {
            throw new ArgumentException(
                "Invalid configuration: When maxFile is limited, at least one rotation strategy " +
                "(maxFileSizeMB or rotateOnNewDay) must be configured.");
        }

        // 路径规范化 + 进程级注册
        _normalizedLogFilePath = Path.GetFullPath(logFilePath);
        if (!_activePaths.TryAdd(_normalizedLogFilePath, 0))
        {
            throw new InvalidOperationException(
                $"The log file path '{_normalizedLogFilePath}' is already in use by another active FileTarget instance. " +
                "Each file path can only be associated with one FileTarget at a time. " +
                "Dispose the existing FileTarget before creating a new one for the same path.");
        }
        _pathRegistered = true;

        try
        {
            _flushTimer = new Timer(
                callback: FlushTimerCallback,
                state: null,
                dueTime: TimeSpan.FromSeconds(10),
                period: TimeSpan.FromSeconds(10)
            );
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void FlushTimerCallback(object? state)
    {
        try
        {
            lock (_lock)
            {
                if (_disposed) return;
                EnsureFileExists();
                _fileStream?.Flush();
            }
        }
        catch (Exception ex)
        {
            InternalLogger.Error(ex, "Critical error in timer flush");
        }
    }

    /// <inheritdoc/>
    public void UpdateTextOutputIncludeConfig(TextOutputIncludeConfig config)
    {
        // 原子性地更新配置
        Interlocked.Exchange(ref _textOutputIncludeConfig, config);
    }

    /// <inheritdoc/>
    public TextOutputIncludeConfig GetTextOutputIncludeConfig() => _textOutputIncludeConfig;

    /// <summary>Writes a log entry to the file, rotating if necessary before writing.</summary>
    /// <param name="entry">The log entry to write.</param>
    public void Emit(LogEntry entry)
    {
        try
        {
            using LogWriter logWriter = ToJson ? WriterPool.Get<LogJsonWriter>() : WriterPool.Get<LogTextWriter>();
            if (logWriter is ITextTarget textTarget)
            {
                logWriter.Render(entry, _textOutputIncludeConfig);
            }
            else
            {
                logWriter.Render(entry);
            }

            lock (_lock)
            {
                CheckForRotation(entry.Timestamp);

                if (_fileStream == null)
                {
                    InternalLogger.Error("Log Writer FAILED: _fileStream is null");
                    return;
                }

                logWriter.FlushTo(_fileStream);

                // 分级刷新策略
                if (entry.LogLevel >= LogLevel.Error)
                {
                    EnsureFileExists();
                    _fileStream.Flush();
                }
            }

            // 更新当前文件大小
            _currentFileSize = _fileStream?.Position ?? _currentFileSize;
        }
        catch (IOException ex)
        {
            InternalLogger.Error(ex, "Log Writer FAILED: catch IOException:");

            // 恢复
            CloseCurrentWriter();
            OpenFile(entry.Timestamp);

            if (_fileStream != null && _maxFile > 0)
            {
                CleanupOldFiles();
            }
        }
    }

    // 检查目标文件是否还存在，若丢失则重新打开
    private void EnsureFileExists()
    {
        if (_fileStream == null)
        {
            OpenFile(DateTimeOffset.Now);
            return;
        }

        try
        {
            _ = _fileStream.Position;

            if (!File.Exists(_currentLogFilePath))
            {
                CloseCurrentWriter();
                OpenFile(DateTimeOffset.Now);
            }
        }
        catch
        {
            CloseCurrentWriter();
            OpenFile(DateTimeOffset.Now);
        }
    }

    // 根据已启用的轮转策略检查是否需要轮转；满足任一条件即触发
    private void CheckForRotation(DateTimeOffset timestamp)
    {
        if (_fileStream == null)
        {
            OpenFile(timestamp);
            return;
        }

        bool shouldRotate = false;

        if (_rotateOnNewDay && timestamp.Date != _currentFileDate)
            shouldRotate = true;

        if (!shouldRotate && _maxFileSize > 0 && _currentFileSize >= _maxFileSize)
            shouldRotate = true;

        if (shouldRotate)
        {
            CloseCurrentWriter();
            OpenFile(timestamp);

            if (_fileStream != null && _maxFile > 0)
            {
                CleanupOldFiles();
            }
        }
    }

    // 根据当前轮转策略打开（或复用）一个日志文件
    private void OpenFile(DateTimeOffset timestamp)
    {
        try
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string currentFilePath;

            if (_maxFileSize <= 0 && !_rotateOnNewDay)
            {
                // 无轮转：始终使用同一个文件
                currentFilePath = _logFilePath;
            }
            else if (_rotateOnNewDay && _maxFileSize <= 0)
            {
                // 纯按天：文件名含日期，例如 app-2026-03-10.log
                var fileName = Path.GetFileNameWithoutExtension(_logFilePath);
                var extension = Path.GetExtension(_logFilePath);
                var dateString = timestamp.ToString("yyyy-MM-dd");
                currentFilePath = Path.Combine(directory ?? "", $"{fileName}-{dateString}{extension}");
            }
            else
            {
                // 按大小，或按大小+按天叠加：使用完整时间戳文件名
                // 叠加模式下限制只搜索当天的文件，跨天时强制新建
                currentFilePath = FindLatestLogFileOrCreateNew(timestamp);
            }

            _fileStream = new FileStream(
                currentFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read | FileShare.Delete,
                bufferSize: 4096,
                FileOptions.None);

            _currentFileSize = File.Exists(currentFilePath) ? new FileInfo(currentFilePath).Length : 0;
            _currentLogFilePath = currentFilePath;
            _currentFileDate = timestamp.Date;
        }
        catch (Exception ex)
        {
            InternalLogger.Error(ex, "Failed to open log file:");
            _fileStream = null;
        }
    }

    // 查找最新的且未达到大小限制的日志文件。如果找不到，则生成一个带时间戳的新文件名。
    // 在按大小+按天叠加模式下，只检索当天的文件。
    private string FindLatestLogFileOrCreateNew(DateTimeOffset timestamp)
    {
        var directory = Path.GetDirectoryName(_logFilePath);
        var fileName = Path.GetFileNameWithoutExtension(_logFilePath);
        var extension = Path.GetExtension(_logFilePath);

        var pattern = new Regex(
            $@"^{Regex.Escape(fileName)}-\d{{4}}-\d{{2}}-\d{{2}}-\d{{2}}-\d{{2}}-\d{{2}}{Regex.Escape(extension)}$");

        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            var query = Directory.GetFiles(directory, $"{fileName}-*{extension}")
                .Where(f => pattern.IsMatch(Path.GetFileName(f)));

            // 按大小+按天叠加时，只考虑当天的文件，避免复用前一天的未满文件
            if (_rotateOnNewDay)
            {
                var todayPrefix = $"{fileName}-{timestamp:yyyy-MM-dd}";
                query = query.Where(f => Path.GetFileName(f).StartsWith(todayPrefix));
            }

            var latestFile = query
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.Name)
                .FirstOrDefault();

            if (latestFile != null && latestFile.Length < _maxFileSize)
            {
                return latestFile.FullName;
            }
        }

        var ts = timestamp.ToString("yyyy-MM-dd-HH-mm-ss");
        return Path.Combine(directory ?? "", $"{fileName}-{ts}{extension}");
    }

    // 清理旧的日志文件，保留最新的 _maxFile 个
    private void CleanupOldFiles()
    {
        try
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;

            var fileName = Path.GetFileNameWithoutExtension(_logFilePath);
            var extension = Path.GetExtension(_logFilePath);

            // 纯按天：匹配 yyyy-MM-dd；按大小或叠加：匹配完整时间戳
            var pattern = (_rotateOnNewDay && _maxFileSize <= 0)
                ? new Regex($@"^{Regex.Escape(fileName)}-\d{{4}}-\d{{2}}-\d{{2}}{Regex.Escape(extension)}$")
                : new Regex($@"^{Regex.Escape(fileName)}-\d{{4}}-\d{{2}}-\d{{2}}-\d{{2}}-\d{{2}}-\d{{2}}{Regex.Escape(extension)}$");

            var files = Directory.GetFiles(directory, $"{fileName}-*{extension}")
                .Where(f => pattern.IsMatch(Path.GetFileName(f)))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.Name)
                .ToList();

            if (files.Count > _maxFile)
            {
                foreach (var file in files.Skip(_maxFile))
                {
                    file.Delete();
                }
            }
        }
        catch (Exception ex)
        {
            InternalLogger.Error(ex, "Failed to clean up old log files");
        }
    }

    // 安全地关闭并释放当前的 FileStream
    private void CloseCurrentWriter()
    {
        _fileStream?.Flush();
        _fileStream?.Dispose();
        _fileStream = null;
    }

    /// <summary>Releases all resources held by this target, flushing and closing the file stream.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _flushTimer?.Change(Timeout.Infinite, 0);
            _flushTimer?.Dispose();

            CloseCurrentWriter();

            _disposed = true;

            if (_pathRegistered)
            {
                _activePaths.TryRemove(_normalizedLogFilePath, out _);
                _pathRegistered = false;
            }
        }
    }
}
