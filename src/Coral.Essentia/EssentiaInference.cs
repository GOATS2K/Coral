namespace Coral.Essentia;

public class EssentiaInference : IDisposable
{
    private readonly MonoLoader _loader;
    private readonly TensorflowPredictEffnetDiscogs _tf;

    public EssentiaInference()
    {
        _loader = new MonoLoader();
        _tf = new TensorflowPredictEffnetDiscogs();
    }
    
    public void LoadModel(string path) => _tf.LoadModel(path);

    public float[] RunInference(string fileName)
    {
        _loader.Configure(new Dictionary<string, object>
        {
            {"filename", fileName },
            {"sampleRate", 16000f},
            {"resampleQuality", 4},
        });
        float[] audioData = _loader.Compute();
        return _tf.Compute(audioData);
    }

    public void Dispose()
    {
        _tf.Dispose();
    }
}