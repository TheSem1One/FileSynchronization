using FolderSync.Console;
using Xunit;

namespace FolderSync.Tests;

public class ArgumentParserTests
{
    [Fact]
    public void Parse_WithValidFourArguments_ReturnsSuccess()
    {
        var args = new[] { "source_dir", "replica_dir", "15", "logs/sync.log" };

        var result = ArgumentParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        Assert.Equal(TimeSpan.FromSeconds(15), result.Options.Interval);
        Assert.True(Path.IsPathRooted(result.Options.SourcePath));
        Assert.True(Path.IsPathRooted(result.Options.ReplicaPath));
        Assert.True(Path.IsPathRooted(result.Options.LogFilePath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void Parse_WithInvalidArgumentsCount_ReturnsFailure(int count)
    {
        var args = Enumerable.Range(0, count).Select(i => $"arg{i}").ToArray();

        var result = ArgumentParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains("Expected exactly 4 arguments", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithNullArgs_ReturnsFailure()
    {
        var result = ArgumentParser.Parse(null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Expected exactly 4 arguments", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WithEmptySource_ReturnsFailure(string source)
    {
        var args = new[] { source, "replica", "10", "log.txt" };

        var result = ArgumentParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains("Source path cannot be empty", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WithEmptyReplica_ReturnsFailure(string replica)
    {
        var args = new[] { "source", replica, "10", "log.txt" };

        var result = ArgumentParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains("Replica path cannot be empty", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WithEmptyLogFilePath_ReturnsFailure(string logPath)
    {
        var args = new[] { "source", "replica", "10", logPath };

        var result = ArgumentParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains("Log file path cannot be empty", result.ErrorMessage);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-100")]
    [InlineData("abc")]
    [InlineData("NaN")]
    public void Parse_WithInvalidInterval_ReturnsFailure(string interval)
    {
        var args = new[] { "source", "replica", interval, "log.txt" };

        var result = ArgumentParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains("Interval must be a positive number", result.ErrorMessage);
    }
}
