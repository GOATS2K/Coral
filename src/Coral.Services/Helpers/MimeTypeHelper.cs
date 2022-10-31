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
    
    public static string GetMimeTypeForCodec(string codec)
    {
        return codec switch
        {
            "mp3" => "audio/mpeg",
            "aac" => "audio/mp4",
            "ogg" => "audio/ogg",
            "opus" => "audio/webm;codecs=\"opus\"",
            _ => "application/octet-stream"
        };
    }

}