using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using CliWrap.Buffered;

namespace Coral.Services.Helpers;

public static class Ffprobe
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<FfprobeResult?> GetAudioMetadata(string filePath)
    {
        try
        {
            var result = await Cli.Wrap("ffprobe")
                .WithArguments(args => args
                    .Add("-v").Add("quiet")
                    .Add("-print_format").Add("json")
                    .Add("-show_format")
                    .Add("-show_streams")
                    .Add(filePath))
                .ExecuteBufferedAsync();

            if (result.ExitCode != 0)
            {
                return null;
            }

            var ffprobeResult = JsonSerializer.Deserialize<FfprobeResult>(result.StandardOutput, JsonOptions);
            return ffprobeResult;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public class FfprobeResult
{
    [JsonPropertyName("streams")]
    public List<FfprobeStream> Streams { get; set; } = new();

    [JsonPropertyName("format")]
    public FfprobeFormat Format { get; set; } = new();
}

public class FfprobeStream
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("codec_name")]
    public string CodecName { get; set; } = string.Empty;

    [JsonPropertyName("codec_long_name")]
    public string CodecLongName { get; set; } = string.Empty;

    [JsonPropertyName("codec_type")]
    public string CodecType { get; set; } = string.Empty;

    [JsonPropertyName("sample_rate")]
    public string? SampleRate { get; set; }

    [JsonPropertyName("channels")]
    public int? Channels { get; set; }

    [JsonPropertyName("channel_layout")]
    public string? ChannelLayout { get; set; }

    [JsonPropertyName("bits_per_sample")]
    public int? BitsPerSample { get; set; }

    [JsonPropertyName("bits_per_raw_sample")]
    public string? BitsPerRawSample { get; set; }

    [JsonPropertyName("bit_rate")]
    public string? BitRate { get; set; }

    [JsonPropertyName("start_time")]
    public string? StartTime { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("initial_padding")]
    public int? InitialPadding { get; set; }
}

public class FfprobeFormat
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("format_name")]
    public string FormatName { get; set; } = string.Empty;

    [JsonPropertyName("format_long_name")]
    public string FormatLongName { get; set; } = string.Empty;

    [JsonPropertyName("start_time")]
    public string? StartTime { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("bit_rate")]
    public string? BitRate { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}
