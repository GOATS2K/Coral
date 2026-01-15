using System.Diagnostics;
using Coral.Database;
using Coral.Services;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Coral.Cli.Commands;

internal class EmbeddingsCommand : AsyncCommand<EmbeddingsCommand.Settings>
{
    private readonly IEmbeddingService _embeddingService;
    private readonly InferenceService _inferenceService;
    private readonly CoralDbContext _dbContext;
    private readonly IAnsiConsole _console;

    public EmbeddingsCommand(
        IEmbeddingService embeddingService,
        InferenceService inferenceService,
        CoralDbContext dbContext,
        IAnsiConsole console)
    {
        _embeddingService = embeddingService;
        _inferenceService = inferenceService;
        _dbContext = dbContext;
        _console = console;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--concurrency")]
        public int Concurrency { get; set; } = 12;

        [CommandOption("-s|--skip-existing")]
        public bool SkipExisting { get; set; } = true;

        [CommandOption("--retry-failed")]
        public bool RetryFailed { get; set; } = false;

        [CommandOption("--min-duration")]
        public int MinDurationSeconds { get; set; } = 60;

        [CommandOption("--max-duration")]
        public int MaxDurationSeconds { get; set; } = 900; // 15 minutes
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold yellow]Embedding Generation[/]");
        _console.WriteLine();

        // Initialize DuckDB
        await _console.Status()
            .Spinner(Spinner.Known.Arc)
            .StartAsync("Initializing DuckDB embeddings database...", async ctx =>
            {
                await _embeddingService.InitializeAsync();
                ctx.Status = "Ensuring inference model exists...";
                await _inferenceService.EnsureModelExists();
            });

        _console.MarkupLine("[green]Database and model ready.[/]");
        _console.WriteLine();

        // Get all tracks from the library
        var tracks = await _dbContext.Tracks
            .Include(track => track.AudioFile)
            .ToListAsync();

        _console.MarkupLine($"[blue]Found {tracks.Count} tracks to process.[/]");
        _console.WriteLine();

        // Filter tracks by duration
        var eligibleTracks = tracks
            .Where(t => t.DurationInSeconds >= settings.MinDurationSeconds &&
                       t.DurationInSeconds <= settings.MaxDurationSeconds)
            .ToList();

        _console.MarkupLine($"[blue]{eligibleTracks.Count} tracks meet duration criteria ({settings.MinDurationSeconds}s - {settings.MaxDurationSeconds}s)[/]");

        // Get failed track IDs if retrying
        HashSet<Guid> failedTrackIds = new();
        if (settings.RetryFailed)
        {
            failedTrackIds = await _embeddingService.GetAllFailedTrackIdsAsync();
            _console.MarkupLine($"[blue]{failedTrackIds.Count} previously failed tracks will be retried[/]");
        }

        // Check for existing embeddings if skip is enabled
        if (settings.SkipExisting)
        {
            var existingEmbeddings = await _embeddingService.GetAllTrackIdsWithEmbeddingsAsync();
            var tracksToProcess = eligibleTracks
                .Where(t => !existingEmbeddings.Contains(t.Id) || failedTrackIds.Contains(t.Id))
                .ToList();
            eligibleTracks = tracksToProcess;
            _console.MarkupLine($"[blue]{eligibleTracks.Count} tracks need embeddings[/]");
        }

        // Clear failed embeddings for tracks we're about to retry
        if (settings.RetryFailed && failedTrackIds.Count > 0)
        {
            var trackIdsToRetry = eligibleTracks.Select(t => t.Id).Where(failedTrackIds.Contains).ToList();
            if (trackIdsToRetry.Count > 0)
            {
                await _embeddingService.ClearFailedEmbeddingsAsync(trackIdsToRetry);
                _console.MarkupLine($"[blue]Cleared {trackIdsToRetry.Count} failed embedding records[/]");
            }
        }

        if (eligibleTracks.Count == 0)
        {
            _console.MarkupLine("[yellow]No tracks to process![/]");
            return 0;
        }

        _console.WriteLine();

        // Process embeddings
        var embeddingsProcessed = 0;
        var embeddingsFailed = 0;
        var stopwatch = Stopwatch.StartNew();
        var semaphore = new SemaphoreSlim(settings.Concurrency);

        await _console.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Generating embeddings[/]", maxValue: eligibleTracks.Count);

                var tasks = eligibleTracks.Select(async track =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var embeddings = await _inferenceService.RunInference(track.AudioFile.FilePath);
                        await _embeddingService.InsertEmbeddingAsync(track.Id, embeddings);
                        Interlocked.Increment(ref embeddingsProcessed);
                    }
                    catch (Exception ex)
                    {
                        var fileName = Markup.Escape(Path.GetFileName(track.AudioFile.FilePath));
                        var errorMsg = Markup.Escape(ex.Message);
                        _console.MarkupLine($"[red]ERROR:[/] {fileName}: {errorMsg}");
                        Interlocked.Increment(ref embeddingsFailed);
                    }
                    finally
                    {
                        semaphore.Release();
                        task.Increment(1);

                        var total = embeddingsProcessed + embeddingsFailed;
                        if (total % 10 == 0)
                        {
                            var elapsed = stopwatch.Elapsed.TotalSeconds;
                            var rate = embeddingsProcessed / elapsed;
                            task.Description = $"[yellow]Generating embeddings[/] ({rate:F2} per sec)";
                        }
                    }
                });

                await Task.WhenAll(tasks);
                task.StopTask();
            });

        stopwatch.Stop();

        // Print results
        _console.WriteLine();
        var resultTable = new Table();
        resultTable.Title = new TableTitle("[bold green]Embedding Generation Complete[/]");
        resultTable.AddColumn("Metric");
        resultTable.AddColumn("Value");

        resultTable.AddRow("Total time", $"{stopwatch.Elapsed.TotalSeconds:F2} seconds");
        resultTable.AddRow("Embeddings processed", embeddingsProcessed.ToString());
        resultTable.AddRow("Embeddings failed", embeddingsFailed.ToString());

        if (embeddingsProcessed > 0)
        {
            resultTable.AddRow("Average time per embedding", $"{stopwatch.Elapsed.TotalSeconds / embeddingsProcessed:F2}s");
            resultTable.AddRow("Embeddings per second", $"{embeddingsProcessed / stopwatch.Elapsed.TotalSeconds:F2}");
        }

        _console.Write(resultTable);

        return embeddingsFailed > 0 ? 1 : 0;
    }
}