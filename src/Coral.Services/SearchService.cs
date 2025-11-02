using AutoMapper;
using Coral.BulkExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Comparers;
using Coral.Dto.Models;
using Coral.Services.Models;
using Diacritics.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Coral.Services
{
    public interface ISearchService
    {
        public Task InsertKeywordsForTrack(Track track);
        public Task InsertKeywordsForTracksBulk(Dictionary<Track, string> trackKeywordStrings);
        public Task<PaginatedCustomData<SearchResult>> Search(string query, int offset = 0, int limit = 100);
    }

    public class SearchService : ISearchService
    {
        private readonly IMapper _mapper;
        private readonly ILogger<SearchService> _logger;
        private readonly CoralDbContext _context;
        private readonly IPaginationService _paginationService;
        private static readonly Regex _keywordExtractionRegex = SearchRegexPatterns.KeywordExtraction();

        public SearchService(IMapper mapper, CoralDbContext context, ILogger<SearchService> logger,
            IPaginationService paginationService)
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
                && !missingKeywordsOnTrack.Any())
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

        public async Task InsertKeywordsForTracksBulk(Dictionary<Track, string> trackKeywordStrings)
        {
            if (!trackKeywordStrings.Any())
                return;

            _logger.LogInformation("Starting bulk keyword insertion for {TrackCount} tracks", trackKeywordStrings.Count);

            // Step 1: Extract all keywords from pre-computed keyword strings
            var trackKeywords = new Dictionary<Guid, List<string>>();
            var allKeywordValues = new HashSet<string>();

            foreach (var (track, keywordString) in trackKeywordStrings)
            {
                var keywords = ProcessInputString(keywordString);
                trackKeywords[track.Id] = keywords;
                foreach (var keyword in keywords)
                {
                    allKeywordValues.Add(keyword);
                }
            }

            _logger.LogDebug("Extracted {KeywordCount} unique keyword values from {TrackCount} tracks",
                allKeywordValues.Count, trackKeywordStrings.Count);

            // Step 2: Query database for existing keywords (single query)
            var existingKeywords = await _context.Keywords
                .Where(k => allKeywordValues.Contains(k.Value))
                .ToListAsync();

            var existingKeywordDict = new Dictionary<string, Keyword>();

            // Add existing keywords to BulkContext cache so they can be used in relationships
            foreach (var existingKeyword in existingKeywords)
            {
                var keyword = await _context.Keywords.GetOrAddBulk(
                    k => k.Value,
                    () => existingKeyword);
                existingKeywordDict[existingKeyword.Value] = keyword;
            }

            _logger.LogDebug("Found {ExistingCount} existing keywords in database", existingKeywords.Count);

            // Step 3: Create new keywords that don't exist yet using BulkContext
            var newKeywordValues = allKeywordValues.Except(existingKeywords.Select(k => k.Value)).ToList();

            if (newKeywordValues.Any())
            {
                _logger.LogInformation("Creating {NewKeywordCount} new keywords", newKeywordValues.Count);

                // Use bulk insert for new keywords (they'll be saved by the caller)
                foreach (var keywordValue in newKeywordValues)
                {
                    var keyword = await _context.Keywords.GetOrAddBulk(
                        k => k.Value,
                        () => new Keyword
                        {
                            Id = Guid.NewGuid(),
                            Value = keywordValue,
                            CreatedAt = DateTime.UtcNow,
                            Tracks = new List<Track>()
                        });

                    existingKeywordDict[keywordValue] = keyword;
                }
            }

            // Step 4: Register track-keyword relationships using BulkContext
            // Note: Tracks should already be in BulkContext cache from previous save with retainCache=true
            var relationshipCount = 0;
            foreach (var (track, _) in trackKeywordStrings)
            {
                var keywordValues = trackKeywords[track.Id];
                foreach (var keywordValue in keywordValues)
                {
                    if (existingKeywordDict.TryGetValue(keywordValue, out var keyword))
                    {
                        _context.AddRelationshipBulk<Keyword, Track>(keyword, track);
                        relationshipCount++;
                    }
                }
            }

            _logger.LogInformation("Registered {RelationshipCount} track-keyword relationships in BulkContext",
                relationshipCount);
        }

        public async Task<PaginatedCustomData<SearchResult>> Search(string query, int offset = 0, int limit = 100)
        {
            var searchResult = await GetTracksForKeywords(query);

            // fetch tracks matching query
            var paginated =
                await _paginationService.PaginateQuery<Track, TrackDto>(
                    t => t.Where(tr => searchResult.Contains(tr.Id)), offset, limit);
            var tracks = paginated.Data;

            var artists = tracks.Select(a => a.Artists)
                .SelectMany(a => a);

            var finalResults = new SearchResult()
            {
                Albums = tracks.Select(t => t.Album)
                    .DistinctBy(t => t.Id)
                    .ToList(),
                Artists = artists
                    .DistinctBy(t => t.Id)
                    .Select(t => new SimpleArtistDto()
                    {
                        Id = t.Id,
                        Name = t.Name
                    })
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

        private async Task<List<Guid>> GetTracksForKeywords(string query)
        {
            // get all tracks matching keywords
            var keywords = ProcessInputString(query);
            var lastResult = new List<Guid>();
            var currentSearchResult = new List<Guid>();
            foreach (var keyword in keywords)
            {
                var currentKeywordResult = await _context.Keywords
                    .AsNoTracking()
                    .Where(k => EF.Functions.Like(k.Value, $"%{keyword}%"))
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

            // split by word boundary and alphanumerical values using source-generated regex
            // \p{L}    => matches unicode letters / L     Letter
            // \p{Nd}   => matches unicode numbers / Nd    Decimal number
            // +        => one or more of
            // http://www.pcre.org/original/doc/html/pcrepattern.html
            var matches = _keywordExtractionRegex.Matches(sanitized);
            // return split
            return matches?.Select(m => m.Value.ToLower()).Distinct().ToList() ?? new List<string>();
        }
    }
}