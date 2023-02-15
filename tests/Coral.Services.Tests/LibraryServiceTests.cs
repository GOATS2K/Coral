using ATL;
using Coral.Database;
using Coral.TestProviders;
using System;
using System.Collections.Generic;
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

        // I'd love to use [Theory] here, but I'm lazy and I want to use my test objects
        [Fact]
        public async Task Search_FullTrackName_FindsTrack()
        {
            // arrange
            var trackToFind = _testDatabase.OldTimesSake;
            var query = "old times' sake";
            // act
            var result = await _libraryService.Search(query);
            // assert
            Assert.Single(result.Tracks);
            var searchResult = result.Tracks.Single();
            Assert.Equal(trackToFind.Title, searchResult.Title);
        }

        [Fact]
        public async Task Search_IncompleteTrackName_FindsTrack()
        {
            // arrange
            var trackToFind = _testDatabase.OldTimesSake;
            var query = "old time";
            // act
            var result = await _libraryService.Search(query);
            // assert
            Assert.Single(result.Tracks);
            var searchResult = result.Tracks.Single();
            Assert.Equal(trackToFind.Title, searchResult.Title);
        }

        [Fact]
        public async Task Search_IncompleteArtistName_FindsArtist()
        {
            // arrange
            var artistToFind = _testDatabase.Tatora;
            var query = "tat";
            // act
            var result = await _libraryService.Search(query);
            // assert
            Assert.Single(result.Artists);
            var searchResult = result.Artists.Single();
            Assert.Equal(artistToFind.Name, searchResult.Name);
        }
    }
}
