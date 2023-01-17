using System.Diagnostics;
using CliWrap;
using Coral.Configuration;
using Coral.Encoders.EncodingModels;

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
    
    public static Command GetHlsPipeCommand(TranscodingJob job)
    {
        var jobDir = Path.Combine(ApplicationConfiguration.HLSDirectory, job.Id.ToString());
        Directory.CreateDirectory(jobDir);
        return Cli.Wrap("ffmpeg")
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .WithArguments(new[]
            {
                "-i",
                "-",
                "-loglevel",
                "error",
                "-hide_banner",
                "-acodec",
                "copy",
                "-f",
                "hls",
                "-hls_time",
                "5",
                "-hls_playlist_type",
                "event",
                "-hls_segment_filename",
                $"{Path.Combine(jobDir, "chunk-%02d.ts")}",
                "-master_pl_name",
                $"{job.FinalOutputFile}",
                $"{Path.Combine(jobDir, "playlist.m3u8")}"
            });
    }
}