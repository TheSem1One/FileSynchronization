using FolderSync.Core.Models;

namespace FolderSync.Core.Validation;

public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success() => new(true, Array.Empty<string>());
    public static ValidationResult Failure(params string[] errors) => new(false, errors);
    public static ValidationResult Failure(IEnumerable<string> errors) => new(false, errors.ToArray());

    public string ErrorMessage => string.Join(Environment.NewLine, Errors);
}

public static class SyncOptionsValidator
{
    public static ValidationResult Validate(SyncOptions options)
    {
        var errors = new List<string>();

        if (options == null)
        {
            return ValidationResult.Failure("Options cannot be null.");
        }

        if (options.Interval <= TimeSpan.Zero)
        {
            errors.Add("Synchronization interval must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.LogFilePath))
        {
            errors.Add("Log file path cannot be empty.");
        }
        else
        {
            try
            {
                _ = Path.GetFullPath(options.LogFilePath);
            }
            catch (Exception ex)
            {
                errors.Add($"Invalid log file path: {ex.Message}");
            }
        }

        var pathResult = ValidatePaths(options.SourcePath, options.ReplicaPath);
        if (!pathResult.IsValid)
        {
            errors.AddRange(pathResult.Errors);
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }

    public static ValidationResult ValidatePaths(string sourcePath, string replicaPath)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            errors.Add("Source path cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(replicaPath))
        {
            errors.Add("Replica path cannot be empty.");
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Failure(errors);
        }

        string fullSource;
        string fullReplica;

        try
        {
            fullSource = Path.GetFullPath(sourcePath);
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Invalid source path: {ex.Message}");
        }

        try
        {
            fullReplica = Path.GetFullPath(replicaPath);
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Invalid replica path: {ex.Message}");
        }

        if (!Directory.Exists(fullSource))
        {
            errors.Add($"Source directory does not exist or is not a directory: '{fullSource}'.");
            return ValidationResult.Failure(errors);
        }

        if (File.Exists(fullReplica))
        {
            errors.Add($"Replica path points to an existing file: '{fullReplica}'.");
            return ValidationResult.Failure(errors);
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var sTrimmed = Path.TrimEndingDirectorySeparator(fullSource);
        var rTrimmed = Path.TrimEndingDirectorySeparator(fullReplica);

        if (string.Equals(sTrimmed, rTrimmed, comparison))
        {
            errors.Add("Source and Replica cannot refer to the same directory.");
            return ValidationResult.Failure(errors);
        }

        var sWithSeparator = sTrimmed + Path.DirectorySeparatorChar;
        var rWithSeparator = rTrimmed + Path.DirectorySeparatorChar;

        if (rWithSeparator.StartsWith(sWithSeparator, comparison))
        {
            errors.Add("Replica directory cannot be located inside the Source directory.");
        }

        if (sWithSeparator.StartsWith(rWithSeparator, comparison))
        {
            errors.Add("Source directory cannot be located inside the Replica directory.");
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}
