using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.TestProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Coral.Services.Tests
{
    public class PaginationServiceTests
    {
        private readonly IPaginationService _paginationService;
        private readonly TestDatabase _testDatabase;

        public PaginationServiceTests()
        {
            var testDatabase = new TestDatabase();
            _paginationService = new PaginationService(testDatabase.Mapper, testDatabase.Context);
            _testDatabase = testDatabase;
        }

        [Fact]
        public async Task PaginatedQueryable_FiveTracksFromLenzman_GetsAsPaginatedData()
        {
            // arrange
            var take = 5;
            var skip = 0;

            // act
            var results = await _paginationService.PaginateQuery<Track, TrackDto>(query =>
            {
                return query.Where(t => t.Artists.Any(t => t.Artist.Name == _testDatabase.Lenzman.Name));
            }, skip, take);
            // assert
            var selfQuery = _testDatabase.Context.Tracks.Where(t => t.Artists.Any(a => a.Artist.Name == _testDatabase.Lenzman.Name)).ToList();

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
            var results = await _paginationService.PaginateQuery<Album, SimpleAlbumDto>(skip, take);
            // assert
            Assert.Single(results.Data);
            Assert.Equal(_testDatabase.Context.Albums.Count(), results.TotalRecords);
            Assert.Equal(results.Data.Count(), results.ResultCount);
            Assert.NotEqual(0, results.AvailableRecords);
        }
    }
}
