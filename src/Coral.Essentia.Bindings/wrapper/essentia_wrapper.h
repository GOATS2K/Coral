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

    WRAPPER_API bool ew_configure_mono_loader(const char* filename, int sampleRate);
    WRAPPER_API bool ew_configure_tf_model(const char* model_path);
    WRAPPER_API int ew_run_inference();
    WRAPPER_API int ew_get_embedding_size();
    WRAPPER_API int ew_get_embedding_count();
    WRAPPER_API int ew_get_embedding_size(); 
    WRAPPER_API int ew_get_total_embedding_elements();
    WRAPPER_API bool ew_get_embeddings_flattened(float* out_buffer, int buffer_size);
    WRAPPER_API void ew_clean_up();
    WRAPPER_API bool ew_get_error(char* buffer, int buffer_size);
    WRAPPER_API int ew_get_error_length();



#ifdef __cplusplus
}
#endif

