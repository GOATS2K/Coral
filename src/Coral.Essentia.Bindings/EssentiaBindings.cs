using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Coral.Essentia.Bindings;

public static partial class EssentiaBindings
{
    private const string DllName = "essentia_bindings.dll";

    /// <summary>
    /// Clean up resources
    /// </summary>
    [LibraryImport(DllName)]
    [UnmanagedCallConv(
        CallConvs = [typeof(CallConvCdecl)])]
    public static partial void ew_destroy_context(int context_id);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(
        CallConvs = [typeof(CallConvCdecl)])]
    public static partial int ew_create_context();

    /// <summary>
    /// Configure the TensorFlow model with model path
    /// </summary>
    /// <param name="modelPath">Path to the TensorFlow model file</param>
    /// <returns>True if configuration successful, false otherwise</returns>
    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(
        CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool ew_configure_tf_model(int context_id, [MarshalAs(UnmanagedType.LPUTF8Str)] string modelPath);

    /// <summary>
    /// Run inference on loaded audio using the configured model
    /// </summary>
    /// <returns>0 on success, -1 on failure</returns>
    [LibraryImport(DllName)]
    [UnmanagedCallConv(
        CallConvs = [typeof(CallConvCdecl)])]
    public static partial int ew_run_inference(int context_id, [MarshalAs(UnmanagedType.LPUTF8Str)] string audioFile,
        int sampleRate, int resampleQuality);

    /// <summary>
    /// Get the number of embedding vectors (outer dimension)
    /// </summary>
    [LibraryImport(DllName)]
    [UnmanagedCallConv(
        CallConvs = [typeof(CallConvCdecl)])]
    public static partial int ew_get_embedding_count(int context_id);

    /// <summary>
    /// Get the size of each embedding vector (inner dimension)
    /// </summary>
    [LibraryImport(DllName)]
    [UnmanagedCallConv(
        CallConvs = [typeof(CallConvCdecl)])]
    public static partial int ew_get_embedding_size(int context_id);

    /// <summary>
    /// Get total number of float elements across all embeddings
    /// </summary>
    [LibraryImport(DllName)]
    [UnmanagedCallConv(
        CallConvs = [typeof(CallConvCdecl)])]
    public static partial int ew_get_total_embedding_elements(int context_id);

    /// <summary>
    /// Get all embeddings flattened into a 1D array (row-major order)
    /// </summary>
    [LibraryImport(DllName)]
    [UnmanagedCallConv(
        CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool ew_get_embeddings_flattened(int context_id, [Out] float[] outBuffer, int bufferSize);

    /// <summary>
    /// Alternative: Copy error message to byte array buffer
    /// </summary>
    /// <param name="buffer">Buffer to receive error message (as byte array)</param>
    /// <param name="bufferSize">Size of buffer</param>
    /// <returns>True if successful, false if buffer too small or error occurred</returns>
    [LibraryImport(DllName)]
    [UnmanagedCallConv(
        CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool ew_get_error(int context_id, [Out] byte[] buffer, int bufferSize);

    /// <summary>
    /// Get the length of the current error message
    /// </summary>
    /// <returns>Length of error message in characters (excluding null terminator)</returns>
    [LibraryImport(DllName)]
    [UnmanagedCallConv(
        CallConvs = [typeof(CallConvCdecl)])]
    public static partial int ew_get_error_length(int context_id);
}