using System.Diagnostics;
using Coral.Essentia.Bindings;

var modelPath = @"C:\Users\bootie-\Downloads\discogs_track_embeddings-effnet-bs64-1.pb";

var tracks = Directory.GetFiles(@"C:\Music", "*.*", SearchOption.AllDirectories)
    .Where(c => Path.GetExtension(c) == ".mp3")
    .Select(c => new ATL.Track(c))
    .Where(c => c.Duration < 300)
    .Select(c => c.Path)
    .ToArray();

using var essentia = new EssentiaService();
essentia.LoadModel(modelPath);

foreach (var track in tracks)
{
    try
    {
        var sw =  Stopwatch.StartNew();
        Console.WriteLine($"Loading track: {track}");
        essentia.LoadAudio(track);
        var emb = essentia.RunInference();
        Console.WriteLine($"Inference completed in {sw.Elapsed.TotalSeconds} seconds");
    }
    catch (EssentiaException e)
    {
        Console.WriteLine($"Failed to get embeddings: {e.Message}");
    }
}