using Castle.Core.Logging;
using Coral.TestProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
            var logger = Substitute.For<ILogger<SearchService>>();
            _searchService = new SearchService(testDatabase.Mapper, testDatabase.Context, logger);
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

        [Fact]
        public async Task Search_FuwarinViaNonLatinCharacters_FindsTrack()
        {
            // arrange
            // generate keywords for tracks in album
            foreach (var track in _testDatabase.Radio.Tracks)
            {
                await _searchService.InsertKeywordsForTrack(track);
            }
            var trackToFind = _testDatabase.Fuwarin;

            // act
            var results = await _searchService.Search(trackToFind.Title);

            // assert
            Assert.NotEmpty(results.Tracks);
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
