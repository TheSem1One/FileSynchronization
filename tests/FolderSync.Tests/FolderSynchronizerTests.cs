using FolderSync.Core.Services;
using FolderSync.Tests.Helpers;
using Xunit;

namespace FolderSync.Tests;

public class FolderSynchronizerTests : IDisposable
{
    private readonly TestDirectory _fixture = new();
    private readonly TestLogger _logger = new();
    private readonly FolderSynchronizer _synchronizer;

    public FolderSynchronizerTests()
    {
        _synchronizer = new FolderSynchronizer(_logger);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    // Scenario 1: Replica does not exist -> Replica created
    [Fact]
    public void Synchronize_WhenReplicaDoesNotExist_CreatesReplica()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = Path.Combine(_fixture.Path, "non_existent_replica");

        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.True(Directory.Exists(replica));
    }

    // Scenario 2: Source is empty -> Replica is empty
    [Fact]
    public void Synchronize_WhenSourceIsEmpty_ReplicaRemainsEmpty()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.Empty(Directory.EnumerateFileSystemEntries(replica));
    }

    // Scenario 3: Source has a new file -> File is copied
    [Fact]
    public void Synchronize_WhenSourceHasNewFile_CopiesFileToReplica()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        _fixture.CreateFile("source/hello.txt", "Hello, World!");

        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesCopied);

        var replicaFile = Path.Combine(replica, "hello.txt");
        Assert.True(File.Exists(replicaFile));
        Assert.Equal("Hello, World!", File.ReadAllText(replicaFile));
        Assert.Contains(_logger.Messages, m => m.Contains("Copied file: hello.txt"));
    }

    // Scenario 4: Source has nested directories -> Structure reproduced
    [Fact]
    public void Synchronize_WhenSourceHasNestedDirectories_ReproducesDirectoryTree()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        _fixture.CreateFile("source/Docs/Reports/2026/report.txt", "Annual Report Content");
        _fixture.CreateFile("source/Images/logo.png", "PNG bytes");

        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.Equal(2, result.FilesCopied);

        var replicaReport = Path.Combine(replica, "Docs", "Reports", "2026", "report.txt");
        var replicaLogo = Path.Combine(replica, "Images", "logo.png");

        Assert.True(File.Exists(replicaReport));
        Assert.True(File.Exists(replicaLogo));
        Assert.Equal("Annual Report Content", File.ReadAllText(replicaReport));
    }

    // Scenario 5: File content changed -> Replica updated
    [Fact]
    public void Synchronize_WhenFileContentChanged_UpdatesReplica()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        _fixture.CreateFile("source/data.txt", "Initial Version");
        _synchronizer.Synchronize(source, replica);

        // Update file in source
        _fixture.CreateFile("source/data.txt", "Updated Version with More Content");

        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesCopied);

        var replicaFile = Path.Combine(replica, "data.txt");
        Assert.Equal("Updated Version with More Content", File.ReadAllText(replicaFile));
    }

    // Scenario 6: File deleted from Source -> File deleted from Replica
    [Fact]
    public void Synchronize_WhenFileDeletedFromSource_DeletesFileFromReplica()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        var sourceFile = _fixture.CreateFile("source/to_be_deleted.txt", "temporary");
        _synchronizer.Synchronize(source, replica);

        var replicaFile = Path.Combine(replica, "to_be_deleted.txt");
        Assert.True(File.Exists(replicaFile));

        // Delete from source
        File.Delete(sourceFile);

        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesDeleted);
        Assert.False(File.Exists(replicaFile));
        Assert.Contains(_logger.Messages, m => m.Contains("Deleted file: to_be_deleted.txt"));
    }

    // Scenario 7: Directory deleted from Source -> Extra directory deleted from Replica
    [Fact]
    public void Synchronize_WhenDirectoryDeletedFromSource_DeletesDirectoryFromReplica()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        var sourceSubDir = _fixture.CreateSubdirectory("source/OldFolder/Nested");
        _fixture.CreateFile("source/OldFolder/Nested/file.txt", "data");

        _synchronizer.Synchronize(source, replica);

        Assert.True(Directory.Exists(Path.Combine(replica, "OldFolder", "Nested")));

        // Delete OldFolder in source
        Directory.Delete(Path.Combine(source, "OldFolder"), recursive: true);

        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesDeleted);
        Assert.Equal(2, result.DirectoriesDeleted);
        Assert.False(Directory.Exists(Path.Combine(replica, "OldFolder")));
    }

    // Scenario 8: File exists only in Replica -> File deleted
    [Fact]
    public void Synchronize_WhenFileExistsOnlyInReplica_DeletesIt()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        _fixture.CreateFile("replica/rogue_file.txt", "created in replica directly");

        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesDeleted);
        Assert.False(File.Exists(Path.Combine(replica, "rogue_file.txt")));
    }

    // Scenario 9: Source and Replica are identical -> No unnecessary changes
    [Fact]
    public void Synchronize_WhenSourceAndReplicaAreIdentical_PerformsNoOperations()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        _fixture.CreateFile("source/unchanged.txt", "same content");
        _synchronizer.Synchronize(source, replica);

        _logger.Log("--- Next Pass ---");
        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.Equal(0, result.FilesCopied);
        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(0, result.DirectoriesCreated);
        Assert.Equal(0, result.DirectoriesDeleted);
    }

    // Scenario 10: Source and Replica conflict by path -> Validation rejects
    [Fact]
    public void Synchronize_WhenPathsConflict_LogsErrorAndDoesNotSynchronize()
    {
        var source = _fixture.CreateSubdirectory("source");

        var result = _synchronizer.Synchronize(source, source);

        Assert.False(result.Success);
        Assert.True(result.ErrorsCount > 0);
        Assert.Contains(_logger.Messages, m => m.Contains("Validation error"));
    }

    // Scenario 11: No access to file -> Error logged, program does not crash
    [Fact]
    public void Synchronize_WhenFileIsLocked_LogsErrorAndContinues()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        var sourceFile = _fixture.CreateFile("source/locked.txt", "content");
        _fixture.CreateFile("source/unlocked.txt", "another file");

        // Lock the source file exclusively
        using var lockStream = new FileStream(sourceFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = _synchronizer.Synchronize(source, replica);

        // Locked file fails, but unlocked file should succeed
        Assert.False(result.Success);
        Assert.Equal(1, result.FilesCopied); // unlocked.txt copied
        Assert.True(result.ErrorsCount >= 1);
        Assert.True(File.Exists(Path.Combine(replica, "unlocked.txt")));
        Assert.Contains(_logger.Messages, m => m.Contains("Error inspecting file 'locked.txt'") || m.Contains("Error copying file 'locked.txt'"));
    }

    // Scenario 12: Files with same size, but different content (SHA-256 test) -> Replica updated
    [Fact]
    public void Synchronize_WhenFilesHaveSameSizeButDifferentContent_UpdatesReplica()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        // Both contents are 16 bytes, but have different SHA-256 hashes
        var contentA = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var contentB = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 99 };

        _fixture.CreateBinaryFile("source/binary.bin", contentA);
        _fixture.CreateBinaryFile("replica/binary.bin", contentB);

        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesCopied);

        var replicaBytes = File.ReadAllBytes(Path.Combine(replica, "binary.bin"));
        Assert.Equal(contentA, replicaBytes);
    }

    // Scenario 13: Files with identical names in different subdirectories
    [Fact]
    public void Synchronize_WhenIdenticalFileNamesInDifferentDirectories_HandlesCorrectly()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        _fixture.CreateFile("source/FolderA/readme.txt", "Content A");
        _fixture.CreateFile("source/FolderB/readme.txt", "Content B");

        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.Equal(2, result.FilesCopied);
        Assert.Equal("Content A", File.ReadAllText(Path.Combine(replica, "FolderA", "readme.txt")));
        Assert.Equal("Content B", File.ReadAllText(Path.Combine(replica, "FolderB", "readme.txt")));
    }

    // Scenario 14: Source is not modified during or after synchronization
    [Fact]
    public void Synchronize_NeverModifiesSourceDirectoryOrFiles()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        var file1 = _fixture.CreateFile("source/original.txt", "Important data");
        var initialWriteTime = File.GetLastWriteTimeUtc(file1);

        _synchronizer.Synchronize(source, replica);

        Assert.True(File.Exists(file1));
        Assert.Equal("Important data", File.ReadAllText(file1));
        Assert.Equal(initialWriteTime, File.GetLastWriteTimeUtc(file1));
    }

    // Scenario 15: Cancellation token is respected
    [Fact]
    public void Synchronize_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        _fixture.CreateFile("source/file.txt", "content");

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        Assert.Throws<OperationCanceledException>(() =>
        {
            _synchronizer.Synchronize(source, replica, cts.Token);
        });
    }

    // Scenario 16: Identical content with different timestamps is not copied
    [Fact]
    public void Synchronize_WhenFilesHaveIdenticalContent_DoesNotCopyEvenIfTimestampsDiffer()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = _fixture.CreateSubdirectory("replica");

        var sourceFile = _fixture.CreateFile("source/sample.txt", "Same exact content");
        var replicaFile = _fixture.CreateFile("replica/sample.txt", "Same exact content");

        // Alter write time of replica to simulate different timestamp
        File.SetLastWriteTime(replicaFile, DateTime.Now.AddDays(-10));

        var result = _synchronizer.Synchronize(source, replica);

        Assert.True(result.Success);
        Assert.Equal(0, result.FilesCopied);
        Assert.DoesNotContain(_logger.Messages, m => m.Contains("Copied file: sample.txt"));
    }
}
