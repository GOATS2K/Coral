#include <pch.h>

#include "essentia_wrapper.h"

#include "essentia/algorithmfactory.h"
#include "essentia/essentiamath.h"
#include "essentia/pool.h"
#include <essentia/essentia.h>
#include <essentia/debugging.h>


extern "C" {
    namespace {
        essentia::standard::Algorithm* monoLoaderInstance = nullptr;
        essentia::standard::Algorithm* tfModelInstance = nullptr;
        bool hasInitialized = false;
        std::vector<std::vector<float>> lastEmbeddings;
    }

    void ew_clean_up() {
        delete monoLoaderInstance;
        monoLoaderInstance = nullptr;
        delete tfModelInstance;
        tfModelInstance = nullptr;
        essentia::shutdown();
    }

    void* ew_create_mono_loader() {
        try {
            if (!hasInitialized) {
                essentia::init();
                hasInitialized = true;
            }

            essentia::standard::AlgorithmFactory& factory = essentia::standard::AlgorithmFactory::instance();
            if (monoLoaderInstance) {
                return monoLoaderInstance;
            }
            monoLoaderInstance = factory.create("MonoLoader");
            return monoLoaderInstance;
        }
        catch (const std::exception& e) {
            std::cerr << "Error creating MonoLoader: " << e.what() << std::endl;
            return nullptr;
        }
    }

    bool ew_configure_mono_loader(void* instance, const char* filename, int sampleRate) {
        try {
            monoLoaderInstance->configure("filename", std::string(filename), "sampleRate", sampleRate);
            return true;
        }
        catch (const std::exception& e) {
            std::cerr << "Error configuring MonoLoader: " << e.what() << std::endl;
            return false;
        }
    }

    void* ew_create_tf_model() {
        try {
            if (!hasInitialized) {
                essentia::init();
                hasInitialized = true;
            }

            essentia::standard::AlgorithmFactory& factory = essentia::standard::AlgorithmFactory::instance();
            if (tfModelInstance) {
                delete tfModelInstance;
            }
            tfModelInstance = factory.create("TensorflowPredictEffnetDiscogs");
            return tfModelInstance;
        }
        catch (const std::exception& e) {
            std::cerr << "Error creating TensorflowPredict model: " << e.what() << std::endl;
            return nullptr;
        }
    }

    bool ew_configure_tf_model(void* instance, const char* model_path) {
        try {
            tfModelInstance->configure("graphFilename", std::string(model_path), "output", "PartitionedCall:1");
            return true;
        }
        catch (const std::exception& e) {
            std::cerr << "Error configuring TensorflowPredict model: " << e.what() << std::endl;
            return false;
        }
    }

    int ew_run_inference() {
        try {
            monoLoaderInstance->reset();
            tfModelInstance->reset();

            std::vector<essentia::Real> audioBuffer;
            monoLoaderInstance->output("audio").set(audioBuffer);
            tfModelInstance->input("signal").set(audioBuffer);
            tfModelInstance->output("predictions").set(lastEmbeddings);

            monoLoaderInstance->compute();
            if (audioBuffer.empty()) {
                std::cerr << "Error: Audio buffer is empty after loading." << std::endl;
                return -1;
            }
            tfModelInstance->compute();
            return 0;
        }
        catch (const std::exception& e) {
            std::cerr << "Error during inference: " << e.what() << std::endl;
            return -1;
        }
    }

    // Get the number of embedding vectors (outer dimension)
    int ew_get_embedding_count() {
        return static_cast<int>(lastEmbeddings.size());
    }

    // Get the size of each embedding vector (inner dimension)
    int ew_get_embedding_size() {
        if (lastEmbeddings.empty()) return 0;
        return static_cast<int>(lastEmbeddings[0].size());
    }

    // Get total number of floats (count * size)
    int ew_get_total_embedding_elements() {
        if (lastEmbeddings.empty()) return 0;
        return static_cast<int>(lastEmbeddings.size() * lastEmbeddings[0].size());
    }

    // Option 1: Flatten the 2D array into a 1D array (row-major order)
    bool ew_get_embeddings_flattened(float* out_buffer, int buffer_size) {
        if (!out_buffer || lastEmbeddings.empty()) return false;

        int totalElements = ew_get_total_embedding_elements();
        if (totalElements > buffer_size) return false;

        int idx = 0;
        for (const auto& embedding : lastEmbeddings) {
            std::copy(embedding.begin(), embedding.end(), out_buffer + idx);
            idx += embedding.size();
        }
        return true;
    }
}
