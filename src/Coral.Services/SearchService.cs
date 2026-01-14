using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services.Helpers;
using Coral.Services.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Coral.Services
{
    public interface ISearchService
    {
        public Task<PaginatedCustomData<SearchResult>> Search(string query, int offset = 0, int limit = 100);
    }

    public class SearchService : ISearchService
    {
        private readonly CoralDbContext _context;
        private readonly IFavoritedMappingHelper _favoritedMappingHelper;
        private static readonly Regex _keywordExtractionRegex = RegexPatterns.KeywordExtraction();

        public SearchService(CoralDbContext context, IFavoritedMappingHelper favoritedMappingHelper)
        {
            _context = context;
            _favoritedMappingHelper = favoritedMappingHelper;
        }

        public async Task<PaginatedCustomData<SearchResult>> Search(string query, int offset = 0, int limit = 100)
        {
            var ftsQuery = BuildFts5Query(query);
            if (string.IsNullOrEmpty(ftsQuery))
            {
                return new PaginatedCustomData<SearchResult>
                {
                    Data = new SearchResult { Albums = [], Artists = [], Tracks = [] },
                    AvailableRecords = 0,
                    ResultCount = 0,
                    TotalRecords = 0
                };
            }

            // Query FTS tables - track results drive discovery, album/artist results drive ranking
            var trackIds = await QueryFts5<Guid>("TrackSearch", ftsQuery, offset, limit);
            var totalTrackCount = await CountFts5("TrackSearch", ftsQuery);
            var directAlbumIds = await QueryFts5<Guid>("AlbumSearch", ftsQuery, 0, limit);
            var directArtistIds = await QueryFts5<Guid>("ArtistSearch", ftsQuery, 0, limit);

            var directAlbumIdSet = directAlbumIds.ToHashSet();
            var directArtistIdSet = directArtistIds.ToHashSet();

            // Fetch tracks with album/artist data included
            var tracks = trackIds.Any()
                ? await _context.Tracks
                    .AsNoTracking()
                    .Where(t => trackIds.Contains(t.Id))
                    .Select(t => new TrackDto
                    {
                        Id = t.Id,
                        Title = t.Title,
                        DurationInSeconds = t.DurationInSeconds,
                        Comment = t.Comment,
                        TrackNumber = t.TrackNumber ?? 0,
                        DiscNumber = t.DiscNumber ?? 0,
                        Favorited = false, // Set via MapFavoritedToTracks
                        Artists = t.Artists.Select(a => new ArtistWithRoleDto
                        {
                            Id = a.Artist.Id,
                            Name = a.Artist.Name,
                            Role = a.Role
                        }).ToList(),
                        Album = new SimpleAlbumDto
                        {
                            Id = t.Album.Id,
                            Name = t.Album.Name,
                            ReleaseYear = t.Album.ReleaseYear ?? 0,
                            Type = t.Album.Type,
                            CreatedAt = t.Album.CreatedAt,
                            Favorited = t.Album.Favorite != null,
                            Artists = t.Album.Artists
                                .Where(a => a.Role == ArtistRole.Main)
                                .Select(a => new SimpleArtistDto { Id = a.Artist.Id, Name = a.Artist.Name })
                                .ToList(),
                            Artworks = t.Album.Artwork != null ? new ArtworkDto
                            {
                                Small = "/api/artwork/" + t.Album.Artwork.Id + "?size=Small",
                                Medium = "/api/artwork/" + t.Album.Artwork.Id + "?size=Medium",
                                Original = "/api/artwork/" + t.Album.Artwork.Id + "?size=Original",
                                Colors = t.Album.Artwork.Colors
                            } : null
                        },
                        Genre = t.Genre != null ? new GenreDto { Id = t.Genre.Id, Name = t.Genre.Name } : null
                    })
                    .ToListAsync()
                : [];

            // Extract unique albums/artists discovered from tracks
            var discoveredAlbums = tracks
                .Select(t => t.Album)
                .DistinctBy(a => a.Id)
                .ToList();

            var discoveredArtists = tracks
                .SelectMany(t => t.Artists)
                .Select(a => new SimpleArtistDto { Id = a.Id, Name = a.Name })
                .DistinctBy(a => a.Id)
                .ToList();

            // Rank albums: direct FTS matches first (preserve bm25 order), then discovered
            var albums = discoveredAlbums
                .Where(a => directAlbumIdSet.Contains(a.Id))
                .OrderBy(a => directAlbumIds.IndexOf(a.Id))
                .Concat(discoveredAlbums.Where(a => !directAlbumIdSet.Contains(a.Id)))
                .ToList();

            // Rank artists: direct FTS matches first (preserve bm25 order), then discovered
            var artists = discoveredArtists
                .Where(a => directArtistIdSet.Contains(a.Id))
                .OrderBy(a => directArtistIds.IndexOf(a.Id))
                .Concat(discoveredArtists.Where(a => !directArtistIdSet.Contains(a.Id)))
                .ToList();

            await _favoritedMappingHelper.MapFavoritedToTracks(tracks);

            return new PaginatedCustomData<SearchResult>
            {
                Data = new SearchResult { Albums = albums, Artists = artists, Tracks = tracks },
                AvailableRecords = Math.Max(0, totalTrackCount - offset - limit),
                ResultCount = tracks.Count,
                TotalRecords = totalTrackCount
            };
        }

        private async Task<List<T>> QueryFts5<T>(string tableName, string ftsQuery, int offset, int limit)
        {
            // Use raw SQL to query FTS5 virtual table
            // bm25() returns negative values, lower = better match
            var sql = $"SELECT id FROM {tableName} WHERE {tableName} MATCH {{0}} ORDER BY bm25({tableName}) LIMIT {{1}} OFFSET {{2}}";
            return await _context.Database
                .SqlQueryRaw<T>(sql, ftsQuery, limit, offset)
                .ToListAsync();
        }

        private async Task<int> CountFts5(string tableName, string ftsQuery)
        {
            var sql = $"SELECT COUNT(*) AS Value FROM {tableName} WHERE {tableName} MATCH {{0}}";
            return await _context.Database
                .SqlQueryRaw<int>(sql, ftsQuery)
                .FirstAsync();
        }

        private string? BuildFts5Query(string query)
        {
            var terms = ProcessInputString(query);
            if (!terms.Any())
                return null;

            // Convert to FTS5 prefix query: "calibre shelflife" -> "calibre* AND shelflife*"
            var ftsTerms = terms.Select(t => $"{EscapeFts5Term(t)}*");
            return string.Join(" AND ", ftsTerms);
        }

        private static string EscapeFts5Term(string term)
        {
            // Escape FTS5 special characters: " * ( ) ^
            return term
                .Replace("\"", "\"\"")
                .Replace("*", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("^", "");
        }

        private List<string> ProcessInputString(string inputString)
        {
            // split by word boundary and alphanumerical values using source-generated regex
            // \p{L}    => matches unicode letters / L     Letter
            // \p{Nd}   => matches unicode numbers / Nd    Decimal number
            // +        => one or more of
            // http://www.pcre.org/original/doc/html/pcrepattern.html
            // Note: We don't remove diacritics here - SearchText contains both original and normalized
            // versions, so users can search with or without diacritics
            var matches = _keywordExtractionRegex.Matches(inputString);
            // return split
            return matches?.Select(m => m.Value.ToLower()).Distinct().ToList() ?? new List<string>();
        }
    }
}