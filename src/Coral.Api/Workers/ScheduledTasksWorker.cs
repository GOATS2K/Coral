using Coral.Configuration;
using Coral.Configuration.Models;
using Coral.Database;
using Coral.Services.ChannelWrappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Coral.Api.Workers;

public class ScheduledTasksWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<ScheduledTasksWorker> _logger;
    private readonly ScheduledTaskSettings _settings;

    public ScheduledTasksWorker(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime appLifetime,
        IOptions<ServerConfiguration> config,
        ILogger<ScheduledTasksWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _appLifetime = appLifetime;
        _logger = logger;
        _settings = config.Value.ScheduledTasks;

        // Register cleanup on application stopping
        _appLifetime.ApplicationStopping.Register(CleanupHlsDirectory);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledTasksWorker started");

        // Wait briefly for other services to initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // Trigger initial full library scan on startup if enabled
        if (_settings.ScanOnStartup)
        {
            await TriggerFullLibraryScan(stoppingToken);
        }
        else
        {
            _logger.LogInformation("Startup library scan is disabled");
        }

        // Start periodic timer for scheduled scans if enabled
        if (_settings.LibraryScanIntervalMinutes <= 0)
        {
            _logger.LogInformation("Periodic library scans are disabled");
            return;
        }

        var scanInterval = TimeSpan.FromMinutes(_settings.LibraryScanIntervalMinutes);
        _logger.LogInformation("Periodic library scans enabled with interval: {Interval}", scanInterval);

        using var timer = new PeriodicTimer(scanInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await TriggerFullLibraryScan(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled library scan");
            }
        }

        _logger.LogInformation("ScheduledTasksWorker stopped");
    }

    private async Task TriggerFullLibraryScan(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();
        var scanChannel = scope.ServiceProvider.GetRequiredService<IScanChannel>();

        var libraries = await context.MusicLibraries.ToListAsync(cancellationToken);

        if (libraries.Count == 0)
        {
            _logger.LogInformation("No music libraries configured, skipping scheduled scan");
            return;
        }

        _logger.LogInformation("Triggering scheduled full scan for {Count} libraries", libraries.Count);

        foreach (var library in libraries)
        {
            var job = new ScanJob(
                Library: library,
                Type: ScanType.Index,
                Incremental: false,
                Trigger: ScanTrigger.Scheduled
            );

            await scanChannel.GetWriter().WriteAsync(job, cancellationToken);
            _logger.LogInformation("Queued scheduled scan for library: {LibraryPath}", library.LibraryPath);
        }
    }

    private void CleanupHlsDirectory()
    {
        try
        {
            var hlsDirectory = ApplicationConfiguration.HLSDirectory;

            if (!Directory.Exists(hlsDirectory))
            {
                _logger.LogInformation("HLS directory does not exist, nothing to clean up");
                return;
            }

            _logger.LogInformation("Cleaning up HLS directory: {Directory}", hlsDirectory);

            // Delete all files and subdirectories
            foreach (var file in Directory.GetFiles(hlsDirectory))
            {
                File.Delete(file);
            }

            foreach (var dir in Directory.GetDirectories(hlsDirectory))
            {
                Directory.Delete(dir, recursive: true);
            }

            _logger.LogInformation("HLS directory cleaned up successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up HLS directory");
        }
    }
}
