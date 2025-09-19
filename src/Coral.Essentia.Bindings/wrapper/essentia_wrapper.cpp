#include <pch.h>

#include "essentia_wrapper.h"
#include "essentia_context.h"


extern "C" {
    namespace {
        // context ID -> context
        int current_context_id = 0;
        std::map<int, EssentiaContext*> context;
        std::mutex context_mutex;
        bool essentiaInit = false;
    }

    int ew_create_context() {
        std::lock_guard<std::mutex> guard(context_mutex);
        if (!essentiaInit) {
            essentia::init();
            essentiaInit = true;
        }

        current_context_id += 1;
        EssentiaContext* ctx = new EssentiaContext();
        ctx->set_context_id(current_context_id);
        context.insert(std::make_pair(current_context_id, ctx));
        std::cout << "[Coral Essentia Wrapper] Creating context: " << current_context_id << "\n";
        std::cout << "[Coral Essentia Wrapper] Context length: " << context.size() << "\n";
        return current_context_id;
    }

    EssentiaContext* get_context(int context_id) {        
        // std::cout << "[Coral Essentia Wrapper] Getting context " << current_context_id << "\n";
        return context.at(context_id);
    }

    void ew_destroy_context(int context_id) {
        auto ctx = get_context(context_id);
        ctx->clean_up();
        delete ctx;
    }

    bool ew_get_error(int context_id, char* buffer, int buffer_size) {
        return get_context(context_id)->get_error(buffer, buffer_size);
    }

    int ew_get_error_length(int context_id) {
        return get_context(context_id)->get_error_length();
    }

    bool ew_configure_tf_model(int context_id, const char* model_path) {
        return get_context(context_id)->configure_tf_model(model_path);
    }

    int ew_run_inference(int context_id, const char* audioFile, int sampleRate, int resampleQuality) {
        return get_context(context_id)->run_inference(audioFile, sampleRate, resampleQuality);
    }

    int ew_get_embedding_count(int context_id) {
        return get_context(context_id)->get_embedding_count();
    }

    int ew_get_embedding_size(int context_id) {
        return get_context(context_id)->get_embedding_size();
    }

    int ew_get_total_embedding_elements(int context_id) {
        return get_context(context_id)->get_total_embedding_elements();
    }

    bool ew_get_embeddings_flattened(int context_id, float* out_buffer, int buffer_size) {
        return get_context(context_id)->get_embeddings_flattened(out_buffer, buffer_size);
    }
}
