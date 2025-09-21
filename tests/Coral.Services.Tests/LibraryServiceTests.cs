using Coral.TestProviders;
using Xunit;

namespace Coral.Services.Tests
{
    public class LibraryServiceTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
    {
        public ILibraryService LibraryService;
        private readonly DatabaseFixture _fixture;

        public LibraryServiceTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetTrack_Believe_ReturnsBelieveDto()
        {
            // arrange
            var trackToFind = _fixture.TestDb.Believe;
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
            var trackWithoutArtwork = _fixture.TestDb.Believe;

            // act
            var artwork = await LibraryService.GetArtworkForTrack(trackWithoutArtwork.Id);

            // assert
            Assert.Null(artwork);
        }
        [Fact]
        public async Task GetArtworkForAlbum_AlbumWithArtwork_ReturnsArtworkPath()
        {
            // arrange
            var albumWithArtwork = _fixture.TestDb.ALittleWhileLonger;
            // act
            var artwork = await LibraryService.GetArtworkForAlbum(albumWithArtwork.Id);
            // assert
            Assert.NotNull(artwork);
        }

        public Task InitializeAsync()
        {
            LibraryService = new LibraryService(_fixture.TestDb.Context, _fixture.TestDb.Mapper);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
