using AutoMapper;
using AutoMapper.EntityFrameworkCore;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Coral.Services
{
    public interface ISearchService
    {
        public Task InsertKeywordsForTrack(Track track);
        public Task<List<Track>> Search(string query);
    }
    public class SearchService : ISearchService
    {
        private readonly IMapper _mapper;
        private readonly CoralDbContext _context;

        public SearchService(IMapper mapper, CoralDbContext context)
        {
            _mapper = mapper;
            _context = context;
        }

        public async Task InsertKeywordsForTrack(Track track)
        {
            var keywords = ProcessInputString(track.ToString());
            // check for existing keywords
            var existingKeywords = await _context.Keywords.Where(k => keywords.Contains(k.Value)).ToListAsync();
            var missingKeywordsOnTrack = existingKeywords.Where(k => !track.Keywords.Contains(k)).ToList();
            
            if (existingKeywords.Count() == keywords.Count() && missingKeywordsOnTrack.Count() == 0)
            {
                return;
            }

            foreach (var missingKeyword in missingKeywordsOnTrack)
            {
                // if existing keyword is not on track, add to track
                track.Keywords.Add(missingKeyword);

                // remove keyword from list
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

        public async Task<List<Track>> Search(string query)
        {
            // get all tracks matching keywords
            var keywords = ProcessInputString(query);
            // here we'll get a keyword match for every part of the string
            var results = await _context.Keywords.Where(k => keywords.Contains(k.Value))
                .Include(k => k.Tracks)
                .ToListAsync();
            // get list of tracks
            var tracks = results.Select(k => k.Tracks).ToList();
            // if there's just one list of tracks, return as-is
            if (tracks.Count == 1)
            {
                return tracks[0];
            }

            // find the track in common in all the queries
            IEnumerable<Track> tracklist = new List<Track>();
            for (var i = 0; i < tracks.Count; i++)
            {
                if (i + 1 == tracks.Count)
                {
                    break;
                }
                var nextList = tracks[i + 1];
                tracklist = tracks[i].Intersect(nextList);
            }
            
            return tracklist.ToList();
        }

        private List<string> ProcessInputString(string inputString)
        {
            // split by word boundary and alphanumerical values
            var pattern = @"([a-zA-Z0-9])\w*";
            var matches = Regex.Matches(inputString, pattern);
            // return split
            return matches?.Select(m => m.Value.ToLower()).Distinct().ToList() ?? new List<string>();
        }
    }
}
