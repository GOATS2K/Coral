using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.TestProviders;
using Xunit;

namespace Coral.Dto.Tests
{
    public class AlbumDtoTests(DatabaseFixture fixture) : TransactionTestBase(fixture)
    {
        [Fact]
        public void AlbumDto_AlbumWithMultiplArtistRoles_SetsArtistRoleOnTrackDto()
        {
            // arrange
            var album = TestDatabase.ALittleWhileLonger;

            // act
            var result = TestDatabase.Mapper.Map<AlbumDto>(album);

            // assert
            Assert.Equal(album.Name, result.Name);
            Assert.Equal(album.Id, result.Id);
            Assert.Equal(album.Tracks.Count(), result.Tracks.Count());

            foreach (var track in result.Tracks)
            {
                var databaseTrack = album.Tracks.Single(t => t.Id == track.Id);
                Assert.Equal(databaseTrack.Title, track.Title);
                foreach (var artist in track.Artists)
                {
                    // artist on TrackDto should use Artist ID, not ArtistOnTrack ID
                    var databaseArtist = databaseTrack.Artists.Single(a => a.ArtistId == artist.Id);
                    Assert.Equal(databaseArtist.Role, artist.Role);
                }
            }
        }

        [Fact]
        public void AlbumDto_AlbumWithType_SetsType()
        {
            // arrange
            var album = TestDatabase.ALittleWhileLonger;

            // act
            var result = TestDatabase.Mapper.Map<AlbumDto>(album);

            // assert
            Assert.Equal(AlbumType.MiniAlbum, result.Type);
        }

        [Fact]
        public void AlbumDto_AlbumWithMultipleArtistRoles_SetsMainContributingArtistsAsAlbumArtists()
        {
            // arrange
            var album = TestDatabase.ALittleWhileLonger;

            // act
            var result = TestDatabase.Mapper.Map<AlbumDto>(album);

            // assert
            Assert.Equal(album.Name, result.Name);
            Assert.Equal(album.Id, result.Id);
            Assert.Equal(album.Tracks.Count(), result.Tracks.Count());

            var mainArtistsOnDatabaseAlbum = album
                .Tracks
                .Select(a => a.Artists)
                .SelectMany(a => a)
                .DistinctBy(a => a.Artist.Id)
                .Where(a => a.Role == ArtistRole.Main)
                .Select(a => a.Artist)
                .ToList();

            // this should just be lenzman
            Assert.Single(mainArtistsOnDatabaseAlbum);
            Assert.Equal(mainArtistsOnDatabaseAlbum.Count(), result.Artists.Count());
        }
    }
}