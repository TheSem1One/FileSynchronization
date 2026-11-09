namespace FolderSync.Core.Models;

public record SyncResult
{
    public bool Success => ErrorsCount == 0;
    public int FilesCopied { get; init; }
    public int FilesDeleted { get; init; }
    public int DirectoriesCreated { get; init; }
    public int DirectoriesDeleted { get; init; }
    public int ErrorsCount => Errors.Count;
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
