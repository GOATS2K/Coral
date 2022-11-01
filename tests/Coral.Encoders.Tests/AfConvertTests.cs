using Coral.Encoders.AAC;
using Coral.TestProviders;
using Xunit;

namespace Coral.Encoders.Tests;

public class AfConvertTests
{
    [SkippableFact]
    public void AfConvert_TranscodeWithBuilder_ReturnsExitCodeZero()
    {
        // arrange
        Skip.IfNot(OperatingSystem.IsMacOS(), "AfConvert only runs on macOS.");
        var targetFile = new DirectoryInfo(TestDataRepository.NeptuneDiscovery)
            .GetFiles("*.flac", SearchOption.TopDirectoryOnly)
            .First();
        var destinationFile = Path.Join(targetFile.DirectoryName, "converted.m4a");

        // act
        var encoder = new AfConvert();
        var transcodedStream = encoder
            .Configure()
            .SetBitrate(256)
            .SetSourceFile(targetFile.FullName)
            .SetDestinationFile(destinationFile)
            .Transcode();

        // assert
        Assert.NotNull(transcodedStream);
        Assert.NotEqual(0, transcodedStream.Length);
        Assert.True(File.Exists(destinationFile));

        // cleanup - delete destination file
        File.Delete(destinationFile);
    }
}