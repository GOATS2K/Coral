#include <iostream>
#include <fstream>
#include <essentia/essentia.h>
#include <essentia/algorithmfactory.h>
#include <Windows.h>

std::string to_utf8(const std::wstring& wstr) {
    if (wstr.empty()) {
        return std::string();
    }
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), NULL, 0, NULL, NULL);
    std::string str_to(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), &str_to[0], size_needed, NULL, NULL);
    return str_to;
}

int wmain(int argc, wchar_t* argv[])
{
    if (argc != 4) {
        std::cerr << "Arguments: <file to get embeddings for> <model path> <output file for embeddings>";
        return 1;
    }

    std::vector<std::vector<float>> lastEmbeddings;
    auto sampleRate = 16000;
    auto resampleQuality = 4;
    std::wstring audioFileName = argv[1];

    std::wstring modelFileName = argv[2];
    std::wstring outputFileName = argv[3];

    essentia::init();

    
    try {
        // Configure MonoLoader
        essentia::standard::AlgorithmFactory& factory = essentia::standard::AlgorithmFactory::instance();
        auto tf = factory.create("TensorflowPredictEffnetDiscogs");
        tf->configure("graphFilename", to_utf8(modelFileName), "output", "PartitionedCall:1");

        auto monoLoader = factory.create("MonoLoader");
        monoLoader->configure("filename", to_utf8(audioFileName), "sampleRate", sampleRate, "resampleQuality", resampleQuality);

        std::vector<essentia::Real> audioBuffer;
        monoLoader->output("audio").set(audioBuffer);
        tf->input("signal").set(audioBuffer);
        tf->output("predictions").set(lastEmbeddings);

        monoLoader->compute();
        tf->compute();

        std::ofstream output;
        output.open(outputFileName);

        output << "-- Inference Result --" << "\n";
        int rowCount = 0;
        for (auto& embedding : lastEmbeddings) {
            // Iterate over the rows of the 2D vector.
            for (const auto& row : embedding) {
                output << row << "\n";
            }
        }
        output << "\n-- Inference Data --\n";
        output << "Row count: " << lastEmbeddings.size() << "\n";
        output << "Embedding size: " << lastEmbeddings[0].size() << "\n";
        output.close();
    }
    catch (std::exception e) {
        std::cerr << "Error occurred while performing inference:" << e.what();
        return 1;
    }



    // Configure TensorflowPredictEffnetDiscogs

    essentia::shutdown();
}