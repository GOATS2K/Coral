using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.LinearAlgebra;

namespace Coral.Essentia;

public static class MusiCnnFeatureExtractor
{
    private const int FrameSize = 512;
    private const int HopSize = 256;
    private const int NumberBands = 96;
    private const float SampleRate = 16000f;
    private const int FftSize = FrameSize;

    private static readonly float[] _hannWindow;
    private static readonly Matrix<float> MelFilterbank;

    static MusiCnnFeatureExtractor()
    {
        // Create Hann window - NO NORMALIZATION first to test
        _hannWindow = Window.Hann(FrameSize).Select(w => (float)w).ToArray();
        MelFilterbank = CreateMelFilterbank();
    }

    public static float[][] Compute(float[] audio)
    {
        var frames = FrameAudio(audio);
        var melSpectrogram = new List<float[]>();

        for (int frameIdx = 0; frameIdx < frames.Count; frameIdx++)
        {
            var frame = frames[frameIdx];

            // Create fresh FFT buffer for each frame
            var fftBuffer = new Complex[FftSize];

            // Apply windowing (no normalization)
            for (int i = 0; i < FrameSize; i++)
            {
                fftBuffer[i] = new Complex(frame[i] * _hannWindow[i], 0);
            }

            // Compute FFT with no scaling to match Essentia
            Fourier.Forward(fftBuffer, FourierOptions.NoScaling);

            // Compute magnitude spectrum (NOT power)
            var magnitudeSpectrum = new float[FftSize / 2 + 1];
            for (int i = 0; i < magnitudeSpectrum.Length; i++)
            {
                magnitudeSpectrum[i] = (float)fftBuffer[i].Magnitude;
            }

            // Apply mel filterbank to get linear mel bands
            var melSpectrumVector = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.DenseOfArray(magnitudeSpectrum);
            var melBandsEnergy = MelFilterbank.Multiply(melSpectrumVector);

            // DO NOT square for power - keep as magnitude
            // Apply the exact TensorflowInputMusiCNN processing:
            // 1. Log10 with cutoff (matching UnaryOperator behavior)
            var logMelBands = melBandsEnergy.Select(val =>
            {
                float cutoff = 1e-30f;
                float clampedVal = Math.Max(val, cutoff);
                return (float)Math.Log10(clampedVal);
            }).ToArray();

            // 2. Apply scale and shift from TensorflowInputMusiCNN parameters
            float scale = 10000f;
            float shift = 1f;
            var finalMelBands = logMelBands.Select(val => val * scale + shift).ToArray();

            melSpectrogram.Add(finalMelBands);
        }

        return melSpectrogram.ToArray();
    }

    private static List<float[]> FrameAudio(float[] audio)
    {
        var frames = new List<float[]>();

        int frameIdx = 0;
        while (true)
        {
            var frame = new float[FrameSize];
            // Match Essentia's FrameCutter default behavior (startFromZero=false)
            int startSample = frameIdx * HopSize - FrameSize / 2;

            // Fill frame with audio data or zero padding
            for (int i = 0; i < FrameSize; i++)
            {
                int sampleIdx = startSample + i;
                if (sampleIdx >= 0 && sampleIdx < audio.Length)
                {
                    frame[i] = audio[sampleIdx];
                }
                // else remains 0 (zero padding)
            }

            frames.Add(frame);
            frameIdx++;

            // Stop when we're past the end of the audio
            if (startSample >= audio.Length) break;
        }

        return frames;
    }

    private static Matrix<float> CreateMelFilterbank()
    {
        const float minFreq = 0f;
        float maxFreq = SampleRate / 2.0f;

        // Slaney mel conversion functions (matching Essentia's "slaneyMel")
        float HzToSlaneyMel(float hz)
        {
            const float f_sp = 200.0f / 3.0f; // ~66.67 Hz
            const float f_log = 1000.0f;

            if (hz >= f_log)
            {
                return 15.0f + 27.0f * (float)Math.Log10(hz / f_log);
            }
            else
            {
                return hz / f_sp;
            }
        }

        float SlaneyMelToHz(float mel)
        {
            const float f_sp = 200.0f / 3.0f; // ~66.67 Hz  
            const float f_log = 1000.0f;

            if (mel >= 15.0f)
            {
                return f_log * (float)Math.Pow(10.0, (mel - 15.0f) / 27.0f);
            }
            else
            {
                return f_sp * mel;
            }
        }

        // Create mel-spaced frequency points
        float minMel = HzToSlaneyMel(minFreq);
        float maxMel = HzToSlaneyMel(maxFreq);

        var melPoints = new float[NumberBands + 2];
        for (int i = 0; i < NumberBands + 2; i++)
        {
            melPoints[i] = minMel + i * (maxMel - minMel) / (NumberBands + 1);
        }

        // Convert mel points to Hz
        var hzPoints = new float[NumberBands + 2];
        for (int i = 0; i < NumberBands + 2; i++)
        {
            hzPoints[i] = SlaneyMelToHz(melPoints[i]);
        }

        // Convert Hz to FFT bin indices (continuous values)
        var binIndices = new float[NumberBands + 2];
        for (int i = 0; i < NumberBands + 2; i++)
        {
            binIndices[i] = hzPoints[i] * (FftSize / 2) / maxFreq;
        }

        // Create filterbank matrix
        var filterbank = Matrix<float>.Build.Dense(NumberBands, FftSize / 2 + 1, 0f);

        // Build triangular filters
        for (int m = 0; m < NumberBands; m++)
        {
            float leftBin = binIndices[m];
            float centerBin = binIndices[m + 1];
            float rightBin = binIndices[m + 2];

            for (int k = 0; k < FftSize / 2 + 1; k++)
            {
                float binFreq = k;

                // Left side of triangle (rising slope)
                if (binFreq >= leftBin && binFreq <= centerBin && centerBin > leftBin)
                {
                    filterbank[m, k] = (binFreq - leftBin) / (centerBin - leftBin);
                }
                // Right side of triangle (falling slope)
                else if (binFreq >= centerBin && binFreq <= rightBin && rightBin > centerBin)
                {
                    filterbank[m, k] = (rightBin - binFreq) / (rightBin - centerBin);
                }
            }
        }

        // Try unit_sum normalization to match Essentia's MelBands default
        for (int m = 0; m < NumberBands; m++)
        {
            var row = filterbank.Row(m);
            float sum = row.Sum();
            if (sum > 1e-6f)
            {
                filterbank.SetRow(m, row / sum);
            }
        }

        return filterbank;
    }
}