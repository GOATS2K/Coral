using Coral.Services.Helpers;
using Coral.TestProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Coral.Services.Tests
{
    public class SearchServiceTests(DatabaseFixture fixture) : TransactionTestBase(fixture)
    {
        private ISearchService SearchService => new SearchService(
            TestDatabase.Mapper,
            TestDatabase.Context,
            Substitute.For<ILogger<SearchService>>(),
            new PaginationService(TestDatabase.Mapper, TestDatabase.Context),
            Substitute.For<IArtworkMappingHelper>(),
            Substitute.For<IFavoritedMappingHelper>());

        [Fact]
        public async Task InsertKeywordsForTrack_NewTrack_InsertsKeywordsSuccessfully()
        {
            // arrange
            var trackToFind = TestDatabase.Starlight;
            // act
            await SearchService.InsertKeywordsForTrack(trackToFind);
            // assert
            var starlightKeyword = await TestDatabase
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
            foreach (var track in TestDatabase.Radio.Tracks)
            {
                await SearchService.InsertKeywordsForTrack(track);
            }
            var trackToFind = TestDatabase.Fuwarin;

            // act
            var results = await SearchService.Search(trackToFind.Title);

            // assert
            Assert.NotEmpty(results.Data.Tracks);
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
            foreach (var track in TestDatabase.ALittleWhileLonger.Tracks)
            {
                await SearchService.InsertKeywordsForTrack(track);
            }

            var trackToFind = TestDatabase.Starlight;
            // act
            var result = await SearchService.Search(query);

            // assert
            Assert.Single(result.Data.Tracks);
            var searchResult = result.Data.Tracks.Single();
            Assert.Equal(trackToFind.Title, searchResult.Title);
        }

        [Fact]
        public async Task Search_ALittleWhileLonger_FindsAlbum()
        {
            // arrange
            foreach (var track in TestDatabase.ALittleWhileLonger.Tracks)
            {
                await SearchService.InsertKeywordsForTrack(track);
            }
            var query = "a little while longer";
            // act
            var result = await SearchService.Search(query);
            // assert
            var album = result.Data.Albums.Single();
            Assert.Equal(TestDatabase.ALittleWhileLonger.Id, album.Id);
        }
    }
}
