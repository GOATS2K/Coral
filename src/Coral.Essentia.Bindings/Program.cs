using System.Diagnostics;
using Coral.Essentia.Bindings;

var modelPath = @"C:\Users\bootie-\Downloads\discogs_track_embeddings-effnet-bs64-1.pb";

var tracks = Directory.GetFiles(@"C:\Music", "*.*", SearchOption.AllDirectories)
    .Where(c => Path.GetExtension(c) == ".mp3")
    .Select(c => new ATL.Track(c))
    .Where(c => c.Duration < 300)
    .Select(c => c.Path)
    .ToArray();

var slices = tracks.Length % 4;
var s1 = tracks[..slices];
var s2 = tracks[slices..(slices * 2)];

void Work1()
{
    var essentia = new EssentiaService();
    essentia.LoadModel(modelPath);
    foreach (var t in tracks)
    {
        essentia.LoadAudio(t);
        essentia.RunInference();
    }
}


Work1();
