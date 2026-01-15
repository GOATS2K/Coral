using Coral.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Coral.Cli.Commands;

internal class DebugEmbeddingsCommand : AsyncCommand<DebugEmbeddingsCommand.Settings>
{
    private readonly IEmbeddingService _embeddingService;
    private readonly InferenceService _inferenceService;
    private readonly IAnsiConsole _console;

    private static readonly string[] AudioExtensions = [".flac", ".mp3", ".m4a", ".ogg", ".opus", ".wav", ".aac"];

    public DebugEmbeddingsCommand(
        IEmbeddingService embeddingService,
        InferenceService inferenceService,
        IAnsiConsole console)
    {
        _embeddingService = embeddingService;
        _inferenceService = inferenceService;
        _console = console;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        public required string Path { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold yellow]Debug Embeddings Command[/]");
        _console.MarkupLine($"[blue]Scanning:[/] {settings.Path}");
        _console.WriteLine();

        if (!Directory.Exists(settings.Path))
        {
            _console.MarkupLine($"[red]Directory not found:[/] {settings.Path}");
            return 1;
        }

        // Initialize services
        await _console.Status()
            .Spinner(Spinner.Known.Arc)
            .StartAsync("Initializing...", async ctx =>
            {
                ctx.Status = "Initializing DuckDB...";
                await _embeddingService.InitializeAsync();
                ctx.Status = "Ensuring inference model exists...";
                await _inferenceService.EnsureModelExists();
            });

        _console.MarkupLine("[green]Services initialized.[/]");
        _console.WriteLine();

        // Find all audio files
        var audioFiles = Directory.GetFiles(settings.Path, "*.*", SearchOption.AllDirectories)
            .Where(f => AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        _console.MarkupLine($"[blue]Found {audioFiles.Count} audio files[/]");
        _console.WriteLine();

        if (audioFiles.Count == 0)
        {
            _console.MarkupLine("[yellow]No audio files found.[/]");
            return 0;
        }

        var succeeded = 0;
        var failed = 0;

        foreach (var file in audioFiles)
        {
            var relativePath = Path.GetRelativePath(settings.Path, file);
            _console.MarkupLine($"[blue]Processing:[/] {relativePath}");

            try
            {
                var embeddings = await _inferenceService.RunInference(file);
                _console.MarkupLine($"  [green]✓ Success[/] - Got {embeddings.Length} dimensions");
                succeeded++;
            }
            catch (Exception ex)
            {
                _console.MarkupLine($"  [red]✗ Failed:[/] {Markup.Escape(ex.Message)}");

                // Test recording the failure (use a fake track ID for testing)
                var fakeTrackId = Guid.NewGuid();
                try
                {
                    await _embeddingService.RecordFailedEmbeddingAsync(fakeTrackId, ex.Message);
                    _console.MarkupLine($"  [yellow]  → Recorded failure for track {fakeTrackId}[/]");

                    // Verify it was recorded
                    var hasFailure = await _embeddingService.HasFailedEmbeddingAsync(fakeTrackId);
                    if (hasFailure)
                    {
                        _console.MarkupLine($"  [green]  → Verified: HasFailedEmbeddingAsync returns true[/]");
                    }
                    else
                    {
                        _console.MarkupLine($"  [red]  → ERROR: HasFailedEmbeddingAsync returned false![/]");
                    }
                }
                catch (Exception recordEx)
                {
                    _console.MarkupLine($"  [red]  → Failed to record failure: {recordEx.Message}[/]");
                }

                failed++;
            }

            _console.WriteLine();
        }

        // Summary
        _console.WriteLine();
        var table = new Table();
        table.Title = new TableTitle("[bold]Results[/]");
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.AddRow("Total files", audioFiles.Count.ToString());
        table.AddRow("[green]Succeeded[/]", succeeded.ToString());
        table.AddRow("[red]Failed[/]", failed.ToString());
        _console.Write(table);

        // Show all failed track IDs in the database
        _console.WriteLine();
        var allFailedIds = await _embeddingService.GetAllFailedTrackIdsAsync();
        _console.MarkupLine($"[blue]Total failed embeddings in database:[/] {allFailedIds.Count}");

        return failed > 0 ? 1 : 0;
    }
}
