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
        std::string lastError = "";
    }

    void ew_clean_up() {
        delete monoLoaderInstance;
        monoLoaderInstance = nullptr;
        delete tfModelInstance;
        tfModelInstance = nullptr;
        lastEmbeddings.clear();
        essentia::shutdown();
    }

    bool ew_get_error(char* buffer, int buffer_size) {
        if (!buffer || buffer_size <= 0) {
            std::cout << "[Coral Essentia Wrapper] No buffer/buffer_size detected.";
            return false;
        }

        if (lastError.length() >= static_cast<size_t>(buffer_size)) {
            std::cout << "[Coral Essentia Wrapper] Buffer too small.";
            return false; // Buffer too small
        }

        strcpy_s(buffer, buffer_size, lastError.c_str());
        return true;
    }

    int ew_get_error_length() {
        // strcpy_s needs an extra byte for the null terminator.
        int length = lastError.length();
        int lengthWithNullTerminator = length + 1;
        return lengthWithNullTerminator;
    }

    essentia::standard::Algorithm* ew_get_mono_loader() {
        try {
            if (!hasInitialized) {
                essentia::init();
                hasInitialized = true;
            }

            if (monoLoaderInstance != nullptr) {
                return monoLoaderInstance;
            }

            essentia::standard::AlgorithmFactory& factory = essentia::standard::AlgorithmFactory::instance();
            monoLoaderInstance = factory.create("MonoLoader");
            return monoLoaderInstance;
        }
        catch (const std::exception& e) {
            lastError = e.what();
            return nullptr;
        }
    }

    bool ew_configure_mono_loader(const char* filename, int sampleRate) {
        try {
            ew_get_mono_loader()->configure("filename", std::string(filename), "sampleRate", sampleRate);
            return true;
        }
        catch (const std::exception& e) {
            lastError = e.what();
            return false;
        }
    }

    essentia::standard::Algorithm* ew_get_tf_model() {
        try {
            if (!hasInitialized) {
                essentia::init();
                hasInitialized = true;
            }

            essentia::standard::AlgorithmFactory& factory = essentia::standard::AlgorithmFactory::instance();
            if (tfModelInstance != nullptr) {
                return tfModelInstance;
            }
            tfModelInstance = factory.create("TensorflowPredictEffnetDiscogs");
            return tfModelInstance;
        }
        catch (const std::exception& e) {
            return nullptr;
        }
    }

    bool ew_configure_tf_model(const char* model_path) {
        try {
            ew_get_tf_model()->configure("graphFilename", std::string(model_path), "output", "PartitionedCall:1");
            return true;
        }
        catch (const std::exception& e) {
            lastError = e.what();
            return false;
        }
    }

    int ew_run_inference() {
        try {
            monoLoaderInstance->reset();
            tfModelInstance->reset();
            lastEmbeddings.clear();

            std::vector<essentia::Real> audioBuffer;
            monoLoaderInstance->output("audio").set(audioBuffer);
            tfModelInstance->input("signal").set(audioBuffer);
            tfModelInstance->output("predictions").set(lastEmbeddings);

            monoLoaderInstance->compute();
            if (audioBuffer.empty()) {
                lastError = "Error: Audio buffer is empty after loading.";
                return -1;
            }
            tfModelInstance->compute();
            return 0;
        }
        catch (const std::exception& e) {
            lastError = e.what();
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
