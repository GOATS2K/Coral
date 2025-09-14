// --- The Firewall ---
// This is the final fix for the persistent <ctime> build error.
// We are manually including the standard C headers BEFORE any Essentia
// code. This ensures the compiler loads the correct system definitions
// before the conflicting headers from Essentia's dependencies can
// interfere.
#include <pch.h>

// --- ew Header ---
#include "essentia_wrapper.h"

// --- Essentia Headers (ISOLATED HERE) ---
#include "essentia/algorithmfactory.h"
#include "essentia/essentiamath.h"
#include "essentia/pool.h"
#include <essentia/essentia.h>
#include <essentia/debugging.h>


// --- Global State ---
// This state is now internal to the ew and hidden from the main wrapper.
namespace {
    essentia::standard::Algorithm* monoLoaderInstance = nullptr;
    essentia::standard::Algorithm* tfModelInstance = nullptr;
    std::vector<essentia::Real> lastEmbeddings;
}


// --- ew Function Implementations ---

void ew_init() {
    try {
        essentia::init();
    }
    catch (const std::exception& e) {
        std::cerr << "Error during Essentia initialization: " << e.what() << std::endl;
    }
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
    if (!instance || instance != monoLoaderInstance) return false;
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
    if (!instance || instance != tfModelInstance) return false;
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
    if (!monoLoaderInstance || !tfModelInstance) return -1;
    try {
        monoLoaderInstance->reset();
        tfModelInstance->reset();

        std::vector<essentia::Real> audioBuffer;
        monoLoaderInstance->output("audio").set(audioBuffer);
        tfModelInstance->input("audio").set(audioBuffer);
        tfModelInstance->output("embeddings").set(lastEmbeddings);

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

int ew_get_embedding_size() {
    return static_cast<int>(lastEmbeddings.size());
}

bool ew_get_embeddings(float* out_buffer, int buffer_size) {
    if (!out_buffer || lastEmbeddings.empty()) return false;
    if (static_cast<int>(lastEmbeddings.size()) > buffer_size) return false;
    std::copy(lastEmbeddings.begin(), lastEmbeddings.end(), out_buffer);
    return true;
}
