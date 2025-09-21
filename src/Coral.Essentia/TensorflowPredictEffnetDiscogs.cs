using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Coral.Essentia;

public class TensorflowPredictEffnetDiscogs : IDisposable
{
    // Hardcoded parameters matching the C++ implementation
    private const int FrameSize = 512;
    private const int HopSize = 256;
    private const int NumberBands = 96;
    private const float SampleRate = 16000f;
    private const int PatchSize = 128;
    private const int PatchHopSize = 62;
    private const int BatchSize = 64;

    private InferenceSession? _session;
    private readonly float[] _window;
    private readonly Matrix<float> _melFilterbank;

    public TensorflowPredictEffnetDiscogs()
    {
        _window = CreateHannWindow();
        _melFilterbank = CreateMelFilterbank();
    }

    public void LoadModel(string modelPath)
    {
        _session?.Dispose();
        _session = new InferenceSession(modelPath);
    }

    public float[] Compute(float[] audioSignal)
    {
        if (_session == null)
            throw new InvalidOperationException("Model not loaded");

        // Step 1: Frame the audio signal
        var frames = FrameCutter(audioSignal);

        // Step 2: Compute mel-spectrograms for each frame
        var melSpectrograms = new List<float[]>();
        foreach (var frame in frames)
        {
            var melBands = ComputeMelBands(frame);
            melSpectrograms.Add(melBands);
        }

        // Step 3: Create patches from mel-spectrograms
        var patches = CreatePatches(melSpectrograms);

        // Step 4: Process patches in batches
        var allEmbeddings = new List<float[]>();
        for (int i = 0; i < patches.Count; i += BatchSize)
        {
            var batch = patches.Skip(i).Take(BatchSize).ToList();

            // Pad batch if needed
            while (batch.Count < BatchSize)
            {
                // Repeat last patch or use zeros
                var emptyPatch = new float[PatchSize, NumberBands];
                batch.Add(emptyPatch);
            }

            var embeddings = RunInference(batch);
            // Only keep non-padded results
            int validCount = Math.Min(patches.Count - i, BatchSize);
            allEmbeddings.AddRange(embeddings.Take(validCount));
        }

        // Step 5: Average embeddings
        if (allEmbeddings.Count == 0)
            return Array.Empty<float>();

        return AverageEmbeddings(allEmbeddings);
    }

    private float[] CreateHannWindow()
    {
        var window = new float[FrameSize];

        // Standard Hann window formula
        for (int i = 0; i < FrameSize; i++)
        {
            window[i] = 0.5f - 0.5f * (float) Math.Cos(2.0 * Math.PI * i / (FrameSize - 1.0));
        }

        // Normalize window (Essentia default: normalized=true)
        float sum = window.Sum();
        if (sum > 0)
        {
            float scale = 2.0f / sum; // Scale by 2 for negative frequencies
            for (int i = 0; i < window.Length; i++)
                window[i] *= scale;
        }

        return window;
    }

    private List<float[]> FrameCutter(float[] audio)
    {
        var frames = new List<float[]>();
        int frameCount = 0;

        // Essentia's default: startFromZero=false, meaning first frame is centered at t=0
        while (true)
        {
            var frame = new float[FrameSize];
            int startSample = frameCount * HopSize - FrameSize / 2;

            // Copy samples with zero padding
            for (int i = 0; i < FrameSize; i++)
            {
                int sampleIdx = startSample + i;
                if (sampleIdx >= 0 && sampleIdx < audio.Length)
                    frame[i] = audio[sampleIdx];
                // else frame[i] remains 0
            }

            frames.Add(frame);
            frameCount++;

            // Stop after we've processed all audio
            if (startSample >= audio.Length)
                break;
        }

        return frames;
    }

    private float[] ComputeMelBands(float[] frame)
    {
        // Step 1: Apply window
        var windowedFrame = new float[FrameSize];
        for (int i = 0; i < FrameSize; i++)
            windowedFrame[i] = frame[i] * _window[i];

        // Step 2: Compute FFT
        var fftBuffer = new Complex[FrameSize];
        for (int i = 0; i < FrameSize; i++)
            fftBuffer[i] = new Complex(windowedFrame[i], 0);

        Fourier.Forward(fftBuffer, FourierOptions.NoScaling);

        // Step 3: Compute magnitude spectrum
        var magnitudeSpectrum = new float[FrameSize / 2 + 1];
        for (int i = 0; i < magnitudeSpectrum.Length; i++)
            magnitudeSpectrum[i] = (float) fftBuffer[i].Magnitude;

        // Step 4: Apply mel filterbank
        var spectrumVector = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.DenseOfArray(magnitudeSpectrum);
        var melBands = _melFilterbank.Multiply(spectrumVector).ToArray();

        // Step 5: Convert to power spectrum (MelBands default: type="power")
        for (int i = 0; i < melBands.Length; i++)
            melBands[i] = melBands[i] * melBands[i];

        // Step 6: Apply log10 (TensorflowInputMusiCNN processing)
        const float epsilon = 1e-30f;
        for (int i = 0; i < melBands.Length; i++)
        {
            melBands[i] = (float) Math.Log10(Math.Max(melBands[i], epsilon));
        }

        // Step 7: Apply scale and shift
        const float scale = 10000f;
        const float shift = 1f;
        for (int i = 0; i < melBands.Length; i++)
        {
            melBands[i] = melBands[i] * scale + shift;
        }

        return melBands;
    }

    private Matrix<float> CreateMelFilterbank()
    {
        // HTK mel scale (Essentia MelBands default)
        float HzToMel(float hz) => 2595.0f * (float) Math.Log10(1.0f + hz / 700.0f);
        float MelToHz(float mel) => 700.0f * ((float) Math.Pow(10.0f, mel / 2595.0f) - 1.0f);

        float minFreq = 0f;
        float maxFreq = SampleRate / 2.0f;
        float minMel = HzToMel(minFreq);
        float maxMel = HzToMel(maxFreq);

        // Create mel-spaced points
        var melPoints = new float[NumberBands + 2];
        for (int i = 0; i < NumberBands + 2; i++)
            melPoints[i] = minMel + i * (maxMel - minMel) / (NumberBands + 1);

        // Convert to Hz then to bin indices
        var binFreqs = new float[NumberBands + 2];
        for (int i = 0; i < NumberBands + 2; i++)
        {
            float hz = MelToHz(melPoints[i]);
            binFreqs[i] = hz * FrameSize / SampleRate;
        }

        // Build triangular filterbank
        var filterbank = Matrix<float>.Build.Dense(NumberBands, FrameSize / 2 + 1);

        for (int m = 0; m < NumberBands; m++)
        {
            float left = binFreqs[m];
            float center = binFreqs[m + 1];
            float right = binFreqs[m + 2];

            for (int k = 0; k <= FrameSize / 2; k++)
            {
                if (k >= left && k <= center)
                {
                    filterbank[m, k] = (k - left) / (center - left);
                }
                else if (k > center && k <= right)
                {
                    filterbank[m, k] = (right - k) / (right - center);
                }
            }
        }

        // Normalize: unit_sum (MelBands default)
        for (int m = 0; m < NumberBands; m++)
        {
            float sum = 0;
            for (int k = 0; k <= FrameSize / 2; k++)
                sum += filterbank[m, k];

            if (sum > 0)
            {
                for (int k = 0; k <= FrameSize / 2; k++)
                    filterbank[m, k] /= sum;
            }
        }

        return filterbank;
    }

    private List<float[,]> CreatePatches(List<float[]> melSpectrograms)
    {
        var patches = new List<float[,]>();

        // Create patches with hop
        for (int i = 0; i <= melSpectrograms.Count - PatchSize; i += PatchHopSize)
        {
            var patch = new float[PatchSize, NumberBands];
            for (int j = 0; j < PatchSize; j++)
            {
                for (int k = 0; k < NumberBands; k++)
                {
                    patch[j, k] = melSpectrograms[i + j][k];
                }
            }

            patches.Add(patch);
        }

        return patches;
    }

    private List<float[]> RunInference(List<float[,]> batch)
    {
        // Prepare input tensor
        var shape = new[] {batch.Count, PatchSize, NumberBands};
        var inputData = new float[batch.Count * PatchSize * NumberBands];

        int idx = 0;
        foreach (var patch in batch)
        {
            for (int i = 0; i < PatchSize; i++)
            {
                for (int j = 0; j < NumberBands; j++)
                {
                    inputData[idx++] = patch[i, j];
                }
            }
        }

        var inputTensor = new DenseTensor<float>(inputData, shape);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("serving_default_melspectrogram:0", inputTensor)
        };

        // Run inference
        using var results = _session!.Run(inputs);
        var outputTensor = results.First(r => r.Name == "PartitionedCall:1").AsTensor<float>();

        // Extract embeddings
        var embeddings = new List<float[]>();
        var outputArray = outputTensor.ToArray();
        int embeddingSize = outputTensor.Dimensions[1];

        for (int i = 0; i < batch.Count; i++)
        {
            var embedding = new float[embeddingSize];
            Array.Copy(outputArray, i * embeddingSize, embedding, 0, embeddingSize);
            embeddings.Add(embedding);
        }

        return embeddings;
    }

    private float[] AverageEmbeddings(List<float[]> embeddings)
    {
        if (embeddings.Count == 0)
            return Array.Empty<float>();

        int size = embeddings[0].Length;
        var result = new float[size];

        foreach (var embedding in embeddings)
        {
            for (int i = 0; i < size; i++)
                result[i] += embedding[i];
        }

        for (int i = 0; i < size; i++)
            result[i] /= embeddings.Count;

        return result;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}