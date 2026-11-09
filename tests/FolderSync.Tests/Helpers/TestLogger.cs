using FolderSync.Core.Interfaces;

namespace FolderSync.Tests.Helpers;

public class TestLogger : IFileLogger
{
    private readonly List<string> _messages = new();
    private readonly Lock _lock = new();

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_lock)
            {
                return _messages.ToList();
            }
        }
    }

    public void Log(string message)
    {
        lock (_lock)
        {
            _messages.Add(message);
        }
    }
}
