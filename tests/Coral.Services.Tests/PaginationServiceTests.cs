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
