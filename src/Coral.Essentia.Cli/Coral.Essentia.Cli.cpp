#include <iostream>
#include <fstream>
#include <essentia/essentia.h>
#include <essentia/algorithmfactory.h>

#ifdef _WIN32
#include <Windows.h>

// Windows-specific UTF-8 conversion function
std::string to_utf8(const std::wstring& wstr) {
    if (wstr.empty()) {
        return std::string();
    }
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), NULL, 0, NULL, NULL);
    std::string str_to(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), &str_to[0], size_needed, NULL, NULL);
    return str_to;
}
#endif

// Common inference logic for all platforms
int run_inference(const std::string& audioFileName, const std::string& modelFileName, const std::string& outputFileName) {
    std::vector<std::vector<float>> lastEmbeddings;
    const auto sampleRate = 16000;
    const auto resampleQuality = 4;

    essentia::init();

    try {
        // Configure algorithms
        essentia::standard::AlgorithmFactory& factory = essentia::standard::AlgorithmFactory::instance();
        auto tf = factory.create("TensorflowPredictEffnetDiscogs");
        tf->configure("graphFilename", modelFileName, "output", "PartitionedCall:1");

        auto monoLoader = factory.create("MonoLoader");
        monoLoader->configure("filename", audioFileName, "sampleRate", sampleRate, "resampleQuality", resampleQuality);

        // Connect algorithms
        std::vector<essentia::Real> audioBuffer;
        monoLoader->output("audio").set(audioBuffer);
        tf->input("signal").set(audioBuffer);
        tf->output("predictions").set(lastEmbeddings);

        // Compute
        monoLoader->compute();
        tf->compute();

        // Write results
        std::ofstream output(outputFileName);
        output << "-- Inference Result --" << "\n";
        for (auto& embedding : lastEmbeddings) {
            for (const auto& row : embedding) {
                output << row << "\n";
            }
        }
        output << "\n-- Inference Data --\n";
        output << "Row count: " << lastEmbeddings.size() << "\n";
        output << "Embedding size: " << lastEmbeddings[0].size() << "\n";
        output.close();
    }
    catch (std::exception& e) {
        std::cerr << "Error occurred while performing inference: " << e.what() << "\n";
        essentia::shutdown();
        return 1;
    }

    essentia::shutdown();
    return 0;
}

#ifdef _WIN32
// Windows: Use wmain for proper Unicode argument handling
int wmain(int argc, wchar_t* argv[]) {
    if (argc != 4) {
        std::cerr << "Arguments: <file to get embeddings for> <model path> <output file for embeddings>\n";
        return 1;
    }

    return run_inference(
        to_utf8(argv[1]),  // audioFileName
        to_utf8(argv[2]),  // modelFileName
        to_utf8(argv[3])   // outputFileName
    );
}
#else
// Linux/macOS: UTF-8 is native, use regular main
int main(int argc, char* argv[]) {
    if (argc != 4) {
        std::cerr << "Arguments: <file to get embeddings for> <model path> <output file for embeddings>\n";
        return 1;
    }

    return run_inference(
        argv[1],  // audioFileName
        argv[2],  // modelFileName
        argv[3]   // outputFileName
    );
}
#endif