using Coral.Cli.Prototypes;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Coral.Cli.Commands;

internal class TestFileWatcherCommand : AsyncCommand<TestFileWatcherCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public TestFileWatcherCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<library-path>")]
        public required string LibraryPath { get; set; }

        [CommandOption("--debounce-seconds <SECONDS>")]
        public int DebounceSeconds { get; set; } = 5;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold yellow]File Watcher Prototype[/]");
        _console.MarkupLine("[dim]Testing debounce logic and event handling[/]");
        _console.WriteLine();

        if (!Directory.Exists(settings.LibraryPath))
        {
            _console.MarkupLine($"[red]ERROR: Directory does not exist: {settings.LibraryPath}[/]");
            return -1;
        }

        _console.MarkupLine($"[blue]Watching:[/] {settings.LibraryPath}");
        _console.MarkupLine($"[blue]Debounce:[/] {settings.DebounceSeconds} seconds");
        _console.WriteLine();

        _console.MarkupLine("[yellow]Events will be logged below. Press Ctrl+C to stop.[/]");
        _console.WriteLine();

        var prototype = new FileWatcherPrototype(
            settings.LibraryPath,
            settings.DebounceSeconds,
            _console);

        prototype.Start();

        // Wait for Ctrl+C
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (TaskCanceledException)
        {
            _console.WriteLine();
            _console.MarkupLine("[yellow]Stopping file watcher...[/]");
        }

        prototype.Stop();

        _console.WriteLine();
        _console.MarkupLine("[green]File watcher stopped.[/]");

        return 0;
    }
}
