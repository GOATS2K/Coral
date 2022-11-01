using System.Diagnostics;

namespace Coral.Encoders;

public static class CommonEncoderMethods
{
    public static bool CheckEncoderExists(string fileName)
    {
        var startInfo = new ProcessStartInfo()
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var process = Process.Start(startInfo);
        return process != null;
    }
}