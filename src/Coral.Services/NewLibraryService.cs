using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services.ChannelWrappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coral.Services;

public interface INewLibraryService
{
    Task<List<MusicLibraryDto>> GetMusicLibraries();
    Task<MusicLibrary?> AddMusicLibrary(string path);
}

public class NewLibraryService : INewLibraryService
{
    private readonly CoralDbContext _context;
    private readonly IMapper _mapper;
    private readonly IScanChannel _scanChannel;
    private readonly ILogger<NewLibraryService> _logger;

    public NewLibraryService(
        CoralDbContext context,
        IMapper mapper,
        IScanChannel scanChannel,
        ILogger<NewLibraryService> logger)
    {
        _context = context;
        _mapper = mapper;
        _scanChannel = scanChannel;
        _logger = logger;
    }

    public async Task<List<MusicLibraryDto>> GetMusicLibraries()
    {
        return await _context
            .MusicLibraries
            .ProjectTo<MusicLibraryDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<MusicLibrary?> AddMusicLibrary(string path)
    {
        try
        {
            var contentDirectory = new DirectoryInfo(path);
            if (!contentDirectory.Exists)
            {
                throw new ApplicationException("Content directory does not exist.");
            }

            var library = await _context.MusicLibraries.FirstOrDefaultAsync(m => m.LibraryPath == path)
                          ?? new MusicLibrary()
                          {
                              LibraryPath = path,
                              AudioFiles = new List<AudioFile>()
                          };

            _context.MusicLibraries.Add(library);
            await _context.SaveChangesAsync();

            // Queue initial scan instead of emitting event
            var requestId = Guid.NewGuid().ToString();
            await _scanChannel.GetWriter().WriteAsync(new ScanJob(
                Library: library,
                SpecificDirectory: null,
                Incremental: false,
                RequestId: requestId,
                Trigger: ScanTrigger.LibraryAdded
            ));

            _logger.LogInformation("Library added and scan queued: {Path} (RequestId: {RequestId})", path, requestId);

            return library;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to add music library {Path}", path);
            return null;
        }
    }
}
