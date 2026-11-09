using FolderSync.Console.Services;
using FolderSync.Tests.Helpers;
using Xunit;

namespace FolderSync.Tests;

public class FileLoggerTests : IDisposable
{
    private readonly TestDirectory _fixture = new();

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public void Log_WritesFormattedMessageToFile()
    {
        var logFile = Path.Combine(_fixture.Path, "subfolder", "test.log");

        using (var logger = new FileLogger(logFile))
        {
            logger.Log("Test log entry");
        }

        Assert.True(File.Exists(logFile));
        var content = File.ReadAllText(logFile);
        Assert.Contains("Test log entry", content);
        Assert.Matches(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\] Test log entry", content);
    }

    [Fact]
    public void FileLogger_AppendsToExistingLog()
    {
        var logFile = Path.Combine(_fixture.Path, "existing.log");
        File.WriteAllText(logFile, "Previous entry\n");

        using (var logger = new FileLogger(logFile))
        {
            logger.Log("Appended entry");
        }

        var lines = File.ReadAllLines(logFile);
        Assert.Contains("Previous entry", lines[0]);
        Assert.Contains("Appended entry", lines[1]);
    }
}
