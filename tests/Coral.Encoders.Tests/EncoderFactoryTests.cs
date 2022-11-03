using Coral.Encoders.AAC;
using Coral.Encoders.EncodingModels;
using NSubstitute;
using NSubstitute.Extensions;
using Xunit;

namespace Coral.Encoders.Tests;

public class EncoderFactoryTests
{
    private readonly IEncoderFactory _encoderFactory;

    public EncoderFactoryTests()
    {
        _encoderFactory = Substitute.ForPartsOf<EncoderFactory>();
    }

    [Fact]
    public void GetEncoder_AACOnMacOS_ReturnsFFMPEG()
    {
        // arrange
        _encoderFactory.Configure().GetPlatform().Returns(Platform.MacOS);

        // act
        var encoder = _encoderFactory.GetEncoder(OutputFormat.AAC);

        // assert
        Assert.NotNull(encoder);
        var encoderType = encoder.GetType();
        Assert.Equal(nameof(FfmpegForMacOS), encoderType.Name);
    }

    [Fact]
    public void GetEncoder_AACOnWindows_ReturnsQaac()
    {
        // arrange
        _encoderFactory.Configure().GetPlatform().Returns(Platform.Windows);

        // act
        var encoder = _encoderFactory.GetEncoder(OutputFormat.AAC);

        // assert
        Assert.NotNull(encoder);
        var encoderType = encoder.GetType()!;
        Assert.Equal(nameof(Qaac), encoderType.Name);
    }
}