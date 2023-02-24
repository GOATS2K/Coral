using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Dto.Models;
using Coral.TestProviders;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Coral.Dto.Tests
{
    public class ArtistDtoTests
    {
        private readonly IMapper _mapper;
        private readonly TestDatabase _testDatabase;
        public ArtistDtoTests()
        {
            var testDatabase = new TestDatabase();
            _mapper = testDatabase.Mapper;
            _testDatabase = testDatabase;
        }

        [Fact]
        public async Task ArtistDto_WithMappingFromArtistWithRole_FillsAppropriateLists()
        {
            // arrange
            // act
            var mainOnly = _testDatabase
                .Context
                .Artists
                .Where(a => a.Id == _testDatabase.Lenzman.Id)
                .ProjectTo<ArtistDto>(_mapper.ConfigurationProvider);
            var mainOnlyExp = mainOnly.Expression;
            var mainOnlyQuery = await mainOnly.SingleAsync();

            var guestOnlyQuery = await _testDatabase
                .Context
                .Artists
                .Where(a => a.Id == _testDatabase.Slay.Id)
                .ProjectTo<ArtistDto>(_mapper.ConfigurationProvider)
                .SingleAsync(); 

            var remixerOnlyQuery = await _testDatabase
                .Context
                .Artists
                .Where(a => a.Id == _testDatabase.Jubei.Id)
                .ProjectTo<ArtistDto>(_mapper.ConfigurationProvider)
                .SingleAsync();

            // assert
            Assert.Empty(remixerOnlyQuery.Releases);
            Assert.Empty(remixerOnlyQuery.FeaturedIn);

            Assert.Empty(guestOnlyQuery.Releases);
            Assert.Empty(guestOnlyQuery.RemixerIn);

            Assert.Empty(mainOnlyQuery.FeaturedIn);
            Assert.Empty(mainOnlyQuery.RemixerIn);

            Assert.Single(remixerOnlyQuery.RemixerIn);
            Assert.Single(guestOnlyQuery.FeaturedIn);
            Assert.Single(mainOnlyQuery.Releases);
        }
    }
}
