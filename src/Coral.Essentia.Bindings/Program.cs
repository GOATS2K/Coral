using System.Diagnostics;
using Coral.Essentia.Bindings;

var modelPath = @"C:\Users\bootie-\Downloads\discogs_track_embeddings-effnet-bs64-1.pb";

List<string> tracks =
[
    @"D:\Syncthing\Music\Gregory Porter - Holding On (Velocity Bootleg)_final.mp3",
    @"D:\Syncthing\Music\Ghostface_Feat_NeYo_-_Back_Like_That_(Marky_&_Bungle_Remix).mp3",
    @"D:\Syncthing\Music\Fluidity - Leave Me Alone.mp3",
    @"D:\Syncthing\Music\dRamatic_amp_dbAudio-Smile.mp3"
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