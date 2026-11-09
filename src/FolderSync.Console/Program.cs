using FolderSync.Console;
using FolderSync.Console.Services;
using FolderSync.Core.Services;
using FolderSync.Core.Validation;

// Step 1: Parse arguments
var parseResult = ArgumentParser.Parse(args);
if (!parseResult.IsSuccess)
{
    Console.Error.WriteLine(parseResult.ErrorMessage);
    return 1;
}

var options = parseResult.Options!;

// Step 2: Validate configuration and paths
var validation = SyncOptionsValidator.Validate(options);
if (!validation.IsValid)
{
    Console.Error.WriteLine("Configuration error:");
    foreach (var error in validation.Errors)
    {
        Console.Error.WriteLine($" - {error}");
    }
    return 1;
}

// Step 3: Setup graceful cancellation
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
    Console.WriteLine();
    Console.WriteLine("Shutdown requested. Finishing active synchronization...");
};

// Step 4: Initialize logger and synchronizer
using var logger = new FileLogger(options.LogFilePath);
var synchronizer = new FolderSynchronizer(logger);

logger.Log($"FolderSync service initialized.");
logger.Log($"Source directory:  {options.SourcePath}");
logger.Log($"Replica directory: {options.ReplicaPath}");
logger.Log($"Interval:          {options.Interval.TotalSeconds} seconds");
logger.Log($"Log file:          {options.LogFilePath}");
logger.Log("Press Ctrl+C to stop the synchronization service.");

try
{
    // Execute first synchronization immediately
    synchronizer.Synchronize(options, cts.Token);

    // Periodic synchronization loop
    using var timer = new PeriodicTimer(options.Interval);
    while (await timer.WaitForNextTickAsync(cts.Token))
    {
        synchronizer.Synchronize(options, cts.Token);
    }
}
catch (OperationCanceledException)
{
    logger.Log("Synchronization service stopped by user.");
}
catch (Exception ex)
{
    logger.Log($"Fatal error encountered: {ex.Message}");
    return 1;
}

return 0;
