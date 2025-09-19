#pragma once

#include "essentia/algorithmfactory.h"
#include "essentia/essentiamath.h"
#include "essentia/pool.h"
#include <essentia/essentia.h>
#include <essentia/debugging.h>


class EssentiaContext {
    essentia::standard::Algorithm* tfModelInstance = nullptr;
    std::vector<std::vector<float>> lastEmbeddings;
    std::string lastError = "";
    int context_id;
public:
    void set_context_id(int context_id);
    void clean_up();
    bool get_error(char* buffer, int buffer_size);
    int get_error_length();
    essentia::standard::Algorithm* get_tf_model();
    bool configure_tf_model(const char* model_path);
    int run_inference(const char* audioFile, int sampleRate, int resampleQuality);
    int get_embedding_count();
    int get_embedding_size();
    int get_total_embedding_elements();
    bool get_embeddings_flattened(float* out_buffer, int buffer_size);
private:
    void log(std::string message);
};
