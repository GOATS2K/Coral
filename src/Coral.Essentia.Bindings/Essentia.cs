using System.Text;
using NumSharp;

namespace Coral.Essentia.Bindings;

public class Essentia : IDisposable
{
    private IntPtr _monoLoaderInstance = IntPtr.Zero;
    private IntPtr _tfModelInstance = IntPtr.Zero;
    private bool _disposed = false;

    public void Dispose()
    {
        if (!_disposed)
            EssentiaBindings.ew_clean_up();
    }

    public void LoadAudio(string filePath, int sampleRate = 16000)
    {
        var loadSuccess = EssentiaBindings.ew_configure_mono_loader(filePath, sampleRate);
        if (!loadSuccess)
            throw new EssentiaException($"Failed to configure mono loader: {GetError()}");
    }

    public void LoadModel(string filePath)
    {
        var loadSuccess = EssentiaBindings.ew_configure_tf_model(filePath);
        if (!loadSuccess)
            throw new EssentiaException($"Failed to configure tf model: {GetError()}");
    }

    public float[] RunInference()
    {
        var result = EssentiaBindings.ew_run_inference();
        if (result != 0)
            return [];

        var embeddingCount = EssentiaBindings.ew_get_embedding_count();
        var embeddingSize = EssentiaBindings.ew_get_embedding_size();
        var totalElements = EssentiaBindings.ew_get_total_embedding_elements();
        if (totalElements <= 0)
            return [];

        var flattenedEmbeddings = new float[totalElements];
        var success = EssentiaBindings.ew_get_embeddings_flattened(flattenedEmbeddings, totalElements);
        if (!success)
            throw new EssentiaException($"Failed to get embeddings: {GetError()}");

        var ndArray = np.array(flattenedEmbeddings);
        var reshaped = ndArray.reshape(embeddingCount, embeddingSize);
        return reshaped.mean(axis: 0).ToArray<float>();
    }

    private string GetError()
    {
        var bufferSize = EssentiaBindings.ew_get_error_length();
        var errorChars = new byte[bufferSize];
        var success = EssentiaBindings.ew_get_error(errorChars, bufferSize);
        return !success ? throw new EssentiaException("Failed to get error.") : Encoding.ASCII.GetString(errorChars);
    }
}

public class EssentiaException : Exception
{
    public EssentiaException(string message) : base(message)
    {
    }
}