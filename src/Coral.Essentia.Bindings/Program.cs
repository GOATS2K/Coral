using System.Diagnostics;
using Coral.Essentia.Bindings;
using Microsoft.Extensions.Logging;

var modelPath = @"C:\Users\bootie-\Downloads\discogs_track_embeddings-effnet-bs64-1.pb";

/*var tracks = Directory.GetFiles(@"C:\Music", "*.*", SearchOption.AllDirectories)
    .Take(500)
    .Where(c => Path.GetExtension(c) == ".m4a")
    .Select(c => new ATL.Track(c))
    .Where(c => c.Duration < 300)
    .Select(c => c.Path)
    .ToList();*/

var essentia = new EssentiaService();
essentia.LoadModel(modelPath);
var predictions = essentia.RunInference(@"P:\Music\Halogenix - All Blue EP (2015) [META023] [WEB FLAC]\05 - Halogenix - Paper Sword.flac");
Console.WriteLine($"[{string.Join(", ", predictions.Take(5).Select(p => p.ToString("F4")))}...]");

/*var completions = 0;
var loggerFactory = new LoggerFactory();
var logger = loggerFactory.CreateLogger<EssentiaContextManager>();
var essentia = new EssentiaContextManager(logger, 10);
essentia.CreateWorkers();
foreach (var track in tracks)
{
    await essentia.GetEmbeddings(track, _ =>
    {
        Interlocked.Increment(ref completions);
        Console.WriteLine($"Completed {completions} tracks out of {tracks.Count}");
        return Task.CompletedTask;
    });
}

while (completions != tracks.Count)
{
    await Task.Delay(1000);
}
*/

