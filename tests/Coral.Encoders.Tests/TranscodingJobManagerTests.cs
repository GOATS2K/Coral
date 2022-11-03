using Coral.Database.Models;
using Coral.Encoders.EncodingModels;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Coral.Encoders.Tests
{
    public class TranscodingJobManagerTests
    {
        private readonly ITranscodingJobManager _transcodingJobManager;
        private readonly IEncoderFactory _encoderFactory;
        public Track TestTrack { get; } = new Track()
        {
            Id = 1,
            Artist = new Artist()
            {
                Id = 1,
                Name = "Test Artist 1",
                DateIndexed = DateTime.UtcNow
            },
            Album = new Album()
            {
                Id = 1,
                Name = "Test Album 1",
                DateIndexed = DateTime.UtcNow
            },
            Title = "A Nice Song",
            DurationInSeconds = 30,
            FilePath = "this/file/does/not/exist"
        };

        public TranscodingJobManagerTests()
        {
            _encoderFactory = Substitute.For<IEncoderFactory>();
            _transcodingJobManager = new TranscodingJobManager(_encoderFactory);
        }

        [Fact]
        public async Task CreateJob_MissingEncoder_ReturnsArgumentException()
        {
            // arrange
            _encoderFactory.GetPlatform().Returns(Platform.Windows);
            _encoderFactory.GetEncoder(OutputFormat.MP3).Returns(opt => null);

            // assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _transcodingJobManager.CreateJob(OutputFormat.MP3, opt =>
            {
                opt.Bitrate = 320;
                opt.SourceTrack = TestTrack;
                opt.RequestType = TranscodeRequestType.SingleFile;
            }));
        }
    }
}
