using AutoMapper;
using AutoMapper.EntityFrameworkCore;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Comparers;
using Coral.Dto.Models;
using Coral.Services.Models;
using Diacritics.Extensions;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Coral.Services
{
    public interface ISearchService
    {
        public Task InsertKeywordsForTrack(Track track);
        public Task<PaginatedCustomData<SearchResult>> Search(string query, int offset = 0, int limit = 100);
    }
    public class SearchService : ISearchService
    {
        private readonly IMapper _mapper;
        private readonly ILogger<SearchService> _logger;
        private readonly CoralDbContext _context;
        private readonly IPaginationService _paginationService;

        public SearchService(IMapper mapper, CoralDbContext context, ILogger<SearchService> logger, IPaginationService paginationService)
        {
            _mapper = mapper;
            _context = context;
            _logger = logger;
            _paginationService = paginationService;
        }

        public async Task InsertKeywordsForTrack(Track track)
        {
            var keywords = ProcessInputString(track.ToString());
            // check for existing keywords
            var existingKeywords = await _context
                .Keywords
                .Where(k => keywords.Contains(k.Value))
                .ToListAsync();

            var missingKeywordsOnTrack = existingKeywords
                .Where(k => !track.Keywords.Contains(k))
                .ToList();

            // in the event we've indexed all the keywords present on a track before
            if (existingKeywords.Count() == keywords.Count()
                && missingKeywordsOnTrack.Count() == 0)
            {
                return;
            }

            foreach (var missingKeyword in missingKeywordsOnTrack)
            {
                // if existing keyword is not on track, add to track
                track.Keywords.Add(missingKeyword);

                // remove keyword from list of incoming keywords
                keywords.Remove(missingKeyword.Value);
            }

            if (keywords.Count > 0)
            {
                var newKeywords = keywords.Select(k => new Keyword()
                {
                    Value = k
                });
                track.Keywords.AddRange(newKeywords);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<PaginatedCustomData<SearchResult>> Search(string query, int offset = 0, int limit = 100)
        {
            var searchResult = await GetTracksForKeywords(query);

            // fetch tracks matching query
            var paginated = await _paginationService.PaginateQuery<Track, TrackDto>(t => t.Where(tr => searchResult.Contains(tr.Id)), offset, limit);
            var tracks = paginated.Data;

            var artists = tracks.Select(a => a.Artists)
                .SelectMany(a => a);

            var finalResults = new SearchResult()
            {
                Albums = tracks.Select(t => t.Album)
                .Distinct(new SimpleAlbumDtoComparer())
                .ToList(),
                Artists = artists
                .Distinct(new SimpleArtistDtoComparer())
                .ToList(),
                Tracks = tracks
            };

            return new PaginatedCustomData<SearchResult>()
            {
                AvailableRecords = paginated.AvailableRecords,
                ResultCount = paginated.ResultCount,
                TotalRecords = paginated.TotalRecords,
                Data = finalResults
            };
        }

        private async Task<List<int>> GetTracksForKeywords(string query)
        {
            // get all tracks matching keywords
            var keywords = ProcessInputString(query);
            var lastResult = new List<int>();
            var currentSearchResult = new List<int>();
            foreach (var keyword in keywords)
            {
                var currentKeywordResult = await _context.Keywords
                            .AsNoTracking()
                            .Where(k => EF.Functions.Like(k.Value, $"{keyword}%"))
                            .Select(k => k.Tracks)
                            .SelectMany(t => t)
                            .Select(t => t.Id)
                            .ToListAsync();

                // if last keyword is not empty, perform an intersection with new result
                if (lastResult.Any())
                {
                    currentSearchResult = lastResult.Intersect(currentKeywordResult).ToList();
                }

                // return current search if only one keyword was searched for
                if (keywords.Count() == 1)
                {
                    return currentKeywordResult;
                }

                lastResult = currentSearchResult.Any() ? currentSearchResult : currentKeywordResult;
            }

            return currentSearchResult;
        }

        private List<string> ProcessInputString(string inputString)
        {
            // sanitize string for diacritics first
            var sanitized = inputString.RemoveDiacritics();

            // split by word boundary and alphanumerical values
            // \p{L}    => matches unicode letters / L     Letter
            // \p{Nd}   => matches unicode numbers / Nd    Decimal number
            // +        => one or more of
            // http://www.pcre.org/original/doc/html/pcrepattern.html
            var pattern = @"[\p{L}\p{Nd}]+";
            var matches = Regex.Matches(sanitized, pattern, RegexOptions.IgnoreCase);
            // return split
            return matches?.Select(m => m.Value.ToLower()).Distinct().ToList() ?? new List<string>();
        }
    }
}
