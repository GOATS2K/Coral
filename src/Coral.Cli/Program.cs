// See https://aka.ms/new-console-template for more information

using Coral.Services;
using Coral.Services.Helpers;

var trackPath = @"C:\Music\Codec Test";
var files = Directory.EnumerateFiles(trackPath, "*.*", SearchOption.AllDirectories);
foreach (var file in files)
{
    Console.WriteLine(file);
    var t = await Ffprobe.GetAudioMetadata(file);
    var bitrate = int.Parse(t!.Format.BitRate!) / 1000;
    var audioStream = t.Streams.First(a => a.CodecType == "audio");
    Console.WriteLine($"{bitrate} kbps / {audioStream.CodecName} / {audioStream.CodecLongName}\n");
}