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
        var tempWindow = Window.Hann(FrameSize).Select(w => (float) w).ToArray();
        float windowSquareSum = tempWindow.Sum(x => x * x);
        _hannWindow = tempWindow.Select(w => w / (float) Math.Sqrt(windowSquareSum)).ToArray();
        MelFilterbank = CreateMelFilterbank(SampleRate, FftSize, NumberBands);
    }

    public static float[][] Compute(float[] audio)
    {
        var frames = FrameAudio(audio);
        var melSpectrogram = new List<float[]>();
        var fftBuffer = new Complex[FftSize];

        foreach (var frame in frames)
        {
            for (int i = 0; i < FrameSize; i++)
            {
                fftBuffer[i] = new Complex(frame[i] * _hannWindow[i], 0);
            }

            Fourier.Forward(fftBuffer, FourierOptions.NoScaling);

            var magnitudeSpectrum = new float[FftSize / 2 + 1];
            for (int i = 0; i < magnitudeSpectrum.Length; i++)
            {
                magnitudeSpectrum[i] = (float) fftBuffer[i].Magnitude;
            }

            var melSpectrumVector = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.DenseOfArray(magnitudeSpectrum);
            var melBandsEnergy = MelFilterbank.Multiply(melSpectrumVector);

            var logMelSpectrum = melBandsEnergy.Select(val => (float) Math.Log(val + 1e-6)).ToArray();
            melSpectrogram.Add(logMelSpectrum);
        }

        return melSpectrogram.ToArray();
    }

    private static List<float[]> FrameAudio(float[] audio)
    {
        // 1. Apply start padding to center the first frame, matching Essentia's startFromZero=true
        int startPadding = FrameSize / 2;

        // 2. Calculate the number of frames that will be produced. This formula
        //    implicitly accounts for the necessary end-padding.
        int numFrames = (audio.Length + HopSize - 1) / HopSize;

        // 3. Create a new buffer with enough space for both start and end padding.
        int totalLength = (numFrames - 1) * HopSize + FrameSize;
        var paddedAudio = new float[totalLength];
        Array.Copy(audio, 0, paddedAudio, startPadding, audio.Length);

        var frames = new List<float[]>();
        for (int i = 0; i + FrameSize <= totalLength; i += HopSize)
        {
            var frame = new float[FrameSize];
            Array.Copy(paddedAudio, i, frame, 0, FrameSize);
            frames.Add(frame);
        }

        return frames;
    }

    private static Matrix<float> CreateMelFilterbank(float sampleRate, int fftSize, int numBands,
        float minFreq = 0f, float maxFreq = -1f)
    {
        if (maxFreq <= 0) maxFreq = sampleRate / 2.0f;
        float minMel = EssentiaMath.HzToMel(minFreq);
        float maxMel = EssentiaMath.HzToMel(maxFreq);
        var melPoints = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.Dense(numBands + 2,
            i => minMel + i * (maxMel - minMel) / (numBands + 1));
        var hzPoints = melPoints.Select(EssentiaMath.MelToHz).ToArray();
        var fftBins = hzPoints.Select(f => (float) Math.Floor((fftSize + 1) * f / sampleRate)).ToArray();
        var filterbank = Matrix<float>.Build.Dense(numBands, fftSize / 2 + 1, 0f);

        for (int j = 0; j < numBands; j++)
        {
            for (int i = (int) fftBins[j]; i < (int) fftBins[j + 1]; i++)
                filterbank[j, i] = (i - fftBins[j]) / (fftBins[j + 1] - fftBins[j]);
            for (int i = (int) fftBins[j + 1]; i < (int) fftBins[j + 2]; i++)
                filterbank[j, i] = (fftBins[j + 2] - i) / (fftBins[j + 2] - fftBins[j + 1]);
        }

        for (int i = 0; i < filterbank.RowCount; i++)
        {
            var row = filterbank.Row(i);
            float area = row.Sum();
            if (area > 1e-6)
            {
                filterbank.SetRow(i, row / area);
            }
        }

        return filterbank;
    }
}