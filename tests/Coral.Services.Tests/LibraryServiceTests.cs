using Coral.Services.ChannelWrappers;
using Coral.TestProviders;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Coral.Services.Tests
{
    public class LibraryServiceTests(DatabaseFixture fixture) : TransactionTestBase(fixture)
    {
        private ILibraryService LibraryService => new LibraryService(
            TestDatabase.Context,
            TestDatabase.Mapper,
            new ScanChannel(),
            Substitute.For<ILogger<LibraryService>>(),
            Substitute.For<IEmbeddingService>());

        [Fact]
        public async Task GetTrack_Believe_ReturnsBelieveDto()
        {
            // arrange
            var trackToFind = TestDatabase.Believe;
            // act
            var track = await LibraryService.GetTrack(trackToFind.Id);
            // assert
            Assert.NotNull(track);
            Assert.Equal(trackToFind.Title, track.Title);
        }

        [Fact]
        public async Task GetTracks_WithNoArguments_ReturnsTracks()
        {
            // arrange
            // act
            var tracks = await LibraryService.GetTracks().ToListAsync();
            // assert
            Assert.NotEmpty(tracks);
        }
        [Fact]
        public async Task GetAlbums_WithNoArguments_ReturnsAlbums()
        {
            // arrange
            // act
            var albums = await LibraryService.GetAlbums().ToListAsync();
            // assert
            Assert.NotEmpty(albums);
        }
        [Fact]
        public async Task GetArtworkForTrack_TrackWithNoArtwork_ReturnsNull()
        {
            // arrange
            var trackWithoutArtwork = TestDatabase.Believe;

            // act
            var artwork = await LibraryService.GetArtworkForTrack(trackWithoutArtwork.Id);

            // assert
            Assert.Null(artwork);
        }
        [Fact]
        public async Task GetArtworkForAlbum_AlbumWithArtwork_ReturnsArtworkPath()
        {
            // arrange
            var albumWithArtwork = TestDatabase.ALittleWhileLonger;
            // act
            var artwork = await LibraryService.GetArtworkForAlbum(albumWithArtwork.Id);
            // assert
            Assert.NotNull(artwork);
        }
    }
}
