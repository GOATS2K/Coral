using Coral.Services.EncoderFrontend;
using Coral.Services.EncoderFrontend.AAC;
using Coral.Services.HelperModels;
using NSubstitute;
using NSubstitute.Extensions;
using Xunit;

namespace Coral.Services.Tests;

public class EncoderFactoryTests
{
    private readonly IEncoderFactory _encoderFactory;

    public EncoderFactoryTests()
    {
        _encoderFactory = Substitute.ForPartsOf<EncoderFactory>();
    }
    
    [Fact]
    public void GetEncoder_AACOnMacOS_ReturnsAfConvert()
    {
        // arrange
        _encoderFactory.Configure().GetPlatform().Returns(Platform.MacOS);

        // act
        var encoder = _encoderFactory.GetEncoder(OutputFormat.AAC);

        // assert
        var encoderType = encoder.GetType();
        Assert.Equal(nameof(AfConvert), encoderType.Name);
    }
    
    [Fact]
    public void GetEncoder_AACOnWindows_ReturnsQaac()
    {
        // arrange
        _encoderFactory.Configure().GetPlatform().Returns(Platform.Windows);

        // act
        var encoder = _encoderFactory.GetEncoder(OutputFormat.AAC);

        // assert
        var encoderType = encoder.GetType();
        Assert.Equal(nameof(Qaac), encoderType.Name);
    }
}