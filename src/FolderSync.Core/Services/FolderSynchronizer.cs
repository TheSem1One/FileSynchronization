using System.Security.Cryptography;
using FolderSync.Core.Interfaces;
using FolderSync.Core.Models;
using FolderSync.Core.Validation;

namespace FolderSync.Core.Services;

public class FolderSynchronizer
{
    private readonly IFileLogger _logger;
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public FolderSynchronizer(IFileLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SyncResult Synchronize(SyncOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Synchronize(options.SourcePath, options.ReplicaPath, cancellationToken);
    }

    public Task<SyncResult> SynchronizeAsync(SyncOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => Synchronize(options.SourcePath, options.ReplicaPath, cancellationToken), cancellationToken);
    }

    public SyncResult Synchronize(string sourcePath, string replicaPath, CancellationToken cancellationToken = default)
    {
        _logger.Log("Synchronization started.");

        var errors = new List<string>();

        var validationResult = SyncOptionsValidator.ValidatePaths(sourcePath, replicaPath);
        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
            {
                _logger.Log($"Validation error: {error}");
                errors.Add(error);
            }

            _logger.Log($"Synchronization completed with {errors.Count} error(s).");
            return new SyncResult { Errors = errors };
        }

        var fullSource = Path.GetFullPath(sourcePath);
        var fullReplica = Path.GetFullPath(replicaPath);

        // Ensure replica root directory exists
        try
        {
            if (!Directory.Exists(fullReplica))
            {
                Directory.CreateDirectory(fullReplica);
            }
        }
        catch (Exception ex)
        {
            var msg = $"Error creating replica directory '{fullReplica}': {ex.Message}";
            _logger.Log(msg);
            errors.Add(msg);
            _logger.Log($"Synchronization completed with {errors.Count} error(s).");
            return new SyncResult { Errors = errors };
        }

        // Scan source directory tree
        var (sourceRelDirs, sourceRelFiles, sourceScanFailed) = ScanSource(fullSource, errors, cancellationToken);

        int dirsCreated = 0;
        int filesCopied = 0;
        int filesDeleted = 0;
        int dirsDeleted = 0;

        // Step 2: Create missing directories in replica
        cancellationToken.ThrowIfCancellationRequested();
        dirsCreated = CreateMissingDirectories(fullReplica, sourceRelDirs, errors, cancellationToken);

        // Step 3: Copy new and updated files
        cancellationToken.ThrowIfCancellationRequested();
        filesCopied = CopyNewAndUpdatedFiles(fullSource, fullReplica, sourceRelFiles, errors, cancellationToken);

        // Steps 4 & 5: Delete extra files and directories in replica
        cancellationToken.ThrowIfCancellationRequested();
        if (sourceScanFailed)
        {
            _logger.Log("Deletion phase skipped because source directory structure could not be verified completely.");
        }
        else
        {
            var (replicaRelDirs, replicaRelFiles) = ScanReplica(fullReplica, errors, cancellationToken);

            var sourceFileSet = new HashSet<string>(sourceRelFiles, PathComparer);
            var sourceDirSet = new HashSet<string>(sourceRelDirs, PathComparer);

            filesDeleted = DeleteExtraFiles(fullReplica, replicaRelFiles, sourceFileSet, errors, cancellationToken);
            dirsDeleted = DeleteExtraDirectories(fullReplica, replicaRelDirs, sourceDirSet, errors, cancellationToken);
        }

        if (errors.Count == 0)
        {
            _logger.Log("Synchronization completed.");
        }
        else
        {
            _logger.Log($"Synchronization completed with {errors.Count} error(s).");
        }

        return new SyncResult
        {
            DirectoriesCreated = dirsCreated,
            FilesCopied = filesCopied,
            FilesDeleted = filesDeleted,
            DirectoriesDeleted = dirsDeleted,
            Errors = errors
        };
    }

    private (List<string> RelativeDirs, List<string> RelativeFiles, bool ScanFailed) ScanSource(
        string fullSource,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var relativeDirs = new List<string>();
        var relativeFiles = new List<string>();
        bool scanFailed = false;

        var queue = new Queue<string>();
        queue.Enqueue(fullSource);

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDir = queue.Dequeue();

            try
            {
                var dirInfo = new DirectoryInfo(currentDir);
                if (!string.Equals(currentDir, fullSource, StringComparison.OrdinalIgnoreCase) &&
                    dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    var rel = Path.GetRelativePath(fullSource, currentDir);
                    _logger.Log($"Skipped reparse point directory: {NormalizeRelativePath(rel)}");
                    continue;
                }

                foreach (var subDir in Directory.EnumerateDirectories(currentDir))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var subDirInfo = new DirectoryInfo(subDir);
                    var relSubDir = Path.GetRelativePath(fullSource, subDir);

                    if (subDirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        _logger.Log($"Skipped reparse point directory: {NormalizeRelativePath(relSubDir)}");
                        continue;
                    }

                    relativeDirs.Add(relSubDir);
                    queue.Enqueue(subDir);
                }

                foreach (var file in Directory.EnumerateFiles(currentDir))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileInfo = new FileInfo(file);
                    var relFile = Path.GetRelativePath(fullSource, file);

                    if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        _logger.Log($"Skipped reparse point file: {NormalizeRelativePath(relFile)}");
                        continue;
                    }

                    relativeFiles.Add(relFile);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                var rel = Path.GetRelativePath(fullSource, currentDir);
                var displayPath = rel == "." ? "root" : NormalizeRelativePath(rel);
                var msg = $"Error scanning source directory '{displayPath}': {ex.Message}";
                _logger.Log(msg);
                errors.Add(msg);
                scanFailed = true;
            }
        }

        return (relativeDirs, relativeFiles, scanFailed);
    }

    private (List<string> RelativeDirs, List<string> RelativeFiles) ScanReplica(
        string fullReplica,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var relativeDirs = new List<string>();
        var relativeFiles = new List<string>();

        var queue = new Queue<string>();
        queue.Enqueue(fullReplica);

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDir = queue.Dequeue();

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(currentDir))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rel = Path.GetRelativePath(fullReplica, subDir);
                    relativeDirs.Add(rel);
                    queue.Enqueue(subDir);
                }

                foreach (var file in Directory.EnumerateFiles(currentDir))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rel = Path.GetRelativePath(fullReplica, file);
                    relativeFiles.Add(rel);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                var rel = Path.GetRelativePath(fullReplica, currentDir);
                var displayPath = rel == "." ? "root" : NormalizeRelativePath(rel);
                var msg = $"Error scanning replica directory '{displayPath}': {ex.Message}";
                _logger.Log(msg);
                errors.Add(msg);
            }
        }

        return (relativeDirs, relativeFiles);
    }

    private int CreateMissingDirectories(
        string fullReplica,
        IReadOnlyList<string> sourceRelativeDirs,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        int createdCount = 0;

        foreach (var relativeDir in sourceRelativeDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(relativeDir) || relativeDir == ".")
            {
                continue;
            }

            var targetDir = Path.Combine(fullReplica, relativeDir);

            if (!IsUnderDirectory(targetDir, fullReplica))
            {
                var errorMsg = $"Security warning: target directory path traversal detected for '{relativeDir}'.";
                _logger.Log(errorMsg);
                errors.Add(errorMsg);
                continue;
            }

            try
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    _logger.Log($"Created directory: {NormalizeRelativePath(relativeDir)}");
                    createdCount++;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error creating directory '{NormalizeRelativePath(relativeDir)}': {ex.Message}";
                _logger.Log(errorMsg);
                errors.Add(errorMsg);
            }
        }

        return createdCount;
    }

    private int CopyNewAndUpdatedFiles(
        string fullSource,
        string fullReplica,
        IReadOnlyList<string> sourceRelativeFiles,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        int copiedCount = 0;

        foreach (var relativeFile in sourceRelativeFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceFile = Path.Combine(fullSource, relativeFile);
            var replicaFile = Path.Combine(fullReplica, relativeFile);

            if (!IsUnderDirectory(replicaFile, fullReplica))
            {
                var errorMsg = $"Security warning: target file path traversal detected for '{relativeFile}'.";
                _logger.Log(errorMsg);
                errors.Add(errorMsg);
                continue;
            }

            bool needsCopy = false;

            try
            {
                if (!File.Exists(replicaFile))
                {
                    needsCopy = true;
                }
                else
                {
                    var sourceInfo = new FileInfo(sourceFile);
                    var replicaInfo = new FileInfo(replicaFile);

                    if (sourceInfo.Length != replicaInfo.Length)
                    {
                        needsCopy = true;
                    }
                    else if (!AreFilesContentEqual(sourceFile, replicaFile))
                    {
                        needsCopy = true;
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error inspecting file '{NormalizeRelativePath(relativeFile)}': {ex.Message}";
                _logger.Log(errorMsg);
                errors.Add(errorMsg);
                continue;
            }

            if (!needsCopy)
            {
                continue;
            }

            var targetDirectory = Path.GetDirectoryName(replicaFile)!;
            var tempFileName = $".sync_tmp_{Guid.NewGuid():N}.tmp";
            var tempFilePath = Path.Combine(targetDirectory, tempFileName);

            try
            {
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Copy(sourceFile, tempFilePath, overwrite: true);
                File.Move(tempFilePath, replicaFile, overwrite: true);

                _logger.Log($"Copied file: {NormalizeRelativePath(relativeFile)}");
                copiedCount++;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error copying file '{NormalizeRelativePath(relativeFile)}': {ex.Message}";
                _logger.Log(errorMsg);
                errors.Add(errorMsg);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // Ignore cleanup failure for temporary file
                    }
                }
            }
        }

        return copiedCount;
    }

    private int DeleteExtraFiles(
        string fullReplica,
        IReadOnlyList<string> replicaRelativeFiles,
        HashSet<string> sourceFileSet,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        int deletedCount = 0;

        foreach (var relativeFile in replicaRelativeFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(relativeFile);
            if (fileName.StartsWith(".sync_tmp_") && fileName.EndsWith(".tmp"))
            {
                continue;
            }

            if (!sourceFileSet.Contains(relativeFile))
            {
                var replicaFilePath = Path.Combine(fullReplica, relativeFile);

                try
                {
                    File.Delete(replicaFilePath);
                    _logger.Log($"Deleted file: {NormalizeRelativePath(relativeFile)}");
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Error deleting file '{NormalizeRelativePath(relativeFile)}': {ex.Message}";
                    _logger.Log(errorMsg);
                    errors.Add(errorMsg);
                }
            }
        }

        return deletedCount;
    }

    private int DeleteExtraDirectories(
        string fullReplica,
        IReadOnlyList<string> replicaRelativeDirs,
        HashSet<string> sourceDirSet,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        int deletedCount = 0;

        // Filter out directories that exist in source
        var extraDirs = replicaRelativeDirs
            .Where(d => !string.IsNullOrEmpty(d) && d != "." && !sourceDirSet.Contains(d))
            // Sort bottom-up: deepest subdirectories first
            .OrderByDescending(d => d.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).Length)
            .ThenByDescending(d => d.Length)
            .ToList();

        foreach (var relativeDir in extraDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var replicaDirPath = Path.Combine(fullReplica, relativeDir);

            try
            {
                if (Directory.Exists(replicaDirPath))
                {
                    Directory.Delete(replicaDirPath, recursive: false);
                    _logger.Log($"Deleted directory: {NormalizeRelativePath(relativeDir)}");
                    deletedCount++;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error deleting directory '{NormalizeRelativePath(relativeDir)}': {ex.Message}";
                _logger.Log(errorMsg);
                errors.Add(errorMsg);
            }
        }

        return deletedCount;
    }

    private static bool AreFilesContentEqual(string file1, string file2)
    {
        using var stream1 = new FileStream(file1, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920);
        using var stream2 = new FileStream(file2, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920);
        byte[] hash1 = SHA256.HashData(stream1);
        byte[] hash2 = SHA256.HashData(stream2);
        return CryptographicOperations.FixedTimeEquals(hash1, hash2);
    }

    private static bool IsUnderDirectory(string candidatePath, string parentDirectory)
    {
        var candidateFull = Path.GetFullPath(candidatePath);
        var parentFull = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return candidateFull.StartsWith(parentFull, comparison);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }
}
