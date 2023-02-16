using Coral.TestProviders;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Coral.Services.Tests
{
    public class SearchServiceTests
    {
        private readonly ISearchService _searchService;
        private readonly TestDatabase _testDatabase;

        public SearchServiceTests()
        {
            var testDatabase = new TestDatabase();
            _testDatabase = testDatabase;
            _searchService = new SearchService(testDatabase.Mapper, testDatabase.Context);
        }

        [Fact]
        public async Task InsertKeywordsForTrack_NewTrack_InsertsKeywordsSuccessfully()
        {
            // arrange
            var trackToFind = _testDatabase.Starlight;
            // act
            await _searchService.InsertKeywordsForTrack(trackToFind);
            // assert
            var starlightKeyword = await _testDatabase
                .Context
                .Keywords
                .Where(k => k.Value == "starlight")
                .Include(k => k.Tracks)
                .FirstAsync();
            Assert.NotEmpty(starlightKeyword.Tracks);
        }

        [Theory]
        [InlineData("lenzman starlight")]
        [InlineData("starlight lenzman")]
        [InlineData("starlight a little while longer")]
        [InlineData("starlight")]
        [InlineData("star")]
        [InlineData("lenz star")]
        public async Task Search_Starlight_FindsTrack(string query)
        {
            // arrange
            // generate keywords for all tracks in album
            foreach (var track in _testDatabase.ALittleWhileLonger.Tracks)
            {
                await _searchService.InsertKeywordsForTrack(track);
            }

            var trackToFind = _testDatabase.Starlight;
            // act
            var result = await _searchService.Search(query);

            // assert
            Assert.Single(result.Tracks);
            var searchResult = result.Tracks.Single();
            Assert.Equal(trackToFind.Title, searchResult.Title);
        }

        [Fact]
        public async Task Search_ALittleWhileLonger_FindsAlbum()
        {
            // arrange
            foreach (var track in _testDatabase.ALittleWhileLonger.Tracks)
            {
                await _searchService.InsertKeywordsForTrack(track);
            }
            var query = "a little while longer";
            // act
            var result = await _searchService.Search(query);
            // assert
            var album = result.Albums.Single();
            Assert.Equal(_testDatabase.ALittleWhileLonger.Id, album.Id);
        }
    }
}
