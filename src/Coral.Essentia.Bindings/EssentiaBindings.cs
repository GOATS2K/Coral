using System.Runtime.InteropServices;
using System.Text;

namespace Coral.Essentia.Bindings;

internal static class EssentiaBindings
{
    private const string DllName = "essentia_bindings.dll";

    /// <summary>
    /// Clean up resources and shutdown Essentia
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ew_clean_up();

    /// <summary>
    /// Configure the MonoLoader with filename and sample rate
    /// </summary>
    /// <param name="filename">Path to audio file</param>
    /// <param name="sampleRate">Sample rate for audio processing</param>
    /// <param name="resampleQuality">Resampling accuracy, lower number for higher accuracy, trading performance.</param>
    /// <returns>True if configuration successful, false otherwise</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ew_configure_mono_loader(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filename,
        int sampleRate,
        int resampleQuality);

    /// <summary>
    /// Configure the TensorFlow model with model path
    /// </summary>
    /// <param name="modelPath">Path to the TensorFlow model file</param>
    /// <returns>True if configuration successful, false otherwise</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ew_configure_tf_model([MarshalAs(UnmanagedType.LPUTF8Str)] string modelPath);

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

    /// <summary>
    /// Alternative: Copy error message to byte array buffer
    /// </summary>
    /// <param name="buffer">Buffer to receive error message (as byte array)</param>
    /// <param name="bufferSize">Size of buffer</param>
    /// <returns>True if successful, false if buffer too small or error occurred</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ew_get_error([Out] byte[] buffer, int bufferSize);

    /// <summary>
    /// Get the length of the current error message
    /// </summary>
    /// <returns>Length of error message in characters (excluding null terminator)</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ew_get_error_length();
}