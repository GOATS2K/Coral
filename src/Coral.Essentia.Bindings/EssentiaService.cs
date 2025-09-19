using System.Text;
using NumSharp;

namespace Coral.Essentia.Bindings;

public class EssentiaService : IDisposable
{
    private bool _disposed = false;
    private readonly int _contextId = 0;

    public EssentiaService()
    {
        _contextId = EssentiaBindings.ew_create_context();
    }

    public void Dispose()
    {
        if (!_disposed)
            EssentiaBindings.ew_destroy_context(_contextId);
    }

    public void LoadModel(string filePath)
    {
        var loadSuccess = EssentiaBindings.ew_configure_tf_model(_contextId, filePath);
        if (!loadSuccess)
            throw new EssentiaException($"Failed to configure tf model: {GetError()}");
    }

    public float[] RunInference(string filePath, int sampleRate = 16000, int resampleQuality = 4)
    {
        var result = EssentiaBindings.ew_run_inference(_contextId, filePath, sampleRate, resampleQuality);
        if (result != 0)
            throw new EssentiaException($"Failed to get embeddings: {GetError()}");

        var embeddingCount = EssentiaBindings.ew_get_embedding_count(_contextId);
        var embeddingSize = EssentiaBindings.ew_get_embedding_size(_contextId);
        var totalElements = EssentiaBindings.ew_get_total_embedding_elements(_contextId);
        if (totalElements <= 0)
            throw new EssentiaException($"Failed to get embeddings: {GetError()}");

        var flattenedEmbeddings = new float[totalElements];
        var success = EssentiaBindings.ew_get_embeddings_flattened(_contextId, flattenedEmbeddings, totalElements);
        if (!success)
            throw new EssentiaException($"Failed to get embeddings: {GetError()}");
        
        var ndArray = np.array(flattenedEmbeddings);
        var reshaped = ndArray.reshape(embeddingCount, embeddingSize);
        return reshaped.mean(axis: 0).ToArray<float>();
    }

    private string GetError()
    {
        var bufferSize = EssentiaBindings.ew_get_error_length(_contextId);
        var errorChars = new byte[bufferSize];
        var success = EssentiaBindings.ew_get_error(_contextId, errorChars, bufferSize);
        return !success ? throw new EssentiaException("Failed to get error.") : Encoding.ASCII.GetString(errorChars);
    }
}

public class EssentiaException : Exception
{
    public EssentiaException(string message) : base(message)
    {
    }
}