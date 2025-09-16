using System.Diagnostics;
using Coral.Essentia.Bindings;

var modelPath = @"C:\Users\bootie-\Downloads\discogs_track_embeddings-effnet-bs64-1.pb";

List<string> tracks =
[
    @"P:\Music\Downloads\Halogenix - Let Me Explain.opus",
    @"P:\Music\Halogenix - Edits Vol. 1 (2021) [WEB FLAC]\03 - San Holo - Always On My Mind (feat. James Vincent McMorrow & Yvette Young) (Halogenix Edit).flac",
    @"P:\Music\Ichiko Aoba (青葉市子) - Radio (ラヂヲ) (2013) [CD FLAC]\05 - Ichiko Aoba - 不和リン.flac",
    @"P:\Music\50 Cent - The Massacre (2005) [WEB FLAC]\03 - 50 Cent - This Is 50.flac"
];

using var essentia = new EssentiaService();
essentia.LoadModel(modelPath);

var sw = Stopwatch.StartNew();
foreach (var track in tracks)
{
    try
    {
        essentia.LoadAudio(track);
        var emb = essentia.RunInference();
        Console.WriteLine($"[{sw.Elapsed.TotalSeconds}s] {string.Join(", ", emb[..4])}");
    }
    catch (EssentiaException e)
    {
        Console.WriteLine($"Failed to get embeddings: {e.Message}");
    }
}