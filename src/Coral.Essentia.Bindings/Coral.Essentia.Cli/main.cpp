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
    string audioFile = "P:\\Music\\Halogenix - All Blue EP (2015) [META023] [WEB FLAC]\\05 - Halogenix - Paper Sword.flac";
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

    // Debug audio loading
    cout << "Audio buffer size: " << audioBuffer.size() << " samples" << endl;
    cout << "Audio duration: " << (float)audioBuffer.size() / sampleRate << " seconds" << endl;

    if (audioBuffer.size() > 0) {
        cout << "First 10 samples: ";
        for (int i = 0; i < min(10, (int)audioBuffer.size()); i++) {
            cout << fixed << setprecision(6) << audioBuffer[i] << " ";
        }
        cout << endl;

        // Calculate RMS
        Real sum = 0;
        for (Real sample : audioBuffer) sum += sample * sample;
        Real rms = sqrt(sum / audioBuffer.size());
        cout << "Audio RMS: " << rms << endl;
    }

    // Replace your manual frame cutting with proper FrameCutter usage :
    Algorithm * frameCutter = factory.create("FrameCutter",
        "frameSize", 512,
        "hopSize", 256);

    Algorithm* tensorflowInput = factory.create("TensorflowInputMusiCNN");
    Algorithm* melbands = factory.create("MelBands");
    cout << "Default MelBands parameters:" << endl;
    cout << "  numberBands: " << melbands->parameter("numberBands").toReal() << endl;
    cout << "  sampleRate: " << melbands->parameter("sampleRate").toReal() << endl;
    cout << "  inputSize: " << melbands->parameter("inputSize").toInt() << endl;
    cout << "  lowFrequencyBound: " << melbands->parameter("lowFrequencyBound").toReal() << endl;
    cout << "  highFrequencyBound: " << melbands->parameter("highFrequencyBound").toReal() << endl;

    // Process frame by frame
    vector<Real> frame;
    vector<Real> melBands;
    vector<vector<Real>> allMelBands;

    frameCutter->input("signal").set(audioBuffer);
    frameCutter->output("frame").set(frame);

    // Debug the first few frames to see padding behavior
    cout << "[DEBUG] First few frames from FrameCutter:" << endl;
    for (int i = 0; i < 5; i++) {
        frameCutter->compute();
        if (frame.empty()) break;

        cout << "Frame " << i << " first 10 samples: ";
        for (int j = 0; j < min(10, (int)frame.size()); j++) {
            cout << fixed << setprecision(6) << frame[j] << " ";
        }
        cout << endl;
    }
    // Reset for actual processing
    frameCutter->reset();

    tensorflowInput->input("frame").set(frame);
    tensorflowInput->output("bands").set(melBands);

    // Process each frame
    while (true) {
        try {
            frameCutter->compute();
            if (frame.empty()) break;

            tensorflowInput->compute();

            // Apply log scaling
            vector<Real> logMelBands;
            for (Real val : melBands) {
                logMelBands.push_back(log(val + 1e-6));
            }

            allMelBands.push_back(logMelBands);

        }
        catch (...) {
            break;
        }
    }

    // 3. Save results to file
    ofstream outFileStream(outputFile);
    outFileStream << "--- Ground Truth Spectrogram (from Essentia/C++) ---" << endl;
    for (int i = 0; i < min(5, (int)allMelBands.size()); ++i) {
        outFileStream << "Frame " << i << ": [";
        for (int j = 0; j < (int)allMelBands[i].size(); ++j) {
            outFileStream << fixed << setprecision(4) << allMelBands[i][j]
                << (j == (int)allMelBands[i].size() - 1 ? "" : ", ");
        }
        outFileStream << "]" << endl;
    }
    cout << "[DEBUG] C++ spectrogram saved to " << outputFile << endl;

    // Cleanup
    delete monoLoader;
    delete frameCutter;
    delete tensorflowInput;
    essentia::shutdown();

    return 0;
}

