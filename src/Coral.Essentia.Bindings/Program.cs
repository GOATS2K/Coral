using System.Diagnostics;
using Coral.Essentia.Bindings;

var modelPath = @"C:\Users\bootie-\Downloads\discogs_track_embeddings-effnet-bs64-1.pb";
var trackPath = @"P:\Music\Rare Dubs\Producer-sourced\Friends\Satl - Guilty v3 [Closure Master 2 24-44].flac";
var track2 = @"P:\Music\Lenzman - A Little While Longer (2021) [NQ025] [WEB FLAC]\05 - Lenzman - Starlight (feat. Fox).flac";

var sw = Stopwatch.StartNew();
using var essentia = new Essentia();
essentia.LoadAudio(trackPath);
essentia.LoadModel(modelPath);
var e1 = essentia.RunInference();
Console.WriteLine($"[{sw.Elapsed.TotalSeconds}s] Got embeddings for track 1 - {e1[0]}.");

essentia.LoadAudio(track2);
var e2 = essentia.RunInference();
Console.WriteLine($"[{sw.Elapsed.TotalSeconds}s] Got embeddings for track 2 - {e2[0]}.");