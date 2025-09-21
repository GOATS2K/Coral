#include <iostream>
#include <fstream>
#include <vector>
#include <cmath>
#include <iomanip> // For std::fixed and std::setprecision

#include "essentia/essentia.h"
#include "essentia/algorithmfactory.h"

using namespace std;
using namespace essentia;
using namespace essentia::standard;

int main() {
    string audioFile = "P:\\Music\\Rare Dubs\\Producer-sourced\\Friends\\Satl - Medi Vibe.mp3";
    string outputFile = "cpp_spectrogram.txt";
    const int sampleRate = 16000;

    essentia::init();
    AlgorithmFactory& factory = AlgorithmFactory::instance();

    // 1. Load audio into a 1D vector
    vector<Real> audioBuffer;
    Algorithm* monoLoader = factory.create("MonoLoader",
        "filename", audioFile,
        "sampleRate", sampleRate,
        "resampleQuality", 4);
    monoLoader->output("audio").set(audioBuffer);
    monoLoader->compute();

    // 2. Manually cut the audio into frames
    vector<vector<Real>> allFrames;
    const int frameSize = 512;
    const int hopSize = 256;
    for (int i = 0; i + frameSize <= (int)audioBuffer.size(); i += hopSize) {
        allFrames.emplace_back(audioBuffer.begin() + i, audioBuffer.begin() + i + frameSize);
    }

    // 3. Instantiate processing algorithms
    Algorithm* windowing = factory.create("Windowing", "type", "hann");
    Algorithm* spectrum = factory.create("Spectrum");
    Algorithm* melbands = factory.create("MelBands",
        "numberBands", 96,
        "sampleRate", sampleRate,
        "highFrequencyBound", sampleRate / 2);

    vector<vector<Real>> logSpectrogram;

    // 4. Loop through the `allFrames` vector to process each 1D frame individually
    for (const auto& frame : allFrames) {
        vector<Real> windowedFrame, spec, melFrame;

        windowing->input("frame").set(frame);
        windowing->output("frame").set(windowedFrame);
        windowing->compute();

        spectrum->input("frame").set(windowedFrame);
        spectrum->output("spectrum").set(spec);
        spectrum->compute();

        melbands->input("spectrum").set(spec);
        melbands->output("bands").set(melFrame);
        melbands->compute();

        // 5. Apply log scaling to the 1D melFrame
        for (auto& val : melFrame) {
            val = log(val + 1e-6);
        }
        logSpectrogram.push_back(melFrame);
    }

    // 6. Save results to file
    ofstream outFileStream(outputFile);
    outFileStream << "--- Ground Truth Spectrogram (from Essentia/C++) ---" << endl;
    for (int i = 0; i < min(5, (int)logSpectrogram.size()); ++i) {
        outFileStream << "Frame " << i << ": [";
        for (int j = 0; j < logSpectrogram[i].size(); ++j) {
            outFileStream << fixed << setprecision(4) << logSpectrogram[i][j] << (j == logSpectrogram[i].size() - 1 ? "" : ", ");
        }
        outFileStream << "]" << endl;
    }
    cout << "[DEBUG] C++ spectrogram saved to " << outputFile << endl;

    // Cleanup
    delete monoLoader;
    delete windowing;
    delete spectrum;
    delete melbands;
    essentia::shutdown();

    return 0;
}

