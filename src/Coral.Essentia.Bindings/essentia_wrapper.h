#pragma once

// This header declares the C-style functions that are exported from the
// EssentiaWrapper DLL for use with P/Invoke in C#.

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

    WRAPPER_API void ew_init();
    WRAPPER_API void ew_clean_up();
    WRAPPER_API void* ew_create_mono_loader();
    WRAPPER_API bool ew_configure_mono_loader(void* instance, const char* filename, int sampleRate);
    WRAPPER_API void* ew_create_tf_model();
    WRAPPER_API bool ew_configure_tf_model(void* instance, const char* model_path);
    WRAPPER_API int ew_run_inference();
    WRAPPER_API int ew_get_embedding_size();
    WRAPPER_API bool ew_get_embeddings(float* out_buffer, int buffer_size);

#ifdef __cplusplus
}
#endif

