using System.Diagnostics;
using Coral.Essentia.Bindings;

var modelPath = @"C:\Users\bootie-\Downloads\discogs_track_embeddings-effnet-bs64-1.pb";

var tracks = Directory.GetFiles(@"C:\Music", "*.*", SearchOption.AllDirectories)
    .Where(c => Path.GetExtension(c) == ".mp3")
    .Select(c => new ATL.Track(c))
    .Where(c => c.Duration < 300)
    .Select(c => c.Path)
    .Take(10)
    .ToList();

var tasks = tracks.Select<string, Func<Task>>(t =>
{
    return async () =>
    {
        await Task.Run(() =>
        {
            var service = new EssentiaService();
            service.LoadModel(modelPath);
            service.RunInference(t);
        });
    };
});

await Task.WhenAll(tasks.Select(t => t.Invoke()));


