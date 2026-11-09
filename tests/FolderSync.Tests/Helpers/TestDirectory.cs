namespace FolderSync.Tests.Helpers;

public sealed class TestDirectory : IDisposable
{
    public string Path { get; }

    public TestDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FolderSync_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string CreateSubdirectory(string relativePath)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public string CreateFile(string relativePath, string content = "test content")
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public string CreateBinaryFile(string relativePath, byte[] bytes)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllBytes(fullPath, bytes);
        return fullPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for test temporary directories
            }
        }
    }
}
