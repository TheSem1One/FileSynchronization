using FolderSync.Core.Models;

namespace FolderSync.Console;

public record ArgumentParseResult(bool IsSuccess, SyncOptions? Options, string? ErrorMessage)
{
    public static ArgumentParseResult Success(SyncOptions options) => new(true, options, null);
    public static ArgumentParseResult Failure(string errorMessage) => new(false, null, errorMessage);
}

public static class ArgumentParser
{
    public const string UsageMessage =
        "Usage: FolderSync <source-path> <replica-path> <interval-seconds> <log-file-path>\n" +
        "Example: FolderSync ./source ./replica 10 ./logs/sync.log";

    public static ArgumentParseResult Parse(string[]? args)
    {
        if (args == null || args.Length != 4)
        {
            var count = args?.Length ?? 0;
            return ArgumentParseResult.Failure(
                $"Error: Expected exactly 4 arguments, but received {count}.\n{UsageMessage}");
        }

        var sourcePath = args[0]?.Trim();
        var replicaPath = args[1]?.Trim();
        var intervalStr = args[2]?.Trim();
        var logFilePath = args[3]?.Trim();

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return ArgumentParseResult.Failure($"Error: Source path cannot be empty.\n{UsageMessage}");
        }

        if (string.IsNullOrWhiteSpace(replicaPath))
        {
            return ArgumentParseResult.Failure($"Error: Replica path cannot be empty.\n{UsageMessage}");
        }

        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            return ArgumentParseResult.Failure($"Error: Log file path cannot be empty.\n{UsageMessage}");
        }

        if (!double.TryParse(intervalStr, System.Globalization.CultureInfo.InvariantCulture, out double intervalSeconds) ||
            intervalSeconds <= 0 ||
            double.IsNaN(intervalSeconds) ||
            double.IsInfinity(intervalSeconds))
        {
            return ArgumentParseResult.Failure(
                $"Error: Interval must be a positive number of seconds (received '{intervalStr}').\n{UsageMessage}");
        }

        try
        {
            _ = Path.GetFullPath(sourcePath);
        }
        catch (Exception ex)
        {
            return ArgumentParseResult.Failure($"Error: Source path is invalid: {ex.Message}");
        }

        try
        {
            _ = Path.GetFullPath(replicaPath);
        }
        catch (Exception ex)
        {
            return ArgumentParseResult.Failure($"Error: Replica path is invalid: {ex.Message}");
        }

        try
        {
            _ = Path.GetFullPath(logFilePath);
        }
        catch (Exception ex)
        {
            return ArgumentParseResult.Failure($"Error: Log file path is invalid: {ex.Message}");
        }

        var options = new SyncOptions
        {
            SourcePath = Path.GetFullPath(sourcePath),
            ReplicaPath = Path.GetFullPath(replicaPath),
            Interval = TimeSpan.FromSeconds(intervalSeconds),
            LogFilePath = Path.GetFullPath(logFilePath)
        };

        return ArgumentParseResult.Success(options);
    }
}
