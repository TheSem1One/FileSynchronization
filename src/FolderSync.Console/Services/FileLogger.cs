using FolderSync.Core.Interfaces;

namespace FolderSync.Console.Services;

public class FileLogger : IFileLogger, IDisposable
{
    private readonly string _logFilePath;
    private readonly Lock _lock = new();
    private StreamWriter? _writer;
    private bool _disposed;

    public FileLogger(string logFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);
        _logFilePath = Path.GetFullPath(logFilePath);

        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Open in append mode with UTF-8 encoding
        var stream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var formatted = $"[{timestamp}] {message}";

        lock (_lock)
        {
            System.Console.WriteLine(formatted);

            if (_writer != null && !_disposed)
            {
                try
                {
                    _writer.WriteLine(formatted);
                }
                catch (Exception ex)
                {
                    System.Console.Error.WriteLine($"[FileLogger Error] Failed to write to log file: {ex.Message}");
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }

        GC.SuppressFinalize(this);
    }
}
