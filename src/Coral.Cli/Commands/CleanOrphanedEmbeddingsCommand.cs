using System.ComponentModel;
using Coral.Configuration;
using Coral.Database;
using Coral.Services;
using DuckDB.NET.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Coral.Cli.Commands;

internal class CleanOrphanedEmbeddingsCommand : AsyncCommand<CleanOrphanedEmbeddingsCommand.Settings>
{
    private readonly CoralDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<CleanOrphanedEmbeddingsCommand> _logger;
    private readonly IAnsiConsole _console;

    public CleanOrphanedEmbeddingsCommand(
        CoralDbContext dbContext,
        IEmbeddingService embeddingService,
        ILogger<CleanOrphanedEmbeddingsCommand> logger,
        IAnsiConsole console)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _logger = logger;
        _console = console;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--dry-run")]
        [DefaultValue(false)]
        public bool DryRun { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {

        _console.MarkupLine("[bold blue]Cleaning Orphaned Embeddings[/]");
        _console.WriteLine();

        try
        {
            // Get all track IDs from DuckDB
            var embeddingTrackIds = await GetAllEmbeddingTrackIds();

            if (embeddingTrackIds.Count == 0)
            {
                _console.MarkupLine("[yellow]No embeddings found in DuckDB[/]");
                return 0;
            }

            _console.MarkupLine($"[cyan]Found {embeddingTrackIds.Count} embeddings in DuckDB[/]");

            // Get all valid track IDs from SQLite
            var validTrackIds = await _dbContext.Tracks
                .Select(t => t.Id)
                .ToListAsync();

            _console.MarkupLine($"[cyan]Found {validTrackIds.Count} tracks in database[/]");

            // Find orphaned embeddings (exist in DuckDB but not in SQLite)
            var validTrackIdSet = new HashSet<Guid>(validTrackIds);
            var orphanedIds = embeddingTrackIds
                .Where(id => !validTrackIdSet.Contains(id))
                .ToList();

            if (orphanedIds.Count == 0)
            {
                _console.MarkupLine("[green]No orphaned embeddings found![/]");
                return 0;
            }

            _console.MarkupLine($"[yellow]Found {orphanedIds.Count} orphaned embeddings[/]");

            if (settings.DryRun)
            {
                _console.MarkupLine("[yellow]Dry run mode - no changes will be made[/]");
                _console.WriteLine();
                _console.MarkupLine("[dim]Orphaned track IDs:[/]");
                foreach (var id in orphanedIds.Take(10))
                {
                    _console.MarkupLine($"[dim]  - {id}[/]");
                }
                if (orphanedIds.Count > 10)
                {
                    _console.MarkupLine($"[dim]  ... and {orphanedIds.Count - 10} more[/]");
                }
            }
            else
            {
                var confirmDelete = _console.Confirm(
                    $"Delete {orphanedIds.Count} orphaned embeddings?",
                    defaultValue: false);

                if (!confirmDelete)
                {
                    _console.MarkupLine("[red]Cancelled[/]");
                    return 0;
                }

                await _console.Progress()
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("Deleting orphaned embeddings", maxValue: orphanedIds.Count);

                        // Delete in batches for better performance
                        const int batchSize = 100;
                        for (int i = 0; i < orphanedIds.Count; i += batchSize)
                        {
                            var batch = orphanedIds.Skip(i).Take(batchSize);
                            await _embeddingService.DeleteEmbeddingsAsync(batch);
                            task.Increment(Math.Min(batchSize, orphanedIds.Count - i));
                        }
                    });

                _console.MarkupLine($"[green]Successfully deleted {orphanedIds.Count} orphaned embeddings[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean orphaned embeddings");
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private async Task<List<Guid>> GetAllEmbeddingTrackIds()
    {
        var connectionString = $"Data Source={Coral.Configuration.ApplicationConfiguration.DuckDbEmbeddingsPath}";

        using var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT track_id FROM track_embeddings";

        var trackIds = new List<Guid>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            // DuckDB stores UUID as Guid type, read it directly
            var trackId = reader.GetGuid(0);
            trackIds.Add(trackId);
        }

        return trackIds;
    }
}