using Coral.Essentia.Bindings;

var modelPath = @"C:\Users\bootie-\Downloads\discogs_track_embeddings-effnet-bs64-1.pb";
var trackPath = @"P:\Music\Rare Dubs\Producer-sourced\Friends\Satl - Guilty v3 [Closure Master 2 24-44].flac";

using var essentia = new Essentia();
Console.WriteLine("Initializing Essentia...");
essentia.Initialize();
Console.WriteLine("Loading model.");
essentia.LoadModel("this/file/is/bruh");
Console.WriteLine("Loading audio.");
essentia.LoadAudio("this/file/does/not/exist");
Console.WriteLine("Running inference.");
var embeddings = essentia.RunInference();

Console.WriteLine($"Embedding size: {embeddings.Length}");