// See https://aka.ms/new-console-template for more information

using System.Text;
using Coral.Essentia;
using FFmpeg.AutoGen;

ffmpeg.RootPath = @"C:\Projects\Coral\src\Coral.Essentia.Bindings\lib";
DynamicallyLoadedBindings.Initialize();

string audioPath = @"P:\Music\Halogenix - All Blue EP (2015) [META023] [WEB FLAC]\05 - Halogenix - Paper Sword.flac";
string modelPath = @"P:\discogs_embeddings_both_outputs.onnx";
var spectrogramOutput = @"C:\Projects\Coral\src\Coral.Essentia.Bindings\Coral.Essentia.Cli\cpp_spectrogram.txt";

// PreprocessingDebugger.ComparePreprocessing(audioPath, spectrogramOutput);

// 1. Load audio and resample to 16kHz mono
var loader = new MonoLoader();
loader.Configure(new Dictionary<string, object>
{
    {"filename", audioPath},
    {"sampleRate", 16000f},
    {"resampleQuality", 4},
});
float[] audioData = loader.Compute();
var tf = new TensorflowPredictEffnetDiscogs();
tf.LoadModel(modelPath);
var embeddings = tf.Compute(audioData);
Console.WriteLine($"Got {embeddings.Length} embeddings.");
Console.WriteLine($"[{string.Join(", ", embeddings.Take(5))}]");