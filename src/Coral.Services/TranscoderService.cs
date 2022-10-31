using Coral.Database.Models;
using Coral.Services.EncoderFrontend;
using Coral.Services.HelperModels;
using Coral.Services.Helpers;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;

namespace Coral.Services;

public interface ITranscoderService
{
    public TrackStream Transcode(Track track, OutputFormat format = OutputFormat.AAC, int bitrate = 256);
}

public class TranscoderService : ITranscoderService
{
    private readonly IEncoderFactory _encoderFactory;

    public TranscoderService(IEncoderFactory encoderFactory)
    {
        _encoderFactory = encoderFactory;
    }

    private string GetExtensionForCodec(OutputFormat codec)
    {
        return codec switch
        {
            OutputFormat.MP3 => "mp3",
            OutputFormat.AAC => "m4a",
            OutputFormat.Ogg => "ogg",
            OutputFormat.Opus => "webm",
            _ => "tmp"
        };
    }
    
    public TrackStream Transcode(Track track, OutputFormat format = OutputFormat.AAC, int bitrate = 256)
    {
        var encoder = _encoderFactory
            .GetEncoder(format);

        if (encoder == null)
        {
            throw new ApplicationException("Unable to get encoder for platform.");
        }
        
        var fileStream = encoder
            .Configure()
            .SetBitrate(bitrate)
            .SetSourceFile(track.FilePath)
            .Transcode();

        return new TrackStream()
        {
            ContentType = MimeTypeHelper.GetMimeTypeForCodec(format),
            FileName = $"{track.Id}.{GetExtensionForCodec(format)}",
            Stream = fileStream
        };
    }
}