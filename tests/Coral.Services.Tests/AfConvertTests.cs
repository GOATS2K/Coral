using Coral.Services.EncoderFrontend.AAC;
using Xunit;

namespace Coral.Services.Tests;

public class AfConvertTests
{
    [Fact]
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
        var process = encoder
            .Configure()
            .SetBitrate(256)
            .SetSourceFile(targetFile.FullName)
            .SetDestinationFile(destinationFile)
            .Transcode();

        // assert
        Assert.Equal(0, process.ExitCode);
        Assert.True(File.Exists(destinationFile));
        
        // cleanup - delete destination file
        File.Delete(destinationFile);
    }
}