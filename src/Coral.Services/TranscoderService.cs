using Coral.Database.Models;
using Coral.Services.HelperModels;
using Coral.Services.Helpers;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;

namespace Coral.Services;

public interface ITranscoderService
{
    // public Task<TrackStream> Transcode(Track track, OutputFormat format = OutputFormat.AAC, int bitrate = 256);
}

// it really would be nice to use ffmpeg,
// but the existing wrappers don't function as expected on macOS
public class TranscoderService : ITranscoderService
{
    private string GetExtensionForCodec(string codec)
    {
        return codec switch
        {
            "mp3" => ".mp3",
            "aac" => ".m4a",
            "ogg" => ".ogg",
            "opus" => ".webm",
            _ => "application/octet-stream"
        };
    }
    
    // public async Task<TrackStream> Transcode(Track track, OutputFormat format = OutputFormat.AAC, int bitrate = 256)
    // {
    //     var outputStream = new MemoryStream();
    //
    //     return new TrackStream()
    //     {
    //         ContentType = MimeTypeHelper.GetMimeTypeForCodec(requestedCodec),
    //         FileName = $"{track.Id}.{GetExtensionForCodec(codec)}",
    //         Stream = outputStream
    //     };
    // }
}