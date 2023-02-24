﻿using Coral.Database.Models;
using Coral.Encoders.EncodingModels;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coral.Encoders;
using Xunit;
using Coral.Dto.EncodingModels;

namespace Coral.Services.Tests
{
    public class TranscodingJobManagerTests
    {
        private readonly ITranscoderService _transcoderService;
        private readonly IEncoderFactory _encoderFactory;
        public Track TestTrack { get; } = new Track()
        {
            Id = Guid.NewGuid(),
            Artists = new List<ArtistWithRole>()
            {
                new ArtistWithRole()
                {
                    Artist = new Artist(){
                        Name = "Test Artist"
                    },
                    Role = ArtistRole.Main
                },
            },
            Album = new Album()
            {
                Id = Guid.NewGuid(),
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
            _transcoderService = new TranscoderService(_encoderFactory);
        }

        [Fact]
        public async Task CreateJob_MissingEncoder_ReturnsArgumentException()
        {
            // arrange
            _encoderFactory.GetPlatform().Returns(Platform.Windows);
            _encoderFactory.GetEncoder(OutputFormat.MP3).Returns(opt => null);

            // assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _transcoderService.CreateJob(OutputFormat.MP3, opt =>
            {
                opt.Bitrate = 320;
                opt.SourceTrack = TestTrack;
                opt.RequestType = TranscodeRequestType.SingleFile;
            }));
        }
    }
}
