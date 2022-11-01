using Coral.Encoders;
using Coral.Services.HelperModels;

namespace Coral.Services.Helpers;

public static class MimeTypeHelper
{
    public static string GetMimeTypeForExtension(string fileExtension)
    {
        return fileExtension switch
        {
            ".flac" => "audio/flac",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            _ => "application/octet-stream"
        };
    }

    public static string GetMimeTypeForCodec(OutputFormat codec)
    {
        return codec switch
        {
            OutputFormat.MP3 => "audio/mpeg",
            OutputFormat.AAC => "audio/x-m4a",
            OutputFormat.Ogg => "audio/ogg",
            OutputFormat.Opus => "audio/webm;codecs=\"opus\"",
            _ => "application/octet-stream"
        };
    }

}