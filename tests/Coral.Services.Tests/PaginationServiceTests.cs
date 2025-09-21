using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.TestProviders;
using Xunit;

namespace Coral.Services.Tests
{
    public class PaginationServiceTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
    {
        public IPaginationService PaginationService;
        
        private readonly DatabaseFixture _fixture;

        public PaginationServiceTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task PaginatedQueryable_FiveTracksFromLenzman_GetsAsPaginatedData()
        {
            // arrange
            var take = 5;
            var skip = 0;

            // act
            var results = await PaginationService.PaginateQuery<Track, TrackDto>(query =>
            {
                return query.Where(t => t.Artists.Any(t => t.Artist.Name == _fixture.TestDb.Lenzman.Name));
            }, skip, take);
            // assert
            var selfQuery = _fixture.TestDb.Context.Tracks.Where(t => t.Artists.Any(a => a.Artist.Name == _fixture.TestDb.Lenzman.Name)).ToList();

            var resultCount = results.ResultCount;
            var availableRecords = results.AvailableRecords;
            var totalRecords = results.TotalRecords;

            Assert.Equal(selfQuery.Count(), totalRecords);
            Assert.Equal(take, resultCount);
        }

        [Fact]
        public async Task PaginatedQuery_OneLimitZeroSkip_GetsASingleAlbum()
        {
            // arrange
            var take = 1;
            var skip = 0;
            // act
            var results = await PaginationService.PaginateQuery<Album, SimpleAlbumDto>(skip, take);
            // assert
            Assert.Single(results.Data);
            Assert.Equal(_fixture.TestDb.Context.Albums.Count(), results.TotalRecords);
            Assert.Equal(results.Data.Count(), results.ResultCount);
            Assert.NotEqual(0, results.AvailableRecords);
        }

        public Task InitializeAsync()
        {
            PaginationService = new PaginationService(_fixture.TestDb.Mapper, _fixture.TestDb.Context);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
