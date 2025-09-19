#include <pch.h>
#include <essentia_context.h>

void EssentiaContext::clean_up() {
    delete tfModelInstance;
    tfModelInstance = nullptr;
    lastEmbeddings.clear();
}

void EssentiaContext::set_context_id(int context_id) {
    EssentiaContext::context_id = context_id;
}

bool EssentiaContext::get_error(char* buffer, int buffer_size) {
    if (!buffer || buffer_size <= 0) {
        log("No buffer/buffer_size detected.");
        return false;
    }

    if (lastError.length() >= static_cast<size_t>(buffer_size)) {
        log("Buffer too small.");
        return false; // Buffer too small
    }

    strcpy_s(buffer, buffer_size, lastError.c_str());
    return true;
}

int EssentiaContext::get_error_length() {
    // strcpy_s needs an extra byte for the null terminator.
    int length = lastError.length();
    int lengthWithNullTerminator = length + 1;
    return lengthWithNullTerminator;
}

void EssentiaContext::log(std::string message) {
    auto header = "[Coral Essentia Wrapper - " + std::string("Context ") + std::to_string(context_id) + "] ";
    std::cout << header << message << "\n";
}

essentia::standard::Algorithm* EssentiaContext::get_tf_model() {
    try {
        essentia::standard::AlgorithmFactory& factory = essentia::standard::AlgorithmFactory::instance();
        if (tfModelInstance != nullptr) {
            return tfModelInstance;
        }
        log("Creating TensorFlow algorithm.");
        tfModelInstance = factory.create("TensorflowPredictEffnetDiscogs");
        return tfModelInstance;
    }
    catch (const std::exception& e) {
        return nullptr;
    }
}

bool EssentiaContext::configure_tf_model(const char* model_path) {
    try {
        log("Loading model: " + std::string(model_path));
        auto model = get_tf_model();
        model->configure("graphFilename", std::string(model_path), "output", "PartitionedCall:1");
        return true;
    }
    catch (const std::exception& e) {
        lastError = e.what();
        return false;
    }
}

int EssentiaContext::run_inference(const char* audioFile, int sampleRate, int resampleQuality) {
    try {
        auto startTime = std::chrono::system_clock::now();

        tfModelInstance->reset();
        lastEmbeddings.clear();

        essentia::standard::AlgorithmFactory& factory = essentia::standard::AlgorithmFactory::instance();
        essentia::standard::Algorithm* monoLoader = factory.create("MonoLoader");
        monoLoader->configure("filename", std::string(audioFile), "sampleRate", sampleRate, "resampleQuality", resampleQuality);
        std::vector<essentia::Real> audioBuffer;
        monoLoader->output("audio").set(audioBuffer);
        tfModelInstance->input("signal").set(audioBuffer);
        tfModelInstance->output("predictions").set(lastEmbeddings);

        monoLoader->compute();
        std::chrono::duration<double> audioComputationTime = std::chrono::system_clock::now() - startTime;
        auto audioInferenceCompletedTime = std::chrono::system_clock::now();
        log("Audio computation completed in " + std::to_string(audioComputationTime.count()) + " seconds");
        if (audioBuffer.empty()) {
            lastError = "Error: Audio buffer is empty after loading.";
            return -1;
        }
        tfModelInstance->compute();
        std::chrono::duration<double> inferenceComputationTime = std::chrono::system_clock::now() - audioInferenceCompletedTime;
        log("Inference completed in " + std::to_string(inferenceComputationTime.count()) + " seconds");

        return 0;
    }
    catch (const std::exception& e) {
        lastError = e.what();
        return -1;
    }
}

// Get the number of embedding vectors (outer dimension)
int EssentiaContext::get_embedding_count() {
    return static_cast<int>(lastEmbeddings.size());
}

// Get the size of each embedding vector (inner dimension)
int EssentiaContext::get_embedding_size() {
    if (lastEmbeddings.empty()) return 0;
    return static_cast<int>(lastEmbeddings[0].size());
}

// Get total number of floats (count * size)
int EssentiaContext::get_total_embedding_elements() {
    if (lastEmbeddings.empty()) return 0;
    return static_cast<int>(lastEmbeddings.size() * lastEmbeddings[0].size());
}

// Option 1: Flatten the 2D array into a 1D array (row-major order)
bool EssentiaContext::get_embeddings_flattened(float* out_buffer, int buffer_size) {
    if (!out_buffer || lastEmbeddings.empty()) return false;

    int totalElements = EssentiaContext::get_total_embedding_elements();
    if (totalElements > buffer_size) return false;

    int idx = 0;
    for (const auto& embedding : lastEmbeddings) {
        std::copy(embedding.begin(), embedding.end(), out_buffer + idx);
        idx += embedding.size();
    }
    return true;
}