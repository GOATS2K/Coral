using MathNet.Numerics.LinearAlgebra;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NumSharp;

namespace Coral.Essentia;

public class TensorflowPredictEffnetDiscogs : Configurable, IDisposable
{
    private InferenceSession _session;
    private string _inputName;
    private string _outputName = "PartitionedCall:1";
    private int _patchSize = 128;
    private int _patchHopSize = 62;
    private int _batchSize = 64;

    public override void DeclareParameters()
    {
        DeclareParameter("input", "melspectrogram", "Input node name.");
        DeclareParameter("output", "PartitionedCall:1", "Output node name.");
        DeclareParameter("patchSize", 128, "Number of frames per inference patch.");
        DeclareParameter("patchHopSize", 62, "Hop size between patches in frames.");
        DeclareParameter("batchSize", 64, "Inference batch size.");
    }

    public void LoadModel(string modelPath)
    {
        _session?.Dispose();
        _session = new InferenceSession(modelPath);
        _inputName = _session.InputMetadata.Keys.First();
    }

    public override void Configure()
    {
        _inputName = GetParameter<string>("input");
        _outputName = GetParameter<string>("output");
        _patchSize = GetParameter<int>("patchSize");
        _patchHopSize = GetParameter<int>("patchHopSize");
        _batchSize = GetParameter<int>("batchSize");
    }

    public float[] Compute(float[] audio)
    {
        if (_session is null) throw new EssentiaException("Model not loaded.");

        var melFrames = MusiCnnFeatureExtractor.Compute(audio);
        if (melFrames.Length == 0) return [];

        var patches = CreatePatches(melFrames);
        if (patches.Count == 0) return [];

        var allPredictions = new List<float[]>();
        for (int i = 0; i < patches.Count; i += _batchSize)
        {
            var batchPatches = patches.Skip(i).Take(_batchSize).ToList();

            // --- DEFINITIVE CORRECTION: Pad the final batch to match model's fixed size ---
            int originalCount = batchPatches.Count;
            if (originalCount > 0 && originalCount < _batchSize)
            {
                int numBands = batchPatches.First().GetLength(1);
                var emptyPatch = new float[_patchSize, numBands];
                while (batchPatches.Count < _batchSize)
                {
                    batchPatches.Add(emptyPatch);
                }
            }

            // Only run inference if there's a full batch to process
            if (batchPatches.Count == _batchSize)
            {
                var predictions = RunInference(batchPatches);
                // Only add predictions for the original, non-padded data
                allPredictions.AddRange(predictions.Take(originalCount));
            }
            else if (originalCount > 0)
            {
                // This case handles when the total number of patches is less than one full batch
                allPredictions.AddRange(RunInference(batchPatches));
            }
        }

        // 1. Convert the list of arrays into a Matrix.
        //    This is the C# equivalent of creating a 2D NumPy array.
        var matrix = Matrix<float>.Build.DenseOfRows(allPredictions);

        // 2. Calculate the mean of each column (equivalent to np.mean(axis=0)).
        var meanVector = matrix.ColumnSums().Divide(matrix.RowCount);

        // 3. (Optional) Convert the result back to a standard float[] array.
        float[] finalAggregatedVector = meanVector.ToArray();

        L2Normalize(finalAggregatedVector);

        return finalAggregatedVector;
    }

    private void L2Normalize(float[] vector)
    {
        if (vector == null || vector.Length == 0) return;

        // Use a 'double' for the sum to maintain precision and avoid overflow.
        double sumOfSquares = 0.0;
        foreach (float value in vector)
        {
            sumOfSquares += value * value;
        }

        // The magnitude will also be a double.
        double magnitude = Math.Sqrt(sumOfSquares);

        // Handle the edge case of a zero vector.
        // Use a small epsilon for robust floating-point comparison.
        if (magnitude < 1e-6)
        {
            // Optionally, you could zero out the vector here if needed.
            // Array.Clear(vector, 0, vector.Length);
            return;
        }

        // Cast the magnitude back to float for the final division.
        float norm = (float)magnitude;
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }
    }

    private List<float[,]> CreatePatches(float[][] frames)
    {
        var patches = new List<float[,]>();
        if (frames.Length == 0 || frames[0].Length == 0) return patches;

        int numBands = frames[0].Length;
        for (int i = 0; i + _patchSize <= frames.Length; i += _patchHopSize)
        {
            var patch = new float[_patchSize, numBands];
            for (int j = 0; j < _patchSize; j++)
            {
                for (int k = 0; k < numBands; k++)
                {
                    patch[j, k] = frames[i + j][k];
                }
            }

            patches.Add(patch);
        }

        return patches;
    }

    private List<float[]> RunInference(List<float[,]> patchBatch)
    {
        if (patchBatch.Count != _batchSize)
        {
            throw new EssentiaException($"Inference batch size must be {_batchSize}, but got {patchBatch.Count}.");
        }

        var shape = new int[] {patchBatch.Count, _patchSize, patchBatch[0].GetLength(1)};
        var inputData = patchBatch.SelectMany(p => p.Cast<float>()).ToArray();
        var inputTensor = new DenseTensor<float>(inputData, shape);

        var inputs = new List<NamedOnnxValue> {NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)};

        using var results = _session!.Run(inputs);
        var outputTensor = results.First(r => r.Name == _outputName).AsTensor<float>();

        var predictions = new List<float[]>();
        var outputArray = outputTensor.ToArray();

        int batchSizeOut = outputTensor.Dimensions[0];
        int embeddingSize = outputTensor.Dimensions[1];

        for (int i = 0; i < batchSizeOut; i++)
        {
            var embedding = new float[embeddingSize];
            Array.Copy(outputArray, i * embeddingSize, embedding, 0, embeddingSize);
            predictions.Add(embedding);
        }

        return predictions;
    }

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}