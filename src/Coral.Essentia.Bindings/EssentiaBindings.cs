using System.Runtime.InteropServices;

namespace Coral.Essentia.Bindings;

public static class EssentiaBindings
{
    private const string DllName = "essentia_bindings.dll";

    /// <summary>
    /// Clean up resources and shutdown Essentia
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ew_clean_up();

    /// <summary>
    /// Create a MonoLoader instance
    /// </summary>
    /// <returns>Pointer to MonoLoader instance, or IntPtr.Zero on failure</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ew_create_mono_loader();

    /// <summary>
    /// Configure the MonoLoader with filename and sample rate
    /// </summary>
    /// <param name="instance">MonoLoader instance pointer</param>
    /// <param name="filename">Path to audio file</param>
    /// <param name="sampleRate">Sample rate for audio processing</param>
    /// <returns>True if configuration successful, false otherwise</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ew_configure_mono_loader(IntPtr instance, 
        [MarshalAs(UnmanagedType.LPStr)] string filename, 
        int sampleRate);

    /// <summary>
    /// Create a TensorFlow model instance
    /// </summary>
    /// <returns>Pointer to TensorFlow model instance, or IntPtr.Zero on failure</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ew_create_tf_model();

    /// <summary>
    /// Configure the TensorFlow model with model path
    /// </summary>
    /// <param name="instance">TensorFlow model instance pointer</param>
    /// <param name="modelPath">Path to the TensorFlow model file</param>
    /// <returns>True if configuration successful, false otherwise</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ew_configure_tf_model(IntPtr instance, 
        [MarshalAs(UnmanagedType.LPStr)] string modelPath);

    /// <summary>
    /// Run inference on loaded audio using the configured model
    /// </summary>
    /// <returns>0 on success, -1 on failure</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ew_run_inference();

    /// <summary>
    /// Get the number of embedding vectors (outer dimension)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ew_get_embedding_count();

    /// <summary>
    /// Get the size of each embedding vector (inner dimension)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ew_get_embedding_size();

    /// <summary>
    /// Get total number of float elements across all embeddings
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ew_get_total_embedding_elements();

    /// <summary>
    /// Get all embeddings flattened into a 1D array (row-major order)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ew_get_embeddings_flattened([Out] float[] outBuffer, int bufferSize);
}