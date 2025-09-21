using System.Runtime.InteropServices;
using System.Security;

namespace Coral.Essentia;

public static unsafe class Libsamplerate
{
    private const string DllName = @"C:\Projects\essentia\packaging\msvc\bin\samplerate.dll";
    private const CallingConvention CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl;
    public enum ConverterType
    {
        SRC_SINC_BEST_QUALITY = 0,
        SRC_SINC_MEDIUM_QUALITY = 1,
        SRC_SINC_FASTEST = 2,
        SRC_ZERO_ORDER_HOLD = 3,
        SRC_LINEAR = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SRC_DATA
    {
        public float* data_in;
        public float* data_out;
        public int input_frames;
        public int output_frames;
        public int input_frames_used;
        public int output_frames_generated;
        public int end_of_input;
        public double src_ratio;
    }

    [SuppressUnmanagedCodeSecurity]
    [DllImport(DllName, CallingConvention = CallingConvention)]
    public static extern IntPtr src_new(ConverterType converter_type, int channels, out int error);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(DllName, CallingConvention = CallingConvention)]
    public static extern IntPtr src_delete(IntPtr state);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(DllName, CallingConvention = CallingConvention)]
    public static extern int src_process(IntPtr state, ref SRC_DATA data);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(DllName, CallingConvention = CallingConvention)]
    public static extern int src_reset(IntPtr state);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(DllName, CallingConvention = CallingConvention, CharSet = CharSet.Ansi)]
    public static extern string src_strerror(int error);
}