#pragma once
#ifndef WRAPPER_API
#if defined(_WIN32)
#define WRAPPER_API __declspec(dllexport)
#else
#define WRAPPER_API __attribute__((visibility("default")))
#endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

    // --- Function Declarations ---

    WRAPPER_API int ew_create_context();
    WRAPPER_API void ew_destroy_context(int context_id);
    WRAPPER_API bool ew_configure_tf_model(int context_id, const char* model_path);
    WRAPPER_API int ew_run_inference(int context_id, const char* audioFile, int sampleRate, int resampleQuality);
    WRAPPER_API int ew_get_embedding_size(int context_id);
    WRAPPER_API int ew_get_embedding_count(int context_id);
    WRAPPER_API int ew_get_embedding_size(int context_id);
    WRAPPER_API int ew_get_total_embedding_elements(int context_id);
    WRAPPER_API bool ew_get_embeddings_flattened(int context_id, float* out_buffer, int buffer_size);
    WRAPPER_API bool ew_get_error(int context_id, char* buffer, int buffer_size);
    WRAPPER_API int ew_get_error_length(int context_id);



#ifdef __cplusplus
}
#endif

