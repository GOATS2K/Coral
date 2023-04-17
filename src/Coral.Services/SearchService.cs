using AutoMapper;
using AutoMapper.EntityFrameworkCore;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Comparers;
using Coral.Dto.Models;
using Coral.Services.Models;
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
        public Task<SearchResult> Search(string query);
    }
    public class SearchService : ISearchService
    {
        private readonly IMapper _mapper;
        private readonly ILogger<SearchService> _logger;
        private readonly CoralDbContext _context;

        public SearchService(IMapper mapper, CoralDbContext context, ILogger<SearchService> logger)
        {
            _mapper = mapper;
            _context = context;
            _logger = logger;
        }

        private ExpressionStarter<Keyword> GenerateSearchQueryForKeywords(List<string> keywords)
        {
            var predicate = PredicateBuilder.New<Keyword>();
            foreach (var keyword in keywords)
            {
                // I chose to only set the wildcard on the end of the keyword
                // for performance reasons - benefiting from indexing done by the database
                predicate = predicate
                    .Or(k => EF.Functions.Like(k.Value, $"{keyword}%"));
            }
            return predicate;
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

        public async Task<SearchResult> Search(string query)
        {
            // get all tracks matching keywords
            var keywords = ProcessInputString(query);
            var trackIds = await _context.Keywords
                .Where(GenerateSearchQueryForKeywords(keywords))
                .Select(k => k.Tracks)
                .SelectMany(t => t)
                .Select(t => t.Id)
                .ToListAsync();

            var idGroups = trackIds.GroupBy(t => t);
            // get only the IDs matching the query
            var idsMatchingQuery = idGroups
                .Where(g => g.Count() == keywords.Count())
                .Select(g => g.Key);

            // fetch tracks matching query
            var tracks = await _context.Tracks
                .Where(t => idsMatchingQuery.Contains(t.Id))
                .ProjectTo<TrackDto>(_mapper.ConfigurationProvider)
                .ToListAsync();


            var artists = tracks.Select(a => a.Artists)
                .SelectMany(a => a);
            return new SearchResult()
            {
                Albums = tracks.Select(t => t.Album)
                .Distinct(new SimpleAlbumDtoComparer())
                .ToList(),
                Artists = artists
                .Distinct(new SimpleArtistDtoComparer())
                .ToList(),
                Tracks = tracks
            };
        }

        private List<string> ProcessInputString(string inputString)
        {
            // split by word boundary and alphanumerical values
            // \p{L}    => matches unicode letters / L     Letter
            // \p{Nd}   => matches unicode numbers / Nd    Decimal number
            // +        => one or more of
            // http://www.pcre.org/original/doc/html/pcrepattern.html
            var pattern = @"[\p{L}\p{Nd}]+";
            var matches = Regex.Matches(inputString, pattern, RegexOptions.IgnoreCase);
            // return split
            return matches?.Select(m => m.Value.ToLower()).Distinct().ToList() ?? new List<string>();
        }
    }
}
