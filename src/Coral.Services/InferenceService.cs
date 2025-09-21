using System.Text;
using CliWrap;
using CliWrap.Exceptions;
using NumSharp;

namespace Coral.Services;

internal class Embeddings
{
    public float[] FlattenedEmbeddings { get; set; }
    public int Size { get; set; }
    public int Count { get; set; }
}

public static class InferenceService
{
    private static readonly string Executable = "Coral.Essentia.Cli.exe";

    public static async Task<float[]> RunInference(string filePath)
    {
        var modelPath = @"C:\Users\bootie-\Downloads\discogs_track_embeddings-effnet-bs64-1.pb";
        var outputFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        var cmd = Cli.Wrap(Executable)
            .WithArguments([filePath, modelPath, outputFile], escape: true)
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr));
        await cmd.ExecuteAsync();

        var embeddings = await File.ReadAllLinesAsync(outputFile);
        File.Delete(outputFile);

        var results = ParseEmbeddings(embeddings.ToList());
        return results.FlattenedEmbeddings;
    }

    private static float[] ExtractEmbeddings(List<string> data)
    {
        List<float> results = [];
        var beginMarker = "-- Inference Result --";
        var resultMarker = "-- Inference Data --";

        var beginPosition = data.IndexOf(beginMarker);
        var endPosition = data.IndexOf(resultMarker);
        var embeddings = data.Skip(beginPosition + 1).Take(endPosition - beginPosition).ToArray();
        foreach (var embedding in embeddings)
        {
            if (float.TryParse(embedding, out var value))
            {
                results.Add(value);
            }
        }

        return results.ToArray();
    }

    private static Embeddings ParseEmbeddings(List<string> data)
    {
        var resultMarker = "-- Inference Data --";

        var indexOfEndMarker = data.IndexOf(resultMarker);
        var lines = data.Skip(indexOfEndMarker).ToList();
        var rowCount = int.Parse(lines.First(s => s.StartsWith("Row count")).Split(':')[1]);
        var size = int.Parse(lines.First(s => s.StartsWith("Embedding size")).Split(':')[1]);

        var ndArray = np.array(ExtractEmbeddings(data));
        var reshaped = ndArray.reshape(rowCount, size);
        var results = reshaped.mean(axis: 0).ToArray<float>();

        return new Embeddings()
        {
            FlattenedEmbeddings = results,
            Size = size,
            Count = rowCount,
        };
    }
}