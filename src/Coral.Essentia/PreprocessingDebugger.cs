using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.LinearAlgebra;

namespace Coral.Essentia;

public static class PreprocessingDebugger
{
    private const int FrameSize = 512;
    private const int HopSize = 256;
    private const int NumberBands = 96;
    private const float SampleRate = 16000f;
    private const int FftSize = FrameSize;

    public static void ComparePreprocessing(string audioFile, string cppOutputFile)
    {
        // Load the same audio file
        var monoLoader = new MonoLoader();
        monoLoader.Configure(new Dictionary<string, object>
        {
            ["filename"] = audioFile,
            ["sampleRate"] = SampleRate,
            ["resampleQuality"] = 4 // Match C++ "resampleQuality", 4
        });

        float[] audio = monoLoader.Compute();

        // Generate C# mel-spectrogram
        var csharpFrames = FrameAudioDebug(audio);
        var csharpMelSpec = ComputeMelSpectrogramDebug(csharpFrames);

        // Load C++ reference data
        var cppFrames = LoadCppReference(cppOutputFile);

        // Compare first 5 frames in detail
        CompareFrames(csharpMelSpec, cppFrames, "csharp_vs_cpp_comparison.txt");
    }

    private static List<float[]> FrameAudioDebug(float[] audio)
    {
        Console.WriteLine($"[DEBUG] Input audio length: {audio.Length}");

        // FrameCutter default: startFromZero=false, silentFrames=noise
        // This means the first frame is centered at sample 0 (so starts at -frameSize/2)
        var frames = new List<float[]>();

        int frameIdx = 0;
        while (true)
        {
            var frame = new float[FrameSize];
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

            // Debug first few frames to match C++ output
            if (frameIdx < 5)
            {
                Console.WriteLine($"C# Frame {frameIdx}:");
                Console.WriteLine($"  Start sample: {startSample}");
                Console.WriteLine($"  End sample: {startSample + FrameSize - 1}");
                Console.WriteLine(
                    $"  First 10 samples: {string.Join(" ", frame.Take(10).Select(x => x.ToString("F6")))}");

                // Show which audio samples are being used
                int firstNonZeroIdx = -1, lastNonZeroIdx = -1;
                for (int i = 0; i < FrameSize; i++)
                {
                    if (Math.Abs(frame[i]) > 1e-10)
                    {
                        if (firstNonZeroIdx == -1) firstNonZeroIdx = startSample + i;
                        lastNonZeroIdx = startSample + i;
                    }
                }

                Console.WriteLine($"  Audio samples used: {firstNonZeroIdx} to {lastNonZeroIdx}");
            }

            frameIdx++;

            // Stop when we're past the end of the audio
            if (startSample >= audio.Length) break;
        }

        Console.WriteLine($"[DEBUG] Generated {frames.Count} frames (C++ style)");
        return frames;
    }

    private static List<float[]> ComputeMelSpectrogramDebug(List<float[]> frames)
    {
        var melSpectrogram = new List<float[]>();

        // Create Hann window - NO NORMALIZATION (normalized=false in Essentia)
        var hannWindow = Window.Hann(FrameSize).Select(w => (float) w).ToArray();

        // Create mel filterbank
        var melFilterbank = CreateMelFilterbankDebug();

        for (int frameIdx = 0; frameIdx < frames.Count; frameIdx++)
        {
            var frame = frames[frameIdx];

            // Create fresh FFT buffer for each frame to avoid contamination
            var fftBuffer = new Complex[FftSize];

            // Apply windowing
            for (int i = 0; i < FrameSize; i++)
            {
                fftBuffer[i] = new Complex(frame[i] * hannWindow[i], 0);
            }

            // Compute FFT with no scaling to match Essentia
            Fourier.Forward(fftBuffer, FourierOptions.NoScaling);

            // IMMEDIATELY compute magnitude spectrum from THIS frame's FFT
            var magnitudeSpectrum = new float[FftSize / 2 + 1];
            for (int i = 0; i < magnitudeSpectrum.Length; i++)
            {
                magnitudeSpectrum[i] = (float) fftBuffer[i].Magnitude;
            }

            // Apply mel filterbank to get linear mel bands
            var melSpectrumVector = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.DenseOfArray(magnitudeSpectrum);
            var melBandsEnergy = melFilterbank.Multiply(melSpectrumVector);

            // Apply the exact TensorflowInputMusiCNN processing:
            // 1. Log10 with cutoff (matching UnaryOperator behavior)
            var logMelBands = melBandsEnergy.Select(val =>
            {
                float cutoff = 1e-30f;
                float clampedVal = Math.Max(val, cutoff);
                return (float) Math.Log10(clampedVal);
            }).ToArray();

            // 2. Apply scale and shift from TensorflowInputMusiCNN parameters
            float scale = 10000f;
            float shift = 1f;
            var finalMelBands = logMelBands.Select(val => val * scale + shift).ToArray();

            melSpectrogram.Add(finalMelBands);

            // Debug first few frames
            if (frameIdx < 3)
            {
                // Check if the ENTIRE windowed frame is zeros
                bool entireFrameZero = true;
                float maxWindowed = 0;
                for (int i = 0; i < FrameSize; i++)
                {
                    float windowed = frame[i] * hannWindow[i];
                    if (Math.Abs(windowed) > 1e-10)
                    {
                        entireFrameZero = false;
                        maxWindowed = Math.Max(maxWindowed, Math.Abs(windowed));
                    }
                }

                Console.WriteLine($"[DEBUG] Frame {frameIdx}:");
                Console.WriteLine(
                    $"  Raw frame samples [0-4]: [{string.Join(", ", frame.Take(5).Select(x => x.ToString("F6")))}]");
                Console.WriteLine(
                    $"  Windowed samples [0-4]: [{string.Join(", ", Enumerable.Range(0, 5).Select(i => (frame[i] * hannWindow[i]).ToString("F6")))}]");
                Console.WriteLine($"  Entire windowed frame is zero: {entireFrameZero}");
                Console.WriteLine($"  Max windowed sample magnitude: {maxWindowed:F10}");
                Console.WriteLine(
                    $"  Magnitude spectrum [0-4]: [{string.Join(", ", magnitudeSpectrum.Take(5).Select(x => x.ToString("F6")))}]");
                Console.WriteLine(
                    $"  Linear mel bands [0-4]: [{string.Join(", ", melBandsEnergy.Take(5).Select(x => x.ToString("F6")))}]");
                Console.WriteLine(
                    $"  Log10 mel bands [0-4]: [{string.Join(", ", logMelBands.Take(5).Select(x => x.ToString("F6")))}]");
                Console.WriteLine(
                    $"  Final mel bands [0-4]: [{string.Join(", ", finalMelBands.Take(5).Select(x => x.ToString("F6")))}]");
            }
        }

        return melSpectrogram;
    }

    private static Matrix<float> CreateMelFilterbankDebug()
    {
        const float minFreq = 0f;
        float maxFreq = SampleRate / 2.0f;

        // Slaney mel conversion functions
        float HzToSlaneyMel(float hz)
        {
            const float f_sp = 200.0f / 3.0f; // ~66.67 Hz
            const float f_log = 1000.0f;

            if (hz >= f_log)
            {
                return 15.0f + 27.0f * (float) Math.Log10(hz / f_log);
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
                return f_log * (float) Math.Pow(10.0, (mel - 15.0f) / 27.0f);
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

        // Unit triangular normalization (peak = 1.0)
        for (int m = 0; m < NumberBands; m++)
        {
            var row = filterbank.Row(m);
            float maxVal = row.Max();
            if (maxVal > 1e-6f)
            {
                filterbank.SetRow(m, row / maxVal);
            }
        }

        // Debug output
        Console.WriteLine($"[DEBUG] Slaney mel filterbank debug:");
        for (int melBand = 0; melBand < Math.Min(10, NumberBands); melBand++)
        {
            var row = filterbank.Row(melBand);
            var nonZeroIndices = row.Select((val, idx) => new {val, idx})
                .Where(x => x.val > 1e-4f)
                .ToList();

            if (nonZeroIndices.Any())
            {
                Console.WriteLine($"  Mel band {melBand}: {nonZeroIndices.Count} bins, " +
                                  $"range: [{nonZeroIndices.First().idx}-{nonZeroIndices.Last().idx}], " +
                                  $"peak: {row.Max():F3}");
            }
            else
            {
                Console.WriteLine($"  Mel band {melBand}: 0 bins (empty filter)");
            }
        }

        return filterbank;
    }

    private static List<float[]> LoadCppReference(string filename)
    {
        var frames = new List<float[]>();
        var lines = File.ReadAllLines(filename);

        foreach (var line in lines)
        {
            if (line.StartsWith("Frame "))
            {
                // Extract frame data: "Frame 0: [1.234, 5.678, ...]"
                int startIdx = line.IndexOf('[') + 1;
                int endIdx = line.IndexOf(']');

                if (startIdx > 0 && endIdx > startIdx)
                {
                    string dataStr = line.Substring(startIdx, endIdx - startIdx);
                    var values = dataStr.Split(',')
                        .Select(s => float.Parse(s.Trim()))
                        .ToArray();
                    frames.Add(values);
                }
            }
        }

        Console.WriteLine($"[DEBUG] Loaded {frames.Count} reference frames from C++");
        return frames;
    }

    private static void CompareFrames(List<float[]> csharpFrames, List<float[]> cppFrames, string outputFile)
    {
        using var writer = new StreamWriter(outputFile);
        writer.WriteLine("=== C# vs C++ Mel-Spectrogram Comparison ===");
        writer.WriteLine();

        int framesToCompare = Math.Min(5, Math.Min(csharpFrames.Count, cppFrames.Count));

        for (int frameIdx = 0; frameIdx < framesToCompare; frameIdx++)
        {
            var csharpFrame = csharpFrames[frameIdx];
            var cppFrame = cppFrames[frameIdx];

            writer.WriteLine($"Frame {frameIdx}:");
            writer.WriteLine(
                $"  C++ (first 10):     [{string.Join(", ", cppFrame.Take(10).Select(x => x.ToString("F4")))}]");
            writer.WriteLine(
                $"  C#  (first 10):     [{string.Join(", ", csharpFrame.Take(10).Select(x => x.ToString("F4")))}]");

            // Calculate element-wise differences
            var diffs = csharpFrame.Zip(cppFrame, (cs, cpp) => Math.Abs(cs - cpp)).ToArray();
            float maxDiff = diffs.Max();
            float avgDiff = diffs.Average();

            writer.WriteLine($"  Max difference:     {maxDiff:F6}");
            writer.WriteLine($"  Average difference: {avgDiff:F6}");

            // Flag significant differences
            if (maxDiff > 0.01f)
            {
                writer.WriteLine($"  *** WARNING: Large differences detected! ***");

                // Show the 5 largest differences
                var largestDiffs = diffs
                    .Select((diff, idx) => new {Diff = diff, Index = idx})
                    .OrderByDescending(x => x.Diff)
                    .Take(5);

                writer.WriteLine($"  Largest differences:");
                foreach (var item in largestDiffs)
                {
                    writer.WriteLine(
                        $"    Index {item.Index}: C++={cppFrame[item.Index]:F4}, C#={csharpFrame[item.Index]:F4}, Diff={item.Diff:F4}");
                }
            }

            writer.WriteLine();
        }

        Console.WriteLine($"[DEBUG] Comparison saved to {outputFile}");
    }
}