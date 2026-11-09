namespace FolderSync.Core.Models;

public class SyncOptions
{
    public string SourcePath { get; init; } = string.Empty;
    public string ReplicaPath { get; init; } = string.Empty;
    public TimeSpan Interval { get; init; }
    public string LogFilePath { get; init; } = string.Empty;
}
