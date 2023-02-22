using ATL;
using Coral.Database;
using Coral.TestProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Coral.Services.Tests
{
    public class LibraryServiceTests
    {
        private readonly ILibraryService _libraryService;
        private readonly TestDatabase _testDatabase;

        public LibraryServiceTests()
        {
            var testDatabase = new TestDatabase();
            _testDatabase = testDatabase;
            _libraryService = new LibraryService(testDatabase.Context, testDatabase.Mapper);
        }

        [Fact]
        public async Task GetTrack_Believe_ReturnsBelieveDto()
        {
            // arrange
            var trackToFind = _testDatabase.Believe;
            // act
            var track = await _libraryService.GetTrack(trackToFind.Id);
            // assert
            Assert.NotNull(track);
            Assert.Equal(trackToFind.Title, track.Title);
        }

        [Fact]
        public async Task GetTracks_WithNoArguments_ReturnsTracks()
        {
            // arrange
            // act
            var tracks = await _libraryService.GetTracks().ToListAsync();
            // assert
            Assert.NotEmpty(tracks);
        }
        [Fact]
        public async Task GetAlbums_WithNoArguments_ReturnsAlbums()
        {
            // arrange
            // act
            var albums = await _libraryService.GetAlbums().ToListAsync();
            // assert
            Assert.NotEmpty(albums);
        }
        [Fact]
        public async Task GetArtworkForTrack_TrackWithNoArtwork_ReturnsNull()
        {
            // arrange
            var trackWithoutArtwork = _testDatabase.Believe;

            // act
            var artwork = await _libraryService.GetArtworkForTrack(trackWithoutArtwork.Id);

            // assert
            Assert.Null(artwork);
        }
        [Fact]
        public async Task GetArtworkForAlbum_AlbumWithArtwork_ReturnsArtworkPath()
        {
            // arrange
            var albumWithArtwork = _testDatabase.ALittleWhileLonger;
            // act
            var artwork = await _libraryService.GetArtworkForAlbum(albumWithArtwork.Id);
            // assert
            Assert.NotNull(artwork);
        }
    }
}
