using FolderSync.Core.Models;
using FolderSync.Core.Validation;
using FolderSync.Tests.Helpers;
using Xunit;

namespace FolderSync.Tests;

public class SyncOptionsValidatorTests : IDisposable
{
    private readonly TestDirectory _fixture = new();

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = Path.Combine(_fixture.Path, "replica");
        var log = Path.Combine(_fixture.Path, "sync.log");

        var options = new SyncOptions
        {
            SourcePath = source,
            ReplicaPath = replica,
            Interval = TimeSpan.FromSeconds(10),
            LogFilePath = log
        };

        var result = SyncOptionsValidator.Validate(options);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenSourceDoesNotExist_ReturnsFailure()
    {
        var nonExistentSource = Path.Combine(_fixture.Path, "non_existent_folder");
        var replica = Path.Combine(_fixture.Path, "replica");

        var result = SyncOptionsValidator.ValidatePaths(nonExistentSource, replica);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Source directory does not exist"));
    }

    [Fact]
    public void Validate_WhenSourceAndReplicaAreSame_ReturnsFailure()
    {
        var source = _fixture.CreateSubdirectory("same_folder");

        var result = SyncOptionsValidator.ValidatePaths(source, source);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("cannot refer to the same directory"));
    }

    [Fact]
    public void Validate_WhenReplicaIsInsideSource_ReturnsFailure()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replicaInsideSource = Path.Combine(source, "nested_replica");

        var result = SyncOptionsValidator.ValidatePaths(source, replicaInsideSource);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Replica directory cannot be located inside the Source directory"));
    }

    [Fact]
    public void Validate_WhenSourceIsInsideReplica_ReturnsFailure()
    {
        var replica = _fixture.CreateSubdirectory("replica");
        var sourceInsideReplica = _fixture.CreateSubdirectory("replica/nested_source");

        var result = SyncOptionsValidator.ValidatePaths(sourceInsideReplica, replica);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Source directory cannot be located inside the Replica directory"));
    }

    [Fact]
    public void Validate_WhenReplicaIsExistingFile_ReturnsFailure()
    {
        var source = _fixture.CreateSubdirectory("source");
        var fileReplica = _fixture.CreateFile("replica_file.txt", "content");

        var result = SyncOptionsValidator.ValidatePaths(source, fileReplica);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Replica path points to an existing file"));
    }

    [Fact]
    public void Validate_WhenIntervalIsZeroOrNegative_ReturnsFailure()
    {
        var source = _fixture.CreateSubdirectory("source");
        var replica = Path.Combine(_fixture.Path, "replica");

        var options = new SyncOptions
        {
            SourcePath = source,
            ReplicaPath = replica,
            Interval = TimeSpan.Zero,
            LogFilePath = "log.txt"
        };

        var result = SyncOptionsValidator.Validate(options);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("interval must be greater than zero"));
    }
}
