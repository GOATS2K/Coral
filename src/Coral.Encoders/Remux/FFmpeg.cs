using CliWrap;
using Coral.Configuration;
using Coral.Dto.EncodingModels;
using Coral.Encoders.EncodingModels;

namespace Coral.Encoders.Remux;

[EncoderFrontend(nameof(RemuxFFmpeg), OutputFormat.Remux,
    Platform.Windows, Platform.Linux, Platform.MacOS)]
public class RemuxFFmpeg : IEncoder
{
    public string ExecutableName => "ffmpeg";
    public bool WritesOutputToStdErr => true;

    public bool EnsureEncoderExists()
    {
        return CommonEncoderMethods.CheckEncoderExists(ExecutableName);
    }

    public IArgumentBuilder Configure()
    {
        // Not used - we override ConfigureTranscodingJob completely
        throw new NotImplementedException("RemuxFFmpeg builds commands directly in ConfigureTranscodingJob");
    }

    public TranscodingJob ConfigureTranscodingJob(TranscodingJobRequest request)
    {
        var job = new TranscodingJob
        {
            Request = request,
        };

        if (request.RequestType == TranscodeRequestType.HLS)
        {
            job.OutputDirectory = Path.Combine(
                ApplicationConfiguration.HLSDirectory,
                job.Id.ToString()
            );
            Directory.CreateDirectory(job.OutputDirectory);

            job.FinalOutputFile = "playlist.m3u8";
            var playlistPath = Path.Combine(job.OutputDirectory, job.FinalOutputFile);

            // Detect source codec from file extension
            var sourceCodec = GetSourceCodec(request.SourceTrack.AudioFile.FilePath);

            // Build FFmpeg command arguments
            var args = new List<string>
            {
                "-i", request.SourceTrack.AudioFile.FilePath,
                "-map", "0:a",  // Select only audio stream (ignore embedded artwork)
                "-c:a", "copy",
                "-movflags", "frag_keyframe+empty_moov+default_base_moof",
                "-frag_duration", "10000000",  // 10 seconds in microseconds
                "-f", "hls",
                "-hls_time", "10",  // 10 second segments (matches frag_duration)
                "-hls_playlist_type", "event",
                "-hls_segment_type", "fmp4",
                "-hls_flags", "single_file",
            };

            // Add bitstream filter only for AAC
            if (sourceCodec == "aac")
            {
                args.Add("-bsf:a");
                args.Add("aac_adtstoasc");
            }

            // Add playlist output path
            args.Add(playlistPath);

            // Build single FFmpeg command (no pipe needed)
            job.TranscodingCommand = Cli.Wrap(ExecutableName)
                .WithArguments(args);

            // No pipe command needed for remux
            job.PipeCommand = null;
        }
        else
        {
            throw new NotSupportedException(
                $"RemuxFFmpeg only supports HLS transcoding. Requested: {request.RequestType}"
            );
        }

        return job;
    }

    private string GetSourceCodec(string filePath)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "flac" => "flac",
            "m4a" or "aac" => "aac",
            "mp3" => "mp3",
            _ => throw new NotSupportedException(
                $"RemuxFFmpeg only supports FLAC, AAC, and MP3 files. File extension: {ext}"
            )
        };
    }
}
