#include <iostream>
#include <fstream>
#include <essentia/essentia.h>
#include <essentia/algorithmfactory.h>

int main(int argc, char* argv[])
{
    if (argc != 4) {
        std::cerr << "Arguments: <file to get embeddings for> <model path>";
        return 1;
    }

    std::vector<std::vector<float>> lastEmbeddings;
    auto sampleRate = 16000;
    auto resampleQuality = 4;
    auto fileName = argv[1];

    auto modelFileName = argv[2];
    auto outputFileName = argv[3];

    essentia::init();

    
    try {
        // Configure MonoLoader
        essentia::standard::AlgorithmFactory& factory = essentia::standard::AlgorithmFactory::instance();
        auto tf = factory.create("TensorflowPredictEffnetDiscogs");
        tf->configure("graphFilename", std::string(modelFileName), "output", "PartitionedCall:1");

        auto monoLoader = factory.create("MonoLoader");
        monoLoader->configure("filename", std::string(fileName), "sampleRate", sampleRate, "resampleQuality", resampleQuality);

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