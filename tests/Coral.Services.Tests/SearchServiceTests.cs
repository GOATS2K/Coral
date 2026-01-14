using Coral.Services.Helpers;
using Coral.TestProviders;
using NSubstitute;
using Xunit;

namespace Coral.Services.Tests
{
    public class SearchServiceTests(DatabaseFixture fixture) : TransactionTestBase(fixture)
    {
        private ISearchService SearchService => new SearchService(
            TestDatabase.Context,
            Substitute.For<IFavoritedMappingHelper>());

        [Fact]
        public async Task Search_FuwarinViaNonLatinCharacters_FindsTrack()
        {
            // arrange
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
            var query = "a little while longer";

            // act
            var result = await SearchService.Search(query);

            // assert
            var album = result.Data.Albums.Single();
            Assert.Equal(TestDatabase.ALittleWhileLonger.Id, album.Id);
        }
    }
}
