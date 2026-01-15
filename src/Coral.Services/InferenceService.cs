using System.Text;
using CliWrap;
using Coral.Configuration;
using NumSharp;

namespace Coral.Services;

public class InferenceException : Exception
{
    public string FilePath { get; }
    public int ExitCode { get; }

    public InferenceException(string message, string filePath, int exitCode)
        : base(message)
    {
        FilePath = filePath;
        ExitCode = exitCode;
    }
}

internal class Embeddings
{
    public float[] FlattenedEmbeddings { get; set; }
    public int Size { get; set; }
    public int Count { get; set; }
}

public class InferenceService
{
    private const string Executable = "Coral.Essentia.Cli";
    private const string ModelUrl = "https://essentia.upf.edu/models/feature-extractors/discogs-effnet/discogs_track_embeddings-effnet-bs64-1.pb";
    private const string ModelFileName = "discogs_track_embeddings-effnet-bs64-1.pb";
    private readonly HttpClient _httpClient;
    private readonly string _modelPath;

    public InferenceService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _modelPath = Path.Combine(ApplicationConfiguration.Models, ModelFileName);
    }

    public async Task EnsureModelExists()
    {
        if (File.Exists(_modelPath))
        {
            return;
        }

        Console.WriteLine($"Model not found at {_modelPath}, downloading from {ModelUrl}...");

        var modelFile = File.Create(_modelPath);
        var response = await _httpClient.GetStreamAsync(ModelUrl);
        await response.CopyToAsync(modelFile);
        await modelFile.DisposeAsync();

        Console.WriteLine($"Model downloaded successfully to {_modelPath}");
    }

    private string GetExecutableName()
    {
        return OperatingSystem.IsWindows() ? $"{Executable}.exe" : Executable;
    }

    public async Task<float[]> RunInference(string filePath)
    {
        var outputFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        var cmd = Cli.Wrap(GetExecutableName())
            .WithArguments([filePath, _modelPath, outputFile], escape: true)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr));

        var result = await cmd.ExecuteAsync();
        if (result.ExitCode != 0)
        {
            var errorMessage = stdErr.Length > 0 ? stdErr.ToString().Trim() : $"Exit code {result.ExitCode}";
            throw new InferenceException(errorMessage, filePath, result.ExitCode);
        }

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